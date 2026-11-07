import * as Y from 'yjs';
import { fingerprintHash } from './fingerprint.mjs';

export const MAX_DRAWING_BYTES = 1024 * 1024;
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const key = value => typeof value === 'string' && value.length > 0 && value.length <= 128 && !['__proto__', 'constructor', 'prototype'].includes(value);
const object = value => value && typeof value === 'object' && !Array.isArray(value) && (Object.getPrototypeOf(value) === Object.prototype || Object.getPrototypeOf(value) === null);
const compare = (a, b) => a < b ? -1 : a > b ? 1 : 0;

export function assertDrawingBytes(state) {
  if (!(state instanceof Uint8Array) || !state.length || state.length > MAX_DRAWING_BYTES) throw new Error('Drawing exceeds the 1 MiB state limit');
}

/** JSON only: SDK binaries, nested Y types and prototype keys never enter a room. */
function canonical(value, budget, depth = 0) {
  if (++budget.count > 250000 || depth > 64) throw new Error('Drawing is too complex');
  if (value === null || typeof value === 'boolean' || typeof value === 'string') return value;
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  if (Array.isArray(value)) return value.map(item => canonical(item, budget, depth + 1));
  if (!object(value)) throw new Error('Drawing values must contain only JSON');
  const result = Object.create(null);
  for (const name of Object.keys(value).sort()) {
    if (!key(name) || /^(dataurl|base64|bytes)$/i.test(name)) throw new Error('Drawing binaries belong in file storage');
    result[name] = canonical(value[name], budget, depth + 1);
  }
  return result;
}

/** Keep whole element records (including SDK tombstones); local viewport state is absent. */
export function projectDrawingDocument(doc) {
  for (const name of doc.share.keys()) if (!['elements', 'files', 'scene'].includes(name)) throw new Error('Unknown drawing map');
  const elementsMap = doc.getMap('elements'), filesMap = doc.getMap('files'), scene = doc.getMap('scene');
  for (const map of [elementsMap, filesMap, scene]) if (map._start !== null) throw new Error('Drawing roots must be maps');
  if (elementsMap.size > 10000 || filesMap.size > 1000) throw new Error('Drawing has too many elements or files');
  const budget = { count: 0 }, elements = [], files = Object.create(null), assets = new Set();
  for (const [id, value] of elementsMap) {
    if (!key(id) || !object(value) || value.id !== id || typeof value.type !== 'string' || !key(value.type)) throw new Error('Invalid drawing element');
    if (typeof value.link === 'string' && value.link) {
      let allowed = value.link.startsWith('/') && !value.link.startsWith('//');
      try { allowed ||= ['http:', 'https:', 'mailto:'].includes(new URL(value.link).protocol); } catch { /* Relative URLs require the leading slash above. */ }
      if (!allowed) throw new Error('Invalid drawing link');
    }
    elements.push(canonical(value, budget));
  }
  elements.sort((a, b) => compare(String(a.index ?? ''), String(b.index ?? '')) || compare(a.id, b.id));
  for (const [id, value] of [...filesMap].sort(([a], [b]) => compare(a, b))) {
    if (!key(id) || !object(value) || Object.keys(value).length !== 1 || typeof value.assetId !== 'string' || !guid.test(value.assetId)) throw new Error('Invalid drawing file reference');
    const assetId = value.assetId.toLowerCase();
    files[id] = { assetId };
    assets.add(assetId);
  }
  for (const element of elements) {
    if (element.type === 'image' && element.fileId && !files[element.fileId]) throw new Error('Drawing image has no stored file reference');
  }
  for (const name of scene.keys()) if (name !== 'viewBackgroundColor') throw new Error('Private drawing view state must not be shared');
  const viewBackgroundColor = scene.get('viewBackgroundColor') ?? '#ffffff';
  if (typeof viewBackgroundColor !== 'string' || !/^(#[0-9a-f]{3,8}|transparent)$/i.test(viewBackgroundColor)) throw new Error('Invalid drawing background');
  const sceneJson = JSON.stringify({ elements, files, appState: { viewBackgroundColor } });
  if (Buffer.byteLength(sceneJson, 'utf8') > MAX_DRAWING_BYTES) throw new Error('Drawing exceeds the 1 MiB scene limit');
  return { sceneJson, assetIds: [...assets].sort() };
}

export function drawingStateProjection(state) {
  assertDrawingBytes(state);
  const doc = new Y.Doc();
  try {
    Y.applyUpdate(doc, state);
    return { stateBase64: Buffer.from(state).toString('base64'), fingerprint: fingerprintHash(state, Y.encodeStateVector(doc)), ...projectDrawingDocument(doc) };
  } finally { doc.destroy(); }
}

/** Validate a scratch merge before the live room can apply or broadcast a client update. */
export function validateDrawingUpdate(document, update) {
  assertDrawingBytes(update);
  const candidate = new Y.Doc();
  try {
    Y.applyUpdate(candidate, Y.encodeStateAsUpdate(document));
    Y.applyUpdate(candidate, update);
    assertDrawingBytes(Y.encodeStateAsUpdate(candidate));
    projectDrawingDocument(candidate);
  } finally { candidate.destroy(); }
}

/** Independent copies share no CRDT identity/history and receive new file grants. */
export function cloneDrawingDocument({ stateBase64, sceneJson, assetMap = {} } = {}) {
  if (!object(assetMap)) throw new Error('Invalid drawing asset mapping');
  const mappings = new Map();
  for (const [from, to] of Object.entries(assetMap)) {
    if (!guid.test(from) || typeof to !== 'string' || !guid.test(to)) throw new Error('Invalid drawing asset mapping');
    mappings.set(from.toLowerCase(), to.toLowerCase());
  }
  const source = new Y.Doc(), target = new Y.Doc();
  try {
    if (sceneJson !== undefined && sceneJson !== null) {
      if (stateBase64 != null || typeof sceneJson !== 'string' || Buffer.byteLength(sceneJson, 'utf8') > MAX_DRAWING_BYTES) throw new Error('Invalid drawing scene');
      const scene = JSON.parse(sceneJson);
      if (!object(scene) || !Array.isArray(scene.elements) || !object(scene.files) || !object(scene.appState)) throw new Error('Invalid drawing scene');
      const referenced = new Set();
      for (const element of scene.elements) {
        if (!object(element)) throw new Error('Invalid drawing element');
        if (element.isDeleted) continue;
        if (source.getMap('elements').has(element.id)) throw new Error('Duplicate drawing element');
        source.getMap('elements').set(element.id, element);
        if (element.type === 'image' && element.fileId) referenced.add(element.fileId);
      }
      for (const id of referenced) {
        if (!scene.files[id]) throw new Error('Drawing image has no stored file reference');
        source.getMap('files').set(id, scene.files[id]);
      }
      source.getMap('scene').set('viewBackgroundColor', scene.appState.viewBackgroundColor ?? '#ffffff');
    }
    if (stateBase64 !== undefined && stateBase64 !== null) {
      if (typeof stateBase64 !== 'string' || !stateBase64 || stateBase64.length > Math.ceil(MAX_DRAWING_BYTES * 4 / 3) + 4 || !/^[A-Za-z0-9+/]+={0,2}$/.test(stateBase64)) throw new Error('Invalid drawing state');
      const state = new Uint8Array(Buffer.from(stateBase64, 'base64'));
      assertDrawingBytes(state);
      Y.applyUpdate(source, state);
    }
    const scene = JSON.parse(projectDrawingDocument(source).sceneJson);
    target.transact(() => {
      for (const element of scene.elements) target.getMap('elements').set(element.id, element);
      for (const [id, file] of Object.entries(scene.files)) {
        const assetId = mappings.get(file.assetId);
        if (!assetId) throw new Error('Missing drawing asset mapping');
        target.getMap('files').set(id, { assetId });
      }
      target.getMap('scene').set('viewBackgroundColor', scene.appState.viewBackgroundColor);
    });
    return drawingStateProjection(Y.encodeStateAsUpdate(target));
  } finally { source.destroy(); target.destroy(); }
}
