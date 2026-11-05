import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient, documentIdFromName, tokenExpiresAt, MAX_DOCUMENT_BYTES } from '../src/api-client.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const name = `notion:${id}`;
const secret = 'unit-test-service-key-not-a-real-secret';
const jwt = exp => `header.${Buffer.from(JSON.stringify({ exp })).toString('base64url')}.signature`;
const validToken = () => jwt(Math.floor(Date.now() / 1000) + 120);
const client = fetchImpl => createApiClient({ apiBaseUrl: 'http://api.test:8080', serviceSecret: secret, fetchImpl });

test('only scoped notion UUID names and unexpired JWT-shaped tokens reach the API', async () => {
  assert.equal(documentIdFromName(name.toUpperCase()), id);
  for (const value of ['room', `notion:${id}/../../state`, 'notion:1', null]) {
    assert.throws(() => documentIdFromName(value));
  }
  assert.throws(() => tokenExpiresAt(jwt(1)));
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
  const bytes = new Uint8Array([0, 1, 2, 255]);
  const api = client(async (url, request) => {
    assert.equal(url.pathname, `/api/internal/notion/documents/${id}/state`);
    assert.equal(request.headers['X-Notion-Service-Key'], secret);
    assert.equal(request.headers.Authorization, undefined);
    if (request.method === 'PUT') {
      assert.deepEqual(request.body, bytes);
      return Response.json({ savedAtUtc: '2026-11-05T12:00:00Z' });
    }
    return new Response(bytes);
  });
  assert.deepEqual(await api.load(name), bytes);
  assert.equal(await api.store(name, bytes), '2026-11-05T12:00:00Z');
  await assert.rejects(client(async () => new Response(null, { status: 503 })).store(name, bytes), /could not be saved/);
  await assert.rejects(api.store(name, new Uint8Array(MAX_DOCUMENT_BYTES + 1)), /storage limit/);
});
