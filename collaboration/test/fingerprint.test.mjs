import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { fingerprintHash } from '../src/fingerprint.mjs';

const hash = doc => fingerprintHash(Y.encodeStateAsUpdate(doc), Y.encodeStateVector(doc));

test('save fingerprint survives UndoManager retained deletes versus server garbage collection', () => {
  const client = new Y.Doc();
  const server = new Y.Doc();
  const text = client.getText('body');
  const undo = new Y.UndoManager(text);
  text.insert(0, 'Keep this and delete the rest');
  Y.applyUpdate(server, Y.encodeStateAsUpdate(client));
  text.delete(9, text.length - 9);
  Y.applyUpdate(server, Y.encodeStateAsUpdate(client));
  assert.notDeepEqual(Y.encodeStateAsUpdate(client), Y.encodeStateAsUpdate(server));
  assert.equal(hash(client), hash(server));
  undo.undo();
  assert.notEqual(hash(client), hash(server));
  Y.applyUpdate(server, Y.encodeStateAsUpdate(client));
  assert.equal(hash(client), hash(server));
  undo.destroy(); client.destroy(); server.destroy();
});

test('a new delete changes the fingerprint even if the state vector has not advanced', () => {
  const doc = new Y.Doc();
  doc.getText('body').insert(0, 'delete me');
  const vector = Y.encodeStateVector(doc);
  const before = hash(doc);
  doc.getText('body').delete(0, 3);
  assert.deepEqual(Y.encodeStateVector(doc), vector);
  assert.notEqual(hash(doc), before);
  doc.destroy();
});

test('concurrent clients and different update arrival orders produce the same fingerprint', () => {
  const a = new Y.Doc(); const b = new Y.Doc();
  const server = new Y.Doc();
  a.getText('body').insert(0, 'a'); b.getText('body').insert(0, 'b');
  const aUpdate = Y.encodeStateAsUpdate(a); const bUpdate = Y.encodeStateAsUpdate(b);
  Y.applyUpdate(a, bUpdate); Y.applyUpdate(b, aUpdate);
  Y.applyUpdate(server, bUpdate); Y.applyUpdate(server, aUpdate);
  assert.equal(hash(a), hash(b));
  assert.equal(hash(a), hash(server));
  a.destroy(); b.destroy(); server.destroy();
});
