import * as Y from 'yjs';

export const DRAWING_STATE_LIMIT = 1024 * 1024;
export const DRAWING_IMAGE_LIMIT = 3 * 1024 * 1024;

export function stableDrawingJson(value) {
  if (Array.isArray(value)) return '[' + value.map(stableDrawingJson).join(',') + ']';
  if (value && typeof value === 'object') return '{' + Object.keys(value).filter(key => value[key] !== undefined).sort().map(key => JSON.stringify(key) + ':' + stableDrawingJson(value[key])).join(',') + '}';
  return JSON.stringify(value);
}

/** SDK restore treats absent/null bindings as an empty list; this is not a shared edit. */
export function drawingElementComparisonKey(element) {
  return stableDrawingJson(element && typeof element === 'object' ? { ...element, boundElements: element.boundElements ?? [] } : element);
}

export function orderedDrawingElements(elements) {
  return [...elements].sort((a, b) => {
    const first = typeof a.index === 'string' ? a.index : '', second = typeof b.index === 'string' ? b.index : '';
    return first < second ? -1 : first > second ? 1 : a.id < b.id ? -1 : a.id > b.id ? 1 : 0;
  });
}

export function readDrawingScene(doc) {
  const elements = orderedDrawingElements([...doc.getMap('elements').values()]);
  const files = Object.fromEntries([...doc.getMap('files')].sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0));
  return { elements, files, appState: { viewBackgroundColor: doc.getMap('scene').get('viewBackgroundColor') || '#ffffff' } };
}

/** Only a local change is written: remote records never become accidental local overwrites. */
export function drawingChanges(previous, current, fileReferences = {}) {
  const before = new Map(previous.map(item => [item.id, drawingElementComparisonKey(item)]));
  const changes = current.filter(item => before.get(item.id) !== drawingElementComparisonKey(item)).map(item => JSON.parse(JSON.stringify(item)));
  // A new image is not published until its page-authorized binary has finished uploading.
  return changes.filter(item => item.type !== 'image' || item.isDeleted || (item.fileId && fileReferences[item.fileId]));
}

/** Validate an encoded candidate before mutating the connected document. */
export function applyLocalDrawing(doc, { previousElements, elements, fileReferences = {}, viewBackgroundColor, previousBackground }, origin) {
  const changes = drawingChanges(previousElements, elements, fileReferences);
  const newFiles = Object.entries(fileReferences).filter(([id, value]) => stableDrawingJson(doc.getMap('files').get(id)) !== stableDrawingJson(value));
  const backgroundChanged = typeof viewBackgroundColor === 'string' && viewBackgroundColor !== previousBackground;
  if (!changes.length && !newFiles.length && !backgroundChanged) return false;
  const apply = target => target.transact(() => {
    for (const [id, value] of newFiles) target.getMap('files').set(id, value);
    for (const item of changes) target.getMap('elements').set(item.id, item);
    if (backgroundChanged) target.getMap('scene').set('viewBackgroundColor', viewBackgroundColor);
  }, origin);
  const candidate = new Y.Doc();
  try {
    Y.applyUpdate(candidate, Y.encodeStateAsUpdate(doc));
    apply(candidate);
    if (Y.encodeStateAsUpdate(candidate).byteLength > DRAWING_STATE_LIMIT) throw new Error('Tegningen er for stor. Eksportér den, eller fjern noget indhold.');
  } finally { candidate.destroy(); }
  apply(doc);
  return true;
}

export function drawingFileReferences(scene) {
  return new Set(scene.elements.filter(item => item.type === 'image' && !item.isDeleted && item.fileId).map(item => item.fileId));
}

export function validateDrawingImage(file) {
  if (!file || !Number.isSafeInteger(file.size) || file.size <= 0) return 'Vælg et billede med indhold.';
  if (file.size > DRAWING_IMAGE_LIMIT) return 'Billeder i tegninger må højst være 3 MiB.';
  if (!['image/png', 'image/jpeg', 'image/webp', 'image/gif'].includes(file.type)) return 'Vælg PNG, JPEG, WebP eller GIF.';
  return '';
}
