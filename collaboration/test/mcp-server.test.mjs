import test from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import * as Y from 'yjs';
import { WebSocket } from 'ws';
import { HocuspocusProvider, HocuspocusProviderWebsocket } from '@hocuspocus/provider';
import { createCollaborationServer } from '../src/server.mjs';
import { deserializeMcpXml, MCP_RECEIPTS } from '../src/mcp-documents.mjs';

const id = '11111111-2222-4333-8444-555555555555', sub = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', name = `notion:${id}`;
const secret = 'integration-mcp-service-secret-not-a-real-key';
const token = `header.${Buffer.from(JSON.stringify({ sub, exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.signature`;
const waitFor = async (check, message, timeout = 6000) => {
  const end = Date.now() + timeout;
  while (Date.now() < end) { if (await check()) return; await new Promise(resolve => setTimeout(resolve, 15)); }
  assert.fail('Timed out: ' + message);
};
const sample = () => {
  const doc = new Y.Doc();
  doc.getXmlFragment('document-store').insert(0, [deserializeMcpXml({ kind: 'element', name: 'blockGroup', attributes: {}, children: ['a', 'b'].map(id => ({ kind: 'element', name: 'blockContainer', attributes: { id }, children: [{ kind: 'element', name: 'paragraph', attributes: {}, children: [{ kind: 'text', delta: [{ insert: id.toUpperCase() }] }] }] })) })]);
  const bytes = Y.encodeStateAsUpdate(doc); doc.destroy(); return bytes;
};
async function fixture(t) {
  const state = { binary: sample(), allowed: true, failStore: false, failPrepareAcknowledgement: false, authorizations: 0, saved: 0, audit: new Map(), pending: new Set(), afterPrepare: null };
  const api = {
    async authorize(documentName, supplied) { state.authorizations++; if (!state.allowed || supplied !== token || documentName !== name) throw new Error('denied'); return { canEdit: true, canRead: true, documentId: id, expiresAt: Date.now() + 3600000 }; },
    async load() { return state.binary; },
    async prepareMcpChange(pageId, change) {
      assert.equal(pageId, id); assert.ok(state.pending.has(change.operationId));
      const existing = state.audit.get(change.operationId);
      if (existing) assert.deepEqual(existing, change); else state.audit.set(change.operationId, change);
      if (state.afterPrepare) await state.afterPrepare();
      if (state.failPrepareAcknowledgement) throw new Error('Prepared in DB, network acknowledgement lost');
    },
    async store(documentName, bytes, fingerprint, change) {
      if (state.failStore) throw new Error('DB unavailable');
      if (change) { assert.ok(state.audit.has(change.operationId)); assert.ok(state.pending.has(change.operationId)); }
      state.binary = bytes; state.saved++;
      return change ? { savedAtUtc: new Date().toISOString(), mcpOperationId: change.operationId } : new Date().toISOString();
    },
  };
  const servers = [], clients = []; let server;
  const start = async () => { server = createCollaborationServer({ api, serviceSecret: secret, port: 0, address: '127.0.0.1', debounceMs: 100, maxDebounceMs: 200 }); servers.push(server); await server.listen(); return server; };
  await start();
  t.after(async () => { for (const client of clients) client.destroy(); for (const server of servers) await server.destroy(); });
  const send = async (action, body, extraHeaders = {}) => {
    const response = await fetch(`http://127.0.0.1:${server.address.port}/internal/notion/mcp/${action}`, { method: 'POST', headers: { 'X-Notion-Service-Key': secret, 'Content-Type': 'application/json', ...extraHeaders }, body: JSON.stringify({ pageId: id, accessToken: token, ...body }) });
    return { status: response.status, body: await response.json() };
  };
  const read = () => send('read', {});
  const request = changes => { const operationId = randomUUID(); state.pending.add(operationId); return { actorSub: sub, operationId, requestHash: 'a'.repeat(64), changes }; };
  const connect = () => {
    const doc = new Y.Doc();
    const websocket = new HocuspocusProviderWebsocket({ url: `ws://127.0.0.1:${server.address.port}/collaboration?schemaVersion=8`, WebSocketPolyfill: WebSocket, autoConnect: false, maxAttempts: 1 });
    const provider = new HocuspocusProvider({ name, token, document: doc, websocketProvider: websocket }); provider.attach(); websocket.connect().catch(() => {});
    const client = { doc, provider, websocket, destroy() { provider.destroy(); websocket.destroy(); doc.destroy(); } }; clients.push(client); return client;
  };
  return { state, read, send, request, connect, start, get server() { return server; } };
}

test('browser-closed edits wait for durable audit+state, replay safely, and survive restart', async t => {
  const f = await fixture(t);
  const prior = await f.read(); assert.equal(prior.status, 200);
  const req = f.request([{ type: 'insert_after', blockId: 'a', fingerprint: prior.body.blocks[0].fingerprint, block: { type: 'paragraph', content: 'Codex uden åben browser' } }]);
  const applied = await f.send('apply', req); assert.equal(applied.status, 200); assert.equal(applied.body.replayed, false); assert.ok(applied.body.savedAtUtc);
  assert.equal(f.state.audit.size, 1);
  const replay = await f.send('apply', req); assert.equal(replay.status, 200); assert.equal(replay.body.replayed, true);
  assert.equal((await f.read()).body.blocks.filter(b => b.text === 'Codex uden åben browser').length, 1);
  const otherPayload = await f.send('apply', { ...req, changes: [{ ...req.changes[0], block: { type: 'paragraph', content: 'Different' } }] }); assert.equal(otherPayload.status, 409);
  await f.server.destroy(); await f.start();
  const reopened = await f.read(); assert.equal(reopened.status, 200); assert.equal(reopened.body.blocks.length, 3);
  assert.equal((await f.send('apply', req)).body.replayed, true);
});

test('an open real provider receives the MCP update and unrelated unsaved client edits are retained', async t => {
  const f = await fixture(t), browser = f.connect(); await waitFor(() => browser.provider.isSynced, 'browser sync');
  const prior = await f.read();
  const req = f.request([{ type: 'replace_text', blockId: 'a', contentFingerprint: prior.body.blocks[0].contentFingerprint, text: 'Ny forklaring' }]);
  const text = browser.doc.getXmlFragment('document-store').get(0).get(1).get(0).get(0); text.insert(1, ' fra browser');
  await waitFor(() => f.server.hocuspocus.documents.get(name).getXmlFragment('document-store').get(0).get(1).get(0).get(0).toString().includes('browser'), 'live unsaved edit reaches room');
  const response = await f.send('apply', req); assert.equal(response.status, 200);
  await waitFor(() => browser.doc.getXmlFragment('document-store').get(0).get(0).get(0).get(0).toString() === 'Ny forklaring', 'MCP write synchronized');
  assert.equal(browser.doc.getXmlFragment('document-store').get(0).get(1).get(0).get(0).toString(), 'B fra browser');
  assert.equal(browser.doc.getMap(MCP_RECEIPTS).has(req.operationId), true);
});

test('persistence errors never return success and the same operation retries without inserting twice', async t => {
  const f = await fixture(t), prior = await f.read();
  const req = f.request([{ type: 'insert_after', blockId: 'a', fingerprint: prior.body.blocks[0].fingerprint, block: { type: 'paragraph', content: 'Gem mig én gang' } }]);
  f.state.failStore = true;
  const failed = await f.send('apply', req); assert.equal(failed.status, 503); assert.equal(failed.body.error, 'document_unavailable');
  f.state.failStore = false;
  const retry = await f.send('apply', req); assert.equal(retry.status, 200); assert.equal(retry.body.replayed, true);
  assert.equal((await f.read()).body.blocks.filter(b => b.text === 'Gem mig én gang').length, 1);
});

test('a lost audit-prepare acknowledgement can retry the same operation without unstable XML clock anchors', async t => {
  const f = await fixture(t), prior = await f.read();
  const req = f.request([{ type: 'insert_after', blockId: 'a', fingerprint: prior.body.blocks[0].fingerprint, block: { type: 'paragraph', content: 'Stabil audit' } }]);
  f.state.failPrepareAcknowledgement = true;
  assert.equal((await f.send('apply', req)).status, 503);
  assert.equal((await f.read()).body.blocks.length, 2);
  // An unrelated edit cannot change the prepared audit, either.
  f.server.hocuspocus.documents.get(name).getXmlFragment('document-store').get(0).get(1).get(0).get(0).insert(1, ' udenfor');
  f.state.failPrepareAcknowledgement = false;
  assert.equal((await f.send('apply', req)).status, 200);
  assert.equal((await f.read()).body.blocks.filter(b => b.text === 'Stabil audit').length, 1);
});

test('revocation during audit prepare stops before mutation, and internal routes reject browser-origin and wrong user', async t => {
  const f = await fixture(t), prior = await f.read();
  const req = f.request([{ type: 'replace_text', blockId: 'a', fingerprint: prior.body.blocks[0].fingerprint, text: 'Må ikke skrives' }]);
  assert.equal((await f.send('apply', { ...req, actorSub: id })).status, 400);
  assert.equal((await f.send('read', {}, { Origin: 'https://lup.dk' })).status, 401);
  assert.equal((await f.send('read', {}, { 'X-Notion-Service-Key': 'wrong' })).status, 401);
  f.state.afterPrepare = async () => { f.state.allowed = false; };
  assert.equal((await f.send('apply', req)).status, 403);
  f.state.allowed = true; f.state.afterPrepare = null;
  assert.equal((await f.read()).body.blocks[0].text, 'A');
});

test('server undo validates the current target, restores history and keeps unrelated changes', async t => {
  const f = await fixture(t), prior = await f.read();
  const req = f.request([{ type: 'replace_text', blockId: 'a', fingerprint: prior.body.blocks[0].fingerprint, text: 'Ændret' }]);
  assert.equal((await f.send('apply', req)).status, 200);
  const undo = f.request([]); undo.undoOf = req.operationId; undo.originalChange = f.state.audit.get(req.operationId);
  assert.equal((await f.send('undo', undo)).status, 200); assert.equal((await f.read()).body.blocks[0].text, 'A');
  assert.equal((await f.send('undo', undo)).body.replayed, true);
});
