import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient, documentIdFromName, tokenExpiresAt, MAX_DOCUMENT_BYTES, TOKEN_CLOCK_SKEW_MS } from '../src/api-client.mjs';
import { cloneNotebookDocument } from '../src/notebook-documents.mjs';
import { cloneDrawingDocument, MAX_DRAWING_BYTES } from '../src/drawing-documents.mjs';
import * as Y from 'yjs';
import { fingerprintHash } from '../src/fingerprint.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const name = `notion:${id}`;
const secret = 'unit-test-service-key-not-a-real-secret';
const fingerprint = 'a'.repeat(64);
const jwt = exp => `header.${Buffer.from(JSON.stringify({ exp })).toString('base64url')}.signature`;
const jwtPadded = exp => {
  const payload = Buffer.from(JSON.stringify({ exp })).toString('base64');
  return `header.${payload}.signature`;
};
const validToken = () => jwt(Math.floor(Date.now() / 1000) + 120);
const client = fetchImpl => createApiClient({ apiBaseUrl: 'http://api.test:8080', serviceSecret: secret, fetchImpl });

test('only scoped notion UUID names and unexpired JWT-shaped tokens reach the API', async () => {
  assert.equal(documentIdFromName(name.toUpperCase()), id);
  for (const value of ['room', `notion:${id}/../../state`, 'notion:1', null]) {
    assert.throws(() => documentIdFromName(value));
  }
  assert.throws(() => tokenExpiresAt(jwt(Math.floor(Date.now() / 1000) - TOKEN_CLOCK_SKEW_MS / 1000 - 30)));
  assert.ok(tokenExpiresAt(jwt(Math.floor(Date.now() / 1000) - 30)) > Date.now() - 60_000);
  assert.ok(tokenExpiresAt(jwtPadded(Math.floor(Date.now() / 1000) + 120)) > Date.now());
  assert.ok(tokenExpiresAt(`Bearer ${validToken()}`) > Date.now());
  assert.throws(() => tokenExpiresAt('opaque-token'));
  assert.throws(() => tokenExpiresAt(jwt('9999999999')));
  assert.ok(tokenExpiresAt(validToken()) > Date.now());
  let calls = 0;
  await assert.rejects(client(async () => { calls++; }).authorize('other-room', validToken()));
  assert.equal(calls, 0);
});

test('configuration fails closed without a strong service key or with embedded API credentials', () => {
  assert.throws(() => createApiClient({ apiBaseUrl: 'http://api', serviceSecret: '' }));
  assert.throws(() => createApiClient({ apiBaseUrl: 'http://user:pass@api', serviceSecret: secret }));
  assert.throws(() => createApiClient({ apiBaseUrl: 'file:///tmp/api', serviceSecret: secret }));
});

test('authorization needs an explicit write grant for the exact document', async () => {
  await assert.rejects(client(async () => new Response('', { status: 403 })).authorize(name, validToken()), /denied/);
  await assert.rejects(client(async () => Response.json({ documentId: id, canEdit: false })).authorize(name, validToken()), /denied/);
  await assert.rejects(client(async () => Response.json({ documentId: 'other', canEdit: true })).authorize(name, validToken()), /denied/);
  const token = validToken();
  const api = client(async (url, request) => {
    assert.equal(url.pathname, '/api/notion/collaboration/authorize');
    assert.equal(request.headers.Authorization, `Bearer ${token}`);
    assert.equal(request.headers['X-Notion-Service-Key'], undefined);
    assert.deepEqual(JSON.parse(request.body), { documentName: name });
    return Response.json({ documentId: id, canEdit: true });
  });
  assert.equal((await api.authorize(name, token)).documentId, id);
  assert.equal((await api.authorize(name.toUpperCase(), `Bearer ${token}`)).documentId, id);
});

test('only explicit 204 means an empty document; errors never produce a blank replacement', async () => {
  assert.equal(await client(async () => new Response(null, { status: 204 })).load(name), null);
  for (const status of [401, 404, 500, 503]) {
    await assert.rejects(client(async () => new Response(null, { status })).load(name), /could not be loaded/);
  }
  await assert.rejects(client(async () => { throw new Error('sensitive network details'); }).load(name), /API is unavailable/);
  await assert.rejects(client(async () => new Response(new Uint8Array())).load(name), /Invalid document state/);
});

test('binary persistence uses the service key, preserves bytes and waits for success', async () => {
  const doc = new Y.Doc();
  doc.getText('test').insert(0, 'Durable lesson');
  const bytes = Y.encodeStateAsUpdate(doc);
  const canonical = fingerprintHash(bytes, Y.encodeStateVector(doc));
  doc.destroy();
  const api = client(async (url, request) => {
    assert.equal(url.pathname, `/api/internal/notion/documents/${id}/state`);
    assert.equal(request.headers['X-Notion-Service-Key'], secret);
    assert.equal(request.headers.Authorization, undefined);
    if (request.method === 'PUT') {
      assert.equal(request.headers['Content-Type'], 'application/json');
      assert.deepEqual(JSON.parse(request.body), { stateBase64: Buffer.from(bytes).toString('base64'), fingerprint: canonical, drawingIds: [], drawingBlocks: {} });
      assert.equal(request.headers['X-Notion-State-Fingerprint'], canonical);
      return Response.json({ savedAtUtc: '2026-11-05T12:00:00Z' });
    }
    return new Response(bytes);
  });
  assert.deepEqual(await api.load(name), bytes);
  assert.equal(await api.store(name, bytes, canonical), '2026-11-05T12:00:00Z');
  await assert.rejects(client(async () => new Response(null, { status: 503 })).store(name, bytes, canonical), /could not be saved/);
  await assert.rejects(api.store(name, new Uint8Array(MAX_DOCUMENT_BYTES + 1), fingerprint), /storage limit/);
  await assert.rejects(api.store(name, bytes), /fingerprint/);
  await assert.rejects(api.store(name, bytes, 'invalid'), /fingerprint/);
});

test('drawing readers have separate scoped grants and revision-bearing JSON persistence', async () => {
  const drawingName = `drawing:${id}`, projection = cloneDrawingDocument();
  const bytes = new Uint8Array(Buffer.from(projection.stateBase64, 'base64'));
  const api = client(async (url, request) => {
    if (url.pathname.endsWith('/authorize')) {
      assert.equal(url.pathname, '/api/drawings/collaboration/authorize');
      assert.deepEqual(JSON.parse(request.body), { documentName: drawingName });
      return Response.json({ documentId: id, canRead: true, canEdit: false, accessVersion: 3 });
    }
    assert.equal(url.pathname, `/api/internal/drawings/${id}/state`);
    if (request.method === 'PUT') {
      assert.deepEqual(JSON.parse(request.body), projection);
      return Response.json({ savedAtUtc: '2026-11-07T12:00:00Z', revision: 4, fingerprint: projection.fingerprint, accessVersion: 3 });
    }
    return new Response(bytes);
  });
  const permission = await api.authorize(drawingName, validToken());
  assert.equal(permission.canEdit, false);
  assert.equal(permission.canRead, true);
  assert.deepEqual(await api.load(drawingName), bytes);
  assert.equal((await api.store(drawingName, bytes, projection.fingerprint)).revision, 4);
  await assert.rejects(client(async () => Response.json({ documentId: id, canEdit: true })).authorize(drawingName, validToken()), /denied/);
  await assert.rejects(client(async () => Response.json({ documentId: id, canRead: false, canEdit: false })).authorize(drawingName, validToken()), /denied/);
  await assert.rejects(api.store(drawingName, new Uint8Array(MAX_DRAWING_BYTES + 1), fingerprint), /1 MiB/);
  await assert.rejects(client(async () => new Response(new Uint8Array(MAX_DRAWING_BYTES + 1))).load(drawingName), /storage limit/);
  await assert.rejects(client(async () => Response.json({ savedAtUtc: 'now' })).store(drawingName, bytes, projection.fingerprint), /acknowledgement/);
});

test('notebook rooms route to isolated authorization and atomically projected persistence', async () => {
  const notebookName = `notebook:${id}`;
  const projection = cloneNotebookDocument({ pageId: id, defaultTemplate: true });
  const bytes = new Uint8Array(Buffer.from(projection.stateBase64, 'base64'));
  const api = client(async (url, request) => {
    if (url.pathname.endsWith('/authorize')) {
      assert.equal(url.pathname, '/api/notebooks/collaboration/authorize');
      assert.deepEqual(JSON.parse(request.body), { documentName: notebookName });
      return Response.json({ documentId: id, canEdit: true });
    }
    assert.equal(url.pathname, `/api/internal/notebooks/documents/${id}/state`);
    if (request.method === 'PUT') {
      assert.equal(request.headers['Content-Type'], 'application/json');
      assert.deepEqual(JSON.parse(request.body), projection);
      return Response.json({ savedAtUtc: '2026-11-07T12:00:00Z' });
    }
    return new Response(bytes);
  });
  assert.equal(documentIdFromName(notebookName.toUpperCase()), id);
  assert.equal((await api.authorize(notebookName, validToken())).documentId, id);
  assert.deepEqual(await api.load(notebookName), bytes);
  assert.equal(await api.store(notebookName, bytes, projection.fingerprint), '2026-11-07T12:00:00Z');
  await assert.rejects(api.store(notebookName, bytes, fingerprint), /fingerprint mismatch/);
});
