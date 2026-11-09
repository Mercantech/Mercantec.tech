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
import { collaborationUrl } from '../../editor/src/collaboration-url.mjs';

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
  const state = { binary: null, fingerprint: null, loads: 0, saves: 0, storeAttempts: 0, authorizations: 0, failLoad: false, failStore: false, saveGate: null, revision: 0 };
  const grants = new Map();
  const readOnlyTokens = new Set();
  const http = createServer(async (request, response) => {
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);
    const body = Buffer.concat(chunks);
    if (['/api/notion/collaboration/authorize', '/api/notebooks/collaboration/authorize', '/api/drawings/collaboration/authorize'].includes(request.url)) {
      state.authorizations++;
      const accessToken = request.headers.authorization?.replace(/^Bearer /, '');
      const requested = JSON.parse(body.toString()).documentName;
      if (!grants.get(accessToken)?.has(requested)) { response.writeHead(403).end(); return; }
      response.setHeader('Content-Type', 'application/json');
      response.end(JSON.stringify({ documentId: requested.slice(requested.indexOf(':') + 1), canRead: true, canEdit: !readOnlyTokens.has(accessToken), accessVersion: 1 }));
      return;
    }
    if (request.headers['x-notion-service-key'] !== secret) { response.writeHead(401).end(); return; }
    const notebook = request.url === `/api/internal/notebooks/documents/${id}/state`;
    const drawing = request.url === `/api/internal/drawings/${id}/state`;
    if (!notebook && !drawing && request.url !== `/api/internal/notion/documents/${id}/state`) { response.writeHead(404).end(); return; }
    if (request.method === 'GET') {
      state.loads++;
      if (state.failLoad) { response.writeHead(503).end(); return; }
      response.writeHead(state.binary ? 200 : 204, { 'Content-Type': 'application/octet-stream' });
      response.end(state.binary);
    } else if (request.method === 'PUT') {
      state.storeAttempts++;
      const projection = JSON.parse(body.toString());
      const binary = Buffer.from(projection.stateBase64, 'base64');
      const fingerprint = projection.fingerprint;
      if (fingerprint !== fingerprintHash(binary, Y.encodeStateVectorFromUpdate(binary))) {
        response.writeHead(400).end(); return;
      }
      if (state.saveGate) await state.saveGate;
      if (state.failStore) { response.writeHead(503).end(); return; }
      state.binary = binary;
      state.projection = projection;
      if (state.fingerprint !== fingerprint) state.revision++;
      state.fingerprint = fingerprint;
      state.saves++;
      response.setHeader('Content-Type', 'application/json');
      response.end(JSON.stringify({ savedAtUtc: new Date().toISOString(), ...(drawing ? { revision: state.revision, fingerprint } : {}) }));
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

  const connect = (accessToken, name = documentName, schemaVersion) => {
    const doc = new Y.Doc();
    const messages = [];
    let denied = false;
    let denialReason;
    const url = new URL(collaborationUrl({ protocol: 'http:', host: `127.0.0.1:${server.address.port}` }));
    if (schemaVersion === null) url.searchParams.delete('schemaVersion');
    else if (schemaVersion !== undefined) url.searchParams.set('schemaVersion', schemaVersion);
    const websocket = new HocuspocusProviderWebsocket({
      url: url.href,
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
  return { state, grants, readOnlyTokens, connect, start, get server() { return server; } };
}

test('internal AI projection requires service authentication, rejects browser origins and malformed states', async t => {
  const f = await fixture(t);
  const url = `http://127.0.0.1:${f.server.address.port}/internal/notion/project`;
  const doc = new Y.Doc();
  const group = new Y.XmlElement('blockGroup'), container = new Y.XmlElement('blockContainer'), paragraph = new Y.XmlElement('paragraph');
  container.setAttribute('id', 'test-block'); paragraph.insert(0, [new Y.XmlText('Intern læsekontekst')]);
  container.insert(0, [paragraph]); group.insert(0, [container]); doc.getXmlFragment('document-store').insert(0, [group]);
  const body = JSON.stringify({ stateBase64: Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64') });
  const send = (headers, payload = body) => fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: payload });
  assert.equal((await send({})).status, 401);
  assert.equal((await send({ 'X-Notion-Service-Key': 'wrong-key' })).status, 401);
  assert.equal((await send({ 'X-Notion-Service-Key': secret, Origin: 'https://mercantec.tech' })).status, 401);
  const accepted = await send({ 'X-Notion-Service-Key': secret });
  assert.equal(accepted.status, 200);
  assert.equal(accepted.headers.get('cache-control'), 'no-store');
  const projection = await accepted.json();
  assert.equal(projection.plainText, 'Intern læsekontekst');
  assert.deepEqual(projection.blocks, [{ id: 'test-block', type: 'paragraph', text: 'Intern læsekontekst', heading: null }]);
  assert.equal((await send({ 'X-Notion-Service-Key': secret }, '{"stateBase64":"bad!"}')).status, 400);
  doc.destroy();
});

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

test('notebook clone HTTP endpoint requires the internal key and rejects browser origins', async t => {
  const f = await fixture(t);
  const url = `http://127.0.0.1:${f.server.address.port}/internal/notebook/clone`;
  const body = JSON.stringify({ pageId: id, defaultTemplate: true, assetMap: {} });
  assert.equal((await fetch(url, { method: 'POST', body })).status, 401);
  assert.equal((await fetch(url, { method: 'POST', body, headers: { 'X-Notion-Service-Key': secret, Origin: 'https://mercantec.tech' } })).status, 401);
  const response = await fetch(url, { method: 'POST', body, headers: { 'X-Notion-Service-Key': secret, 'Content-Type': 'application/json' } });
  assert.equal(response.status, 200);
  const result = await response.json();
  assert.match(result.plainText, /Hvad lærte jeg\?/);
  assert.equal(result.fingerprint, fingerprintHash(Buffer.from(result.stateBase64, 'base64'), Y.encodeStateVectorFromUpdate(Buffer.from(result.stateBase64, 'base64'))));
  assert.equal((await fetch(url, { method: 'POST', body: '{}', headers: { 'X-Notion-Service-Key': secret } })).status, 400);
  assert.equal(f.state.loads, 0);
  assert.equal(f.state.saves, 0);
});

test('a real notebook provider persists live search/link projections and denies ungranted readers', async t => {
  const f = await fixture(t);
  const ownerToken = token('notebook-owner');
  const notebookName = `notebook:${id}`;
  f.grants.set(ownerToken, new Set([notebookName]));
  const owner = f.connect(ownerToken, notebookName);
  await waitFor(() => owner.provider.isSynced, 'owner notebook synchronized');
  const paragraph = new Y.XmlElement('paragraph');
  paragraph.insert(0, [new Y.XmlText('Et rigtigt logbogsindlæg')]);
  const link = new Y.XmlElement('pageLink');
  link.setAttribute('pageId', 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee');
  owner.doc.getXmlFragment('document-store').insert(0, [paragraph, link]);
  await waitFor(() => f.state.projection?.plainText.includes('Et rigtigt logbogsindlæg'), 'notebook search projection durably saved');
  assert.deepEqual(f.state.projection.linkIds, ['aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee']);
  // The stub's write completes before Hocuspocus broadcasts and the provider processes
  // its acknowledgement. Wait for that exact durable state, not just the HTTP write.
  await waitFor(() => owner.messages.some(message => message.type === 'persisted'
    && message.fingerprintHash === f.state.fingerprint), 'matching notebook durable acknowledgement');
  const saves = f.state.saves;
  const reader = f.connect(token('read-only-classmate'), notebookName);
  reader.doc.getText('injected').insert(0, 'Do not store');
  await waitFor(() => reader.denied, 'ungranted notebook reader denied before load');
  assert.equal(f.state.saves, saves);
  assert.equal(owner.doc.getText('injected').toString(), '');
});

test('notebook rooms require canonical lowercase namespace and UUID before authorization or load', async t => {
  const f = await fixture(t);
  const ownerToken = token('canonical-notebook-owner');
  const canonical = `notebook:${id}`;
  const aliases = [`NOTEBOOK:${id}`, 'notebook:AAAAAAAA-BBBB-4CCC-8DDD-EEEEEEEEEEEE'];
  f.grants.set(ownerToken, new Set([canonical, ...aliases]));
  for (const alias of aliases) {
    const client = f.connect(ownerToken, alias);
    await waitFor(() => client.denied, 'noncanonical notebook room rejected');
  }
  assert.equal(f.state.authorizations, 0);
  assert.equal(f.state.loads, 0);
  assert.equal(f.state.saves, 0);
  const owner = f.connect(ownerToken, canonical);
  await waitFor(() => owner.provider.isSynced, 'canonical lowercase notebook accepted');
  assert.equal(f.state.authorizations, 1);
  assert.equal(f.state.loads, 1);
});

test('missing or incompatible editor schemas are rejected before authorization and document loading', async t => {
  const f = await fixture(t);
  const accessToken = token('teacher');
  const drawingName = `drawing:${id}`;
  f.grants.set(accessToken, new Set([documentName, drawingName]));
  for (const version of [null, '1', '2', '3', '4', '5', '6', '999']) {
    const editor = f.connect(accessToken, documentName, version);
    await waitFor(() => editor.denied, 'incompatible editor rejected');
    assert.match(editor.denialReason, /editor_version_mismatch/);
  }
  const drawing = f.connect(accessToken, drawingName, '7');
  await waitFor(() => drawing.denied, 'outdated drawing editor rejected');
  assert.match(drawing.denialReason, /editor_version_mismatch/);
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

test('drawing providers merge whole elements, persist canonical scenes with revisions and allow read-only awareness', async t => {
  const f = await fixture(t), name = `drawing:${id}`;
  const ownerToken = token('drawing-owner'), peerToken = token('drawing-peer'), readerToken = token('drawing-reader');
  for (const value of [ownerToken, peerToken, readerToken]) f.grants.set(value, new Set([name]));
  f.readOnlyTokens.add(readerToken);
  const owner = f.connect(ownerToken, name), peer = f.connect(peerToken, name), reader = f.connect(readerToken, name);
  await waitFor(() => owner.provider.isSynced && peer.provider.isSynced && reader.provider.isSynced, 'drawing peers and reader synced');
  owner.doc.getMap('elements').set('rectangle', { id: 'rectangle', type: 'rectangle', index: 'a0' });
  peer.doc.getMap('elements').set('ellipse', { id: 'ellipse', type: 'ellipse', index: 'a1' });
  await waitFor(() => reader.doc.getMap('elements').size === 2 && owner.doc.getMap('elements').size === 2, 'merged whole drawing elements');
  await waitFor(() => owner.messages.some(message => message.type === 'persisted' && message.revision > 0), 'revision-bearing drawing acknowledgement');
  const scene = JSON.parse(f.state.projection.sceneJson);
  assert.deepEqual(scene.elements.map(element => element.id), ['rectangle', 'ellipse']);
  reader.provider.awareness.setLocalStateField('user', { name: 'Reader' });
  await waitFor(() => [...owner.provider.awareness.getStates().values()].some(value => value.user?.name === 'Reader'), 'read-only reader presence');
  reader.doc.getMap('elements').set('forbidden', { id: 'forbidden', type: 'rectangle' });
  await new Promise(resolve => setTimeout(resolve, 100));
  assert.equal(owner.doc.getMap('elements').has('forbidden'), false);
  assert.equal(f.server.hocuspocus.documents.get(name).getMap('elements').has('forbidden'), false);
  assert.equal(reader.messages.find(message => message.type === 'permissions')?.canEdit, false);
});

test('drawing permissions downgrade, upgrade and invalidate on a live connection without granting read-only writes', async t => {
  const f = await fixture(t, { reauthorizeMs: 60 }), name = `drawing:${id}`, accessToken = token('drawing-grants');
  f.grants.set(accessToken, new Set([name])); f.readOnlyTokens.add(accessToken);
  const client = f.connect(accessToken, name);
  await waitFor(() => client.provider.isSynced, 'drawing reader connected');
  f.readOnlyTokens.delete(accessToken);
  await waitFor(() => client.messages.some(message => message.type === 'permissions' && message.canEdit === true), 'read grant promoted to edit');
  client.doc.getMap('elements').set('allowed', { id: 'allowed', type: 'rectangle' });
  await waitFor(() => f.state.saves > 0, 'new write grant saved');
  f.readOnlyTokens.add(accessToken);
  const response = await fetch(`http://127.0.0.1:${f.server.address.port}/internal/drawing/invalidate`, { method: 'POST', headers: { 'X-Notion-Service-Key': secret }, body: JSON.stringify({ drawingId: id }) });
  assert.equal(response.status, 200);
  const live = f.server.hocuspocus.documents.get(name);
  assert.equal([...live.connections.keys()][0].readOnly, true);
  client.doc.getMap('elements').set('revoked', { id: 'revoked', type: 'ellipse' });
  await new Promise(resolve => setTimeout(resolve, 100));
  assert.equal(live.getMap('elements').has('revoked'), false);
  f.grants.delete(accessToken);
  await waitFor(() => !f.server.hocuspocus.documents.get(name)?.getConnectionsCount(), 'revoked read access closes drawing socket');
});

test('drawing state limit rejects a cumulative oversized update before apply, broadcast or persistence', async t => {
  const f = await fixture(t), name = `drawing:${id}`, accessToken = token('drawing-limits');
  f.grants.set(accessToken, new Set([name]));
  const sender = f.connect(accessToken, name), observer = f.connect(accessToken, name);
  await waitFor(() => sender.provider.isSynced && observer.provider.isSynced, 'drawing peers synced');
  sender.doc.getMap('elements').set('first', { id: 'first', type: 'text', text: 'a'.repeat(650000) });
  await waitFor(() => observer.doc.getMap('elements').has('first') && f.state.saves > 0, 'valid large element durably saved');
  sender.doc.getMap('elements').set('second', { id: 'second', type: 'text', text: 'b'.repeat(650000) });
  await waitFor(() => f.server.hocuspocus.documents.get(name)?.getConnectionsCount() === 1, 'oversized cumulative update disconnected');
  assert.equal(observer.doc.getMap('elements').has('second'), false);
  assert.equal(f.server.hocuspocus.documents.get(name).getMap('elements').has('second'), false);
  assert.equal(JSON.parse(f.state.projection.sceneJson).elements.some(element => element.id === 'second'), false);
});

test('drawing checkpoint flushes the current scene and returns its persisted fingerprint and revision', async t => {
  const f = await fixture(t), name = `drawing:${id}`, accessToken = token('drawing-checkpoint');
  f.grants.set(accessToken, new Set([name]));
  const client = f.connect(accessToken, name);
  await waitFor(() => client.provider.isSynced, 'drawing writer synced');
  client.doc.getMap('elements').set('a', { id: 'a', type: 'rectangle' });
  await waitFor(() => f.server.hocuspocus.documents.get(name).getMap('elements').has('a'), 'server has latest scene');
  const url = `http://127.0.0.1:${f.server.address.port}/internal/drawing/checkpoint`;
  assert.equal((await fetch(url, { method: 'POST', body: '{}' })).status, 401);
  const response = await fetch(url, { method: 'POST', headers: { 'X-Notion-Service-Key': secret }, body: JSON.stringify({ drawingIds: [id] }) });
  assert.equal(response.status, 200);
  const result = await response.json();
  assert.equal(result.drawings[0].fingerprint, fingerprintHash(Y.encodeStateAsUpdate(client.doc), Y.encodeStateVector(client.doc)));
  assert.equal(result.drawings[0].revision, f.state.revision);
  assert.equal(JSON.parse(f.state.projection.sceneJson).elements[0].id, 'a');
});
