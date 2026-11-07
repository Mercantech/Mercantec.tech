import assert from 'node:assert/strict';
import test from 'node:test';
import * as Y from 'yjs';
import { applyLocalDrawing, readDrawingScene, drawingChanges, validateDrawingImage, DRAWING_IMAGE_LIMIT } from '../src/drawing-state.mjs';

const shape = (id, extra = {}) => ({ id, type: 'rectangle', index: 'a0', x: 0, y: 0, width: 100, height: 80, isDeleted: false, version: 1, versionNonce: 10, ...extra });
function set(doc, previousElements, elements, rest = {}) { return applyLocalDrawing(doc, { previousElements, elements, ...rest }, 'local'); }
function merge(a, b) { const first = Y.encodeStateAsUpdate(a), second = Y.encodeStateAsUpdate(b); Y.applyUpdate(a, second, 'remote'); Y.applyUpdate(b, first, 'remote'); }

test('two disconnected clients retain independently drawn elements and stable layer ordering', () => {
  const a = new Y.Doc(), b = new Y.Doc();
  try {
    set(a, [], [shape('one', { index: 'a1' })]);
    set(b, [], [shape('two', { index: 'a0' })]);
    merge(a, b);
    assert.deepEqual(readDrawingScene(a), readDrawingScene(b));
    assert.deepEqual(readDrawingScene(a).elements.map(item => item.id), ['two', 'one']);
  } finally { a.destroy(); b.destroy(); }
});

test('same-element concurrent updates converge without applying a second version conflict rule', () => {
  const a = new Y.Doc(), b = new Y.Doc();
  try {
    const original = shape('one'); set(a, [], [original]); merge(a, b);
    set(a, [original], [shape('one', { x: 10, version: 2, versionNonce: 20 })]);
    set(b, [original], [shape('one', { x: 20, version: 2, versionNonce: 5 })]);
    merge(a, b);
    assert.deepEqual(readDrawingScene(a), readDrawingScene(b));
    assert.equal(readDrawingScene(a).elements.length, 1);
  } finally { a.destroy(); b.destroy(); }
});

test('deletions stay as tombstones after reconnect and a local undo does not remove a remote element', () => {
  const a = new Y.Doc(), b = new Y.Doc();
  try {
    const original = shape('one'); set(a, [], [original]); merge(a, b);
    const deleted = shape('one', { isDeleted: true, version: 2 }); set(a, [original], [deleted]);
    set(b, [original], [original, shape('remote', { index: 'a1' })]); merge(a, b);
    assert.equal(readDrawingScene(a).elements.find(item => item.id === 'one').isDeleted, true);
    const current = readDrawingScene(a).elements;
    set(a, current, current.map(item => item.id === 'one' ? shape('one', { version: 3 }) : item)); merge(a, b);
    assert.equal(readDrawingScene(b).elements.find(item => item.id === 'one').isDeleted, false);
    assert.ok(readDrawingScene(b).elements.some(item => item.id === 'remote'));
  } finally { a.destroy(); b.destroy(); }
});

test('unchanged remote records are not rewritten, and image records wait for authorized file references', () => {
  const original = shape('one');
  assert.deepEqual(drawingChanges([original], [{ ...original }]), []);
  const image = shape('image', { type: 'image', fileId: 'sha256-file', status: 'saved' });
  assert.deepEqual(drawingChanges([], [image]), []);
  const doc = new Y.Doc();
  try {
    set(doc, [], [image], { fileReferences: { 'sha256-file': { assetId: 'asset-id' } } });
    assert.equal(readDrawingScene(doc).elements.length, 1);
    assert.deepEqual(readDrawingScene(doc).files, { 'sha256-file': { assetId: 'asset-id' } });
  } finally { doc.destroy(); }
});

test('SDK restoration of empty bindings creates no Yjs operation and preserves canonical records', () => {
  const doc = new Y.Doc();
  try {
    const original = [shape('null', { boundElements: null }), shape('absent', { index: 'a1' })];
    set(doc, [], original);
    const state = Y.encodeStateAsUpdate(doc), vector = Y.encodeStateVector(doc);
    const restored = original.map(element => ({ ...element, boundElements: [] }));
    let updates = 0; doc.on('update', () => updates++);
    assert.deepEqual(drawingChanges(original, restored), []);
    assert.equal(set(doc, original, restored), false);
    assert.equal(updates, 0);
    assert.deepEqual(Y.encodeStateVector(doc), vector);
    assert.deepEqual(Y.encodeStateAsUpdate(doc), state);
    assert.deepEqual(readDrawingScene(doc).elements, original);
    assert.equal(drawingChanges(original, restored.map(element => element.id === 'null' ? { ...element, boundElements: [{ id: 'text', type: 'text' }] } : element)).length, 1);
  } finally { doc.destroy(); }
});

test('over-budget scene is refused before the connected document changes and file validation is bounded', () => {
  const doc = new Y.Doc();
  try {
    const original = shape('one'); set(doc, [], [original]);
    const before = Y.encodeStateAsUpdate(doc);
    assert.throws(() => set(doc, [original], [shape('one', { text: 'x'.repeat(1024 * 1024) })]), /for stor/);
    assert.deepEqual(Y.encodeStateAsUpdate(doc), before);
    assert.equal(validateDrawingImage({ size: DRAWING_IMAGE_LIMIT + 1, type: 'image/png' }), 'Billeder i tegninger må højst være 3 MiB.');
    assert.equal(validateDrawingImage({ size: 10, type: 'text/html' }), 'Vælg PNG, JPEG, WebP eller GIF.');
    assert.equal(validateDrawingImage({ size: 10, type: 'image/png' }), '');
  } finally { doc.destroy(); }
});
