import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { canonicalFingerprint } from '../src/persistence.mjs';

test('save fingerprints match when client undo retention and server garbage collection encode different bytes', () => {
  const client = new Y.Doc();
  const server = new Y.Doc();
  const text = client.getText('lesson');
  const undo = new Y.UndoManager(text);
  text.insert(0, 'Introduktion til blokke');
  Y.applyUpdate(server, Y.encodeStateAsUpdate(client));
  undo.stopCapturing();
  text.delete(0, 5);
  Y.applyUpdate(server, Y.encodeStateAsUpdate(client));
  assert.equal(text.toString(), server.getText('lesson').toString());
  assert.notDeepEqual(Y.encodeStateAsUpdate(client), Y.encodeStateAsUpdate(server));
  assert.equal(canonicalFingerprint(client), canonicalFingerprint(server));
  undo.destroy();client.destroy();server.destroy();
});

test('deletions change the save fingerprint even when the state vector is unchanged', () => {
  const doc = new Y.Doc();
  const text = doc.getText('lesson');
  text.insert(0, 'ABC');
  const vectorBefore = Y.encodeStateVector(doc);
  const fingerprintBefore = canonicalFingerprint(doc);
  text.delete(1, 1);
  assert.deepEqual(Y.encodeStateVector(doc), vectorBefore);
  assert.notEqual(canonicalFingerprint(doc), fingerprintBefore);
  doc.destroy();
});

test('equivalent documents produce the same fingerprint despite client insertion ordering', () => {
  const first = new Y.Doc();const second = new Y.Doc();
  first.getText('lesson').insert(0, 'ABC');
  second.getText('lesson').insert(0, 'XYZ');
  const a = Y.encodeStateAsUpdate(first);const b = Y.encodeStateAsUpdate(second);
  Y.applyUpdate(first,b);Y.applyUpdate(second,a);
  assert.equal(canonicalFingerprint(first), canonicalFingerprint(second));
  first.destroy();second.destroy();
});
