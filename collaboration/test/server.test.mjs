import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { createHash } from 'node:crypto';
import * as Y from 'yjs';
import { WebSocket } from 'ws';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import { OutgoingMessage } from '@hocuspocus/server';
import { createCollaborationServer } from '../src/server.mjs';
import { fingerprintHash } from '../src/fingerprint.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const documentName = `notion:${id}`;
const otherName = 'notion:aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const secret = 'integration-test-service-key-not-production';
const token = (sub, expiresIn = 120) => `header.${Buffer.from(JSON.stringify({ sub, exp: Math.floor(Date.now() / 1000) + expiresIn })).toString('base64url')}.signature`;
const waitFor = async (check, message = 'condition', timeout = 5000) => {
  const until = Date.now() + timeout;
  while (Date.now() < until) {
    if (await check()) return;
    await new Promise(resolve => setTimeout(resolve, 20));
  }
  assert.fail(`Timed out waiting for ${message}`);
};

async function fixture(t, { reauthorizeMs = 15000 } = {}) {
  const state = { binary: null, fingerprint: null, loads: 0, saves: 0, storeAttempts: 0, authorizations: 0, failLoad: false, failStore: false, saveGate: null };
  const grants = new Map();
  const http = createServer(async (request, response) => {
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);
    const body = Buffer.concat(chunks);
    if (request.url === '/api/notion/collaboration/authorize') {
      state.authorizations++;
      const accessToken = request.headers.authorization?.replace(/^Bearer /, '');
      const requested = JSON.parse(body.toString()).documentName;
      if (!grants.get(accessToken)?.has(requested)) { response.writeHead(403).end(); return; }
      response.setHeader('Content-Type', 'application/json');
      response.end(JSON.stringify({ documentId: requested.slice(7), canEdit: true }));
      return;
    }
    if (request.headers['x-notion-service-key'] !== secret) { response.writeHead(401).end(); return; }
    if (request.url !== `/api/internal/notion/documents/${id}/state`) { response.writeHead(404).end(); return; }
    if (request.method === 'GET') {
      state.loads++;
      if (state.failLoad) { response.writeHead(503).end(); return; }
      response.writeHead(state.binary ? 200 : 204, { 'Content-Type': 'application/octet-stream' });
      response.end(state.binary);
    } else if (request.method === 'PUT') {
      state.storeAttempts++;
      const fingerprint = request.headers['x-notion-state-fingerprint'];
      if (fingerprint !== fingerprintHash(body, Y.encodeStateVectorFromUpdate(body))) {
        response.writeHead(400).end(); return;
      }
      if (state.saveGate) await state.saveGate;
      if (state.failStore) { response.writeHead(503).end(); return; }
      state.binary = body;
      state.fingerprint = fingerprint;
      state.saves++;
      response.setHeader('Content-Type', 'application/json');
      response.end(JSON.stringify({ savedAtUtc: new Date().toISOString() }));
    } else response.writeHead(405).end();
  });
  http.listen(0, '127.0.0.1');
  await once(http, 'listening');
  let server;
  const servers = [];
  const clients = [];
  const start = async () => {
    server = createCollaborationServer({
      apiBaseUrl: `http://127.0.0.1:${http.address().port}`, serviceSecret: secret,
      port: 0, address: '127.0.0.1', allowedOrigins: ['https://mercantec.tech'],
      reauthorizeMs, debounceMs: 30, maxDebounceMs: 80,
    });
    servers.push(server);
    await server.listen();
    return server;
  };
  await start();
  t.after(async () => {
    for (const client of clients) client.destroy();
    for (const instance of servers) await instance.destroy();
    http.closeAllConnections();
    await new Promise(resolve => http.close(resolve));
  });

  const connect = (accessToken, name = documentName, schemaVersion = '5') => {
    const doc = new Y.Doc();
    const messages = [];
    let denied = false;
    let denialReason;
    const websocket = new HocuspocusProviderWebsocket({
      url: `ws://127.0.0.1:${server.address.port}/collaboration${schemaVersion === null ? '' : '?schemaVersion=' + encodeURIComponent(schemaVersion)}`,
      WebSocketPolyfill: WebSocket, autoConnect: false, maxAttempts: 1,
    });
    const provider = new HocuspocusProvider({
      name, token: accessToken, document: doc, websocketProvider: websocket,
      onAuthenticationFailed: ({ reason }) => { denied = true; denialReason = reason; },
      onStateless: ({ payload }) => messages.push(JSON.parse(payload)),
    });
    provider.attach();
    const result = {
      doc, provider, websocket, messages,
      get denied() { return denied; },
      get denialReason() { return denialReason; },
      destroy() { provider.destroy(); websocket.destroy(); doc.destroy(); },
    };
    clients.push(result);
    websocket.connect().catch(() => {});
    return result;
  };
  return { state, grants, connect, start, get server() { return server; } };
}

test('two real providers merge concurrent edits, acknowledge durable storage and reload binary after restart', async t => {
  const f = await fixture(t);
  const aToken = token('teacher-a'); const bToken = token('teacher-b');
  f.grants.set(aToken, new Set([documentName])); f.grants.set(bToken, new Set([documentName]));
  const a = f.connect(aToken); const b = f.connect(bToken);
  await waitFor(() => a.provider.isSynced && b.provider.isSynced, 'both providers synchronized');
  a.doc.getText('test').insert(0, 'Hej ');
  b.doc.getText('test').insert(0, 'klasse');
  await waitFor(() => a.doc.getText('test').toString() === b.doc.getText('test').toString() && a.doc.getText('test').length === 10, 'concurrent edit merge');
  await waitFor(() => a.messages.some(m => m.type === 'persisted'), 'durable acknowledgement');
  const acknowledgement = a.messages.filter(m => m.type === 'persisted').at(-1);
  assert.equal(acknowledgement.stateHash, createHash('sha256').update(f.state.binary).digest('hex'));
  assert.ok(acknowledgement.stateVector);
  assert.equal(acknowledgement.fingerprintHash, fingerprintHash(Y.encodeStateAsUpdate(a.doc), Y.encodeStateVector(a.doc)));
  assert.equal(f.state.fingerprint, acknowledgement.fingerprintHash);
  const expected = a.doc.getText('test').toString();
  a.destroy(); b.destroy();
  await f.server.destroy();
  await f.start();
  const reopened = f.connect(aToken);
  await waitFor(() => reopened.provider.isSynced && reopened.doc.getText('test').toString() === expected, 'reload after process restart');
  assert.ok(f.state.loads >= 2);
});

test('missing or incompatible editor schemas are rejected before authorization and document loading', async t => {
  const f = await fixture(t);
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  for (const version of [null, '1', '2', '3', '4', '999']) {
    const editor = f.connect(accessToken, documentName, version);
    await waitFor(() => editor.denied, 'incompatible editor rejected');
    assert.match(editor.denialReason, /editor_version_mismatch/);
  }
  assert.equal(f.state.authorizations, 0);
  assert.equal(f.state.loads, 0);
  assert.equal(f.state.saves, 0);
});

test('loading a legacy persisted document stamps its canonical publication fingerprint before synchronization', async t => {
  const f = await fixture(t);
  const legacy = new Y.Doc();
  legacy.getText('test').insert(0, 'Existing lesson');
  f.state.binary = Buffer.from(Y.encodeStateAsUpdate(legacy));
  const expected = fingerprintHash(f.state.binary, Y.encodeStateVector(legacy));
  legacy.destroy();
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const editor = f.connect(accessToken);
  await waitFor(() => editor.provider.isSynced, 'legacy content synchronized');
  assert.equal(editor.doc.getText('test').toString(), 'Existing lesson');
  assert.equal(f.state.fingerprint, expected);
  assert.ok(f.state.saves > 0);
});

test('wrong-hold token is denied before state loads or queued edits reach the document', async t => {
  const f = await fixture(t);
  const unauthorized = token('other-hold-teacher');
  f.grants.set(unauthorized, new Set([otherName]));
  const outsider = f.connect(unauthorized);
  outsider.doc.getText('test').insert(0, 'forbidden');
  await waitFor(() => outsider.denied, 'access denied');
  assert.equal(f.state.loads, 0);
  assert.equal(f.state.saves, 0);
});

test('lost hold access closes idle sessions and blocks further edits', async t => {
  const f = await fixture(t, { reauthorizeMs: 80 });
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const editor = f.connect(accessToken);
  await waitFor(() => editor.provider.isSynced, 'initial sync');
  f.grants.delete(accessToken);
  await waitFor(() => f.state.authorizations > 1 && !f.server.hocuspocus.documents.get(documentName)?.getConnectionsCount(), 'revoked session closed');
  editor.doc.getText('test').insert(0, 'must stay local');
  await new Promise(resolve => setTimeout(resolve, 150));
  const restored = new Y.Doc();
  if (f.state.binary) Y.applyUpdate(restored, f.state.binary);
  assert.equal(restored.getText('test').toString(), '');
  restored.destroy();
});

test('expired token is rejected even if the stub API would grant it', async t => {
  const f = await fixture(t);
  // Beyond the 5-minute authorize skew that mirrors ASP.NET JWT validation.
  const expired = token('teacher', -400);
  f.grants.set(expired, new Set([documentName]));
  const editor = f.connect(expired);
  await waitFor(() => editor.denied, 'expired token denied');
  assert.equal(f.state.authorizations, 0);
  assert.equal(f.state.loads, 0);
});

test('a token within clock skew still authorizes against the API', async t => {
  const f = await fixture(t);
  const skewed = token('teacher', -30);
  f.grants.set(skewed, new Set([documentName]));
  const editor = f.connect(skewed);
  await waitFor(() => editor.provider.isSynced, 'skewed token synced');
  assert.ok(f.state.authorizations > 0);
  assert.equal(editor.denied, false);
});

test('a failed initial load does not create or save a blank replacement', async t => {
  const f = await fixture(t);
  f.state.failLoad = true;
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const editor = f.connect(accessToken);
  editor.doc.getText('test').insert(0, 'must not replace failed load');
  await waitFor(() => f.state.loads > 0, 'failed storage load attempted');
  await new Promise(resolve => setTimeout(resolve, 150));
  assert.equal(editor.provider.isSynced, false);
  assert.equal(f.state.saves, 0);
});

test('client cannot forge server save acknowledgements through BroadcastStateless', async t => {
  const f = await fixture(t);
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const sender = f.connect(accessToken); const observer = f.connect(accessToken);
  await waitFor(() => sender.provider.isSynced && observer.provider.isSynced, 'clients synced');
  sender.websocket.webSocket.send(new OutgoingMessage(documentName).writeBroadcastStateless(JSON.stringify({ type: 'persisted', forged: true })).toUint8Array());
  await waitFor(() => f.server.hocuspocus.documents.get(documentName)?.getConnectionsCount() === 1, 'forged ack sender disconnected');
  assert.equal(observer.messages.some(m => m.forged), false);
});

test('slow storage never acknowledges an outdated snapshot when a newer delete arrives', async t => {
  const f = await fixture(t);
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const editor = f.connect(accessToken);
  await waitFor(() => editor.provider.isSynced, 'initial sync');
  editor.doc.getText('test').insert(0, 'base');
  await waitFor(() => editor.messages.some(m => m.type === 'persisted'), 'baseline saved');
  editor.messages.length = 0;
  let release;
  f.state.saveGate = new Promise(resolve => { release = resolve; });
  t.after(() => release());
  const previousAttempts = f.state.storeAttempts;
  editor.doc.getText('test').insert(4, 'pending');
  await waitFor(() => f.state.storeAttempts > previousAttempts, 'slow write started');
  const oldHash = createHash('sha256').update(Y.encodeStateAsUpdate(f.server.hocuspocus.documents.get(documentName))).digest('hex');
  editor.doc.getText('test').delete(4, 7);
  await waitFor(() => f.server.hocuspocus.documents.get(documentName).getText('test').toString() === 'base', 'newer delete received');
  f.state.saveGate = null;
  release();
  const expectedHash = createHash('sha256').update(Y.encodeStateAsUpdate(editor.doc)).digest('hex');
  await waitFor(() => editor.messages.some(m => m.type === 'persisted' && m.stateHash === expectedHash), 'newer state persisted');
  assert.equal(editor.messages.some(m => m.type === 'persisted' && m.stateHash === oldHash), false);
});

test('a real UndoManager client can confirm saved state after server garbage collection', async t => {
  const f = await fixture(t);
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const editor = f.connect(accessToken);
  const undo = new Y.UndoManager(editor.doc.getText('test'));
  t.after(() => undo.destroy());
  await waitFor(() => editor.provider.isSynced, 'initial sync');
  editor.doc.getText('test').insert(0, 'keep and delete');
  await waitFor(() => editor.messages.some(m => m.type === 'persisted'), 'initial content saved');
  editor.messages.length = 0;
  editor.doc.getText('test').delete(4, 11);
  const expected = fingerprintHash(Y.encodeStateAsUpdate(editor.doc), Y.encodeStateVector(editor.doc));
  await waitFor(() => editor.messages.some(m => m.type === 'persisted' && m.fingerprintHash === expected), 'deleted state confirmed saved');
  assert.notDeepEqual(Y.encodeStateAsUpdate(editor.doc), f.state.binary);
});

test('failed persistence emits an unsaved error and only successful storage can acknowledge it', async t => {
  const f = await fixture(t);
  const accessToken = token('teacher');
  f.grants.set(accessToken, new Set([documentName]));
  const editor = f.connect(accessToken);
  await waitFor(() => editor.provider.isSynced, 'initial sync');
  f.state.failStore = true;
  editor.doc.getText('test').insert(0, 'unsaved');
  await waitFor(() => editor.messages.some(m => m.type === 'persistenceError'), 'unsaved error');
  assert.equal(f.state.saves, 0);
  assert.equal(editor.messages.some(m => m.type === 'persisted'), false);
  f.state.failStore = false;
  editor.doc.getText('test').insert(7, ' recovered');
  await waitFor(() => editor.messages.some(m => m.type === 'persisted'), 'successful recovery saved');
  const restored = new Y.Doc();
  Y.applyUpdate(restored, f.state.binary);
  assert.equal(restored.getText('test').toString(), 'unsaved recovered');
  restored.destroy();
});

test('health endpoint works and unapproved browser origins cannot upgrade', async t => {
  const f = await fixture(t);
  const base = `http://127.0.0.1:${f.server.address.port}`;
  assert.equal((await fetch(`${base}/health`)).status, 200);
  assert.equal((await fetch(`${base}/unknown`)).status, 404);
  const ws = new WebSocket(`ws://127.0.0.1:${f.server.address.port}/collaboration`, { origin: 'https://untrusted.example' });
  const [error] = await once(ws, 'error');
  assert.match(error.message, /403/);
  ws.terminate();
});
