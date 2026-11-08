import { randomUUID } from 'node:crypto';
import * as Y from 'yjs';
import { fingerprintHash } from './fingerprint.mjs';

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const MAX_BYTES = 16 * 1024 * 1024;

function decode(encoded) {
  if (typeof encoded !== 'string' || !encoded || encoded.length > Math.ceil(MAX_BYTES * 4 / 3) + 4
      || !/^[A-Za-z0-9+/]+={0,2}$/.test(encoded)) throw new Error('Invalid notebook state');
  const bytes = new Uint8Array(Buffer.from(encoded, 'base64'));
  if (!bytes.length || bytes.length > MAX_BYTES) throw new Error('Invalid notebook state');
  const doc = new Y.Doc();
  try { Y.applyUpdate(doc, bytes); return doc; }
  catch { doc.destroy(); throw new Error('Invalid notebook state'); }
}

/** Only integrated, live XML children are visited. Deleted CRDT history is never searchable. */
export function projectNotebookDocument(doc, domain = 'notebook') {
  const text = [], links = new Set(), assets = new Set(), drawings = new Set(), drawingBlocks = {}, references = new Map();
  const reference = (kind, id) => {
    if (typeof kind !== 'string' || !/^[a-z][a-z0-9-]{0,47}$/.test(kind) || typeof id !== 'string' || !id || id.length > 240 || /[\u0000-\u001f\u007f]/.test(id)) return;
    if (guid.test(id)) id = id.toLowerCase();
    references.set(kind + ':' + id, { kind, id });
  };
  let visited = 0;
  const visit = (node, depth = 0, blockId) => {
    if (++visited > 250000 || depth > 256) throw new Error('Notebook document is too complex');
    if (node instanceof Y.XmlText) {
      text.push(node.toDelta().map(item => typeof item.insert === 'string' ? item.insert : '').join(''));
      return;
    }
    if (node instanceof Y.XmlElement) {
      const attrs = node.getAttributes();
      if (node.nodeName === 'blockContainer' && typeof attrs.id === 'string') blockId = attrs.id;
      if (node.nodeName === 'pageLink' || node.nodeName === 'pageMention') {
        if (typeof attrs.pageId === 'string' && guid.test(attrs.pageId)) { links.add(attrs.pageId.toLowerCase()); reference(domain === 'notion' ? 'notion-page' : 'notebook-page', attrs.pageId); }
      }
      if (node.nodeName === 'entityMention' || node.nodeName === 'entityLink') reference(attrs.kind, attrs.id);
      if (node.nodeName === 'studentMention') reference('member', attrs.membershipId);
      if (node.nodeName === 'holdPoll') reference('poll', attrs.pollId);
      if (node.nodeName === 'holdGroup') reference('group', attrs.groupId);
      if (node.nodeName === 'classRoster') {
        reference('hold', attrs.holdId);
        for (const id of String(attrs.membershipIds || '').split(',')) if (guid.test(id)) reference('member', id);
      }
      if (node.nodeName === 'pageMedia' && typeof attrs.assetId === 'string' && guid.test(attrs.assetId)) { assets.add(attrs.assetId.toLowerCase()); reference(domain === 'notion' ? 'notion-asset' : 'notebook-asset', attrs.assetId); }
      if (node.nodeName === 'drawing' && typeof attrs.drawingId === 'string' && guid.test(attrs.drawingId)) {
        const id = attrs.drawingId.toLowerCase();
        drawings.add(id);
        reference('drawing', id);
        if (blockId) {
          if (drawingBlocks[id] && drawingBlocks[id] !== blockId) throw new Error('A drawing cannot belong to multiple blocks');
          drawingBlocks[id] = blockId;
        }
      }
      if (node.nodeName === 'mermaidDiagram' && typeof attrs.source === 'string') text.push(attrs.source);
    }
    if (node instanceof Y.XmlElement || node instanceof Y.XmlFragment) {
      for (const child of node.toArray()) visit(child, depth + 1, blockId);
      if (node instanceof Y.XmlElement && !['pageMention', 'studentMention', 'entityMention', 'customEmoji'].includes(node.nodeName)) text.push('\n');
    }
  };
  visit(doc.getXmlFragment('document-store'));
  return { plainText: text.join('').replace(/\n{3,}/g, '\n\n').trim(), linkIds: [...links].sort(), references: [...references].sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0).map(([, value]) => value), assetIds: [...assets].sort(), drawingIds: [...drawings].sort(), drawingBlocks: Object.fromEntries(Object.entries(drawingBlocks).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0)) };
}

export function notebookStateProjection(state, domain = 'notebook') {
  const doc = new Y.Doc();
  try {
    Y.applyUpdate(doc, state);
    return { stateBase64: Buffer.from(state).toString('base64'), fingerprint: fingerprintHash(state, Y.encodeStateVector(doc)), ...projectNotebookDocument(doc, domain) };
  } finally { doc.destroy(); }
}

function appendDefaultBlock(group, type, content = '', level) {
  const container = new Y.XmlElement('blockContainer');
  container.setAttribute('id', randomUUID());
  const block = new Y.XmlElement(type);
  if (level) block.setAttribute('level', level);
  if (content) block.insert(0, [new Y.XmlText(content)]);
  container.insert(0, [block]);
  group.push([container]);
}

export function defaultNotebookTemplate() {
  const doc = new Y.Doc();
  const group = new Y.XmlElement('blockGroup');
  doc.getXmlFragment('document-store').insert(0, [group]);
  for (const question of ['Hvad arbejdede jeg med?', 'Hvad lærte jeg?', 'Hvad er mit næste skridt?']) {
    appendDefaultBlock(group, 'heading', question, 2);
    appendDefaultBlock(group, 'paragraph');
  }
  return doc;
}

/** Fresh CRDT identities, fresh block IDs and page-local asset grants for an independent student copy. */
export function cloneNotebookDocument({ stateBase64, pageId, assetMap = {}, drawingMap = {}, defaultTemplate = false } = {}) {
  drawingMap ??= {};
  if (typeof pageId !== 'string' || !guid.test(pageId) || !assetMap || typeof assetMap !== 'object' || Array.isArray(assetMap) || !drawingMap || typeof drawingMap !== 'object' || Array.isArray(drawingMap)) throw new Error('Invalid notebook clone request');
  const mappings = new Map();
  for (const [from, to] of Object.entries(assetMap)) {
    if (!guid.test(from) || typeof to !== 'string' || !guid.test(to)) throw new Error('Invalid notebook asset mapping');
    mappings.set(from.toLowerCase(), to.toLowerCase());
  }
  const drawingMappings = new Map();
  for (const [from, to] of Object.entries(drawingMap)) {
    if (!guid.test(from) || typeof to !== 'string' || !guid.test(to) || from.toLowerCase() === to.toLowerCase()) throw new Error('Invalid notebook drawing mapping');
    drawingMappings.set(from.toLowerCase(), to.toLowerCase());
  }
  const source = defaultTemplate ? defaultNotebookTemplate() : stateBase64 ? decode(stateBase64) : new Y.Doc();
  const target = new Y.Doc();
  try {
    const sourceRoot = source.getXmlFragment('document-store');
    let count = 0;
    const copy = (node, depth = 0) => {
      if (++count > 250000 || depth > 256) throw new Error('Notebook document is too complex');
      if (node instanceof Y.XmlText) return node.clone();
      if (!(node instanceof Y.XmlElement)) throw new Error('Unsupported notebook node');
      const result = new Y.XmlElement(node.nodeName);
      const attrs = { ...node.getAttributes() };
      if (node.nodeName === 'blockContainer') attrs.id = randomUUID();
      if (node.nodeName === 'pageMedia') {
        const oldId = attrs.assetId;
        if (oldId) {
          const newId = mappings.get(String(oldId).toLowerCase());
          if (!newId) throw new Error('Missing notebook asset mapping');
          attrs.assetId = newId;
        }
        attrs.pageId = pageId.toLowerCase();
      }
      if (node.nodeName === 'drawing') {
        if (attrs.drawingId) {
          const newId = drawingMappings.get(String(attrs.drawingId).toLowerCase());
          if (!newId) throw new Error('Missing notebook drawing mapping');
          attrs.drawingId = newId;
        }
      }
      for (const [key, value] of Object.entries(attrs)) result.setAttribute(key, value);
      result.insert(0, node.toArray().map(child => copy(child, depth + 1)));
      return result;
    };
    target.getXmlFragment('document-store').insert(0, sourceRoot.toArray().map(node => copy(node)));
    return notebookStateProjection(Y.encodeStateAsUpdate(target));
  } finally { source.destroy(); target.destroy(); }
}
