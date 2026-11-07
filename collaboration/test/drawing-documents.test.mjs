import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { cloneDrawingDocument, drawingStateProjection, projectDrawingDocument, validateDrawingUpdate, MAX_DRAWING_BYTES } from '../src/drawing-documents.mjs';

const oldAsset = '22222222-2222-4222-8222-222222222222', newAsset = '33333333-3333-4333-8333-333333333333';
const encode = doc => Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64');
const decode = state => { const doc = new Y.Doc(); Y.applyUpdate(doc, Buffer.from(state, 'base64')); return doc; };
const rectangle = (id, extra = {}) => ({ id, type: 'rectangle', x: 0, y: 0, width: 30, height: 20, index: 'a0', isDeleted: false, ...extra });

test('drawing projection is canonical, keeps element tombstones and excludes private view state', () => {
  const a = new Y.Doc(), b = new Y.Doc();
  const first = rectangle('first', { index: 'aZ', isDeleted: true }), second = rectangle('second', { index: 'aa' });
  a.getMap('elements').set('second', second); a.getMap('elements').set('first', first);
  b.getMap('elements').set('first', Object.fromEntries(Object.entries(first).reverse())); b.getMap('elements').set('second', second);
  assert.deepEqual(projectDrawingDocument(a), projectDrawingDocument(b));
  const scene = JSON.parse(projectDrawingDocument(a).sceneJson);
  assert.deepEqual(scene.elements.map(item => item.id), ['first', 'second']);
  assert.equal(scene.elements[0].isDeleted, true);
  a.getMap('scene').set('zoom', 2);
  assert.throws(() => projectDrawingDocument(a), /Private/);
  a.destroy(); b.destroy();
});

test('drawing file values are only stored asset grants and dangling SDK images are rejected', () => {
  const doc = new Y.Doc();
  doc.getMap('elements').set('image', { id: 'image', type: 'image', fileId: 'sdk-hash' });
  assert.throws(() => projectDrawingDocument(doc), /no stored file/);
  doc.getMap('files').set('sdk-hash', { assetId: oldAsset });
  assert.deepEqual(projectDrawingDocument(doc).assetIds, [oldAsset]);
  doc.getMap('files').set('sdk-hash', { assetId: oldAsset, dataURL: 'data:image/png;base64,aA==' });
  assert.throws(() => projectDrawingDocument(doc), /file reference/);
  doc.getMap('files').delete('sdk-hash'); doc.getMap('elements').delete('image');
  doc.getMap('elements').set('a', rectangle('a', { customData: { dataURL: 'data:image/png;base64,aA==' } }));
  assert.throws(() => projectDrawingDocument(doc), /binaries/);
  doc.destroy();
});

test('scratch update preflight rejects raw and aggregate limits without mutating the live room', () => {
  const live = new Y.Doc(), client = new Y.Doc();
  client.getMap('elements').set('a', rectangle('a', { text: 'a'.repeat(650000) }));
  const initial = Y.encodeStateAsUpdate(client);
  validateDrawingUpdate(live, initial);
  assert.equal(live.getMap('elements').size, 0);
  Y.applyUpdate(live, initial);
  const vector = Y.encodeStateVector(client);
  client.getMap('elements').set('b', rectangle('b', { text: 'b'.repeat(650000) }));
  const update = Y.encodeStateAsUpdate(client, vector);
  assert.ok(update.length < MAX_DRAWING_BYTES);
  assert.throws(() => validateDrawingUpdate(live, update), /1 MiB/);
  assert.equal(live.getMap('elements').has('b'), false);
  assert.throws(() => validateDrawingUpdate(live, new Uint8Array(MAX_DRAWING_BYTES + 1)), /1 MiB/);
  const invalid = new Y.Doc(); invalid.getText('binary').insert(0, 'hidden');
  assert.throws(() => validateDrawingUpdate(live, Y.encodeStateAsUpdate(invalid)), /Unknown/);
  live.destroy(); client.destroy(); invalid.destroy();
});

test('drawing template clones isolate CRDT identities/history and remap only file grants', () => {
  const source = new Y.Doc({ gc: false });
  source.getMap('elements').set('removed', rectangle('removed', { text: 'SECRET HISTORY' }));
  source.getMap('elements').delete('removed');
  source.getMap('elements').set('image', { id: 'image', type: 'image', fileId: 'hash', isDeleted: false });
  source.getMap('files').set('hash', { assetId: oldAsset });
  const request = { stateBase64: encode(source), assetMap: { [oldAsset]: newAsset } };
  const first = cloneDrawingDocument(request), second = cloneDrawingDocument(request);
  const a = decode(first.stateBase64), b = decode(second.stateBase64);
  assert.notDeepEqual(Y.encodeStateVector(a), Y.encodeStateVector(b));
  assert.equal(Buffer.from(first.stateBase64, 'base64').includes(Buffer.from('SECRET HISTORY')), false);
  assert.deepEqual(first.assetIds, [newAsset]);
  a.getMap('elements').set('unique', rectangle('unique'));
  assert.equal(b.getMap('elements').has('unique'), false);
  assert.equal(source.getMap('files').get('hash').assetId, oldAsset);
  assert.throws(() => cloneDrawingDocument({ ...request, assetMap: {} }), /Missing/);
  assert.deepEqual(drawingStateProjection(Buffer.from(second.stateBase64, 'base64')), second);
  source.destroy(); a.destroy(); b.destroy();
});

test('snapshot scene clone excludes deleted elements and unreferenced assets', () => {
  const sceneJson = JSON.stringify({ elements: [rectangle('removed', { isDeleted: true, text: 'PRIVATE' }), rectangle('live')], files: { unused: { assetId: oldAsset } }, appState: { viewBackgroundColor: '#fefefe', zoom: 2 } });
  const result = cloneDrawingDocument({ sceneJson });
  const scene = JSON.parse(result.sceneJson);
  assert.deepEqual(scene.elements.map(item => item.id), ['live']);
  assert.deepEqual(scene.files, {});
  assert.deepEqual(scene.appState, { viewBackgroundColor: '#fefefe' });
  assert.equal(Buffer.from(result.stateBase64, 'base64').includes(Buffer.from('PRIVATE')), false);
  assert.deepEqual(result.assetIds, []);
});

test('nullable transport fields, root-map types and JSON serialization limits are guarded', () => {
  const blank = cloneDrawingDocument({ stateBase64: null, sceneJson: null, assetMap: {} });
  assert.equal(JSON.parse(blank.sceneJson).elements.length, 0);
  const wrong = new Y.Doc(); wrong.getText('elements').insert(0, 'hidden-root-text');
  assert.throws(() => drawingStateProjection(Y.encodeStateAsUpdate(wrong)), /roots must be maps/);
  const quoted = new Y.Doc(); quoted.getMap('elements').set('text', rectangle('text', { text: '"'.repeat(600000) }));
  assert.ok(Y.encodeStateAsUpdate(quoted).length < MAX_DRAWING_BYTES);
  assert.throws(() => drawingStateProjection(Y.encodeStateAsUpdate(quoted)), /scene limit/);
  wrong.destroy(); quoted.destroy();
});
