import { createHash, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { Server, IncomingMessage, MessageType } from '@hocuspocus/server';
import * as Y from 'yjs';
import { createApiClient, documentDomainFromName, documentIdFromName, documentLifecycleFromName, resolveServiceSecret, StaleDocumentSessionError, TOKEN_CLOCK_SKEW_MS, versionedDocumentName } from './api-client.mjs';
import { fingerprintHash } from './fingerprint.mjs';
import { cloneNotebookDocument, projectNotionContext } from './notebook-documents.mjs';
import { cloneDrawingDocument, drawingStateProjection, validateDrawingUpdate, MAX_DRAWING_BYTES } from './drawing-documents.mjs';
import { McpDocumentError, MCP_RECEIPTS, readMcpPage, prepareMcpApply, prepareMcpUndo, makeMcpReceipt, verifyMcpReceipt, revalidateMcpPlan, applyPreparedMcpPlan } from './mcp-documents.mjs';

const DENIED = { code: 4403, reason: 'Document access denied. Open the page again to reconnect.' };
const SCHEMA_MISMATCH = { code: 4409, reason: 'editor_version_mismatch' };

export function createCollaborationServer({
  apiBaseUrl = process.env.API_BASE_URL ?? 'http://mercantec-tech-api:8080',
  serviceSecret = process.env.NOTION_SERVICE_SECRET,
  serviceSecretFile = process.env.NOTION_SERVICE_SECRET_FILE,
  port = Number(process.env.PORT ?? 1234),
  address = process.env.HOST ?? '0.0.0.0',
  allowedOrigins = (process.env.ALLOWED_ORIGINS ?? 'https://mercantec.tech,http://localhost:3000').split(',').map(s => s.trim()).filter(Boolean),
  api = createApiClient({ apiBaseUrl, serviceSecret: resolveServiceSecret(serviceSecret, serviceSecretFile) }),
  reauthorizeMs = 15000,
  debounceMs = 750,
  maxDebounceMs = 3000,
} = {}) {
  if (!Number.isInteger(port) || port < 0 || port > 65535) throw new Error('PORT must be a valid TCP port');
  const sessions = new Set();
  const cloneSecret = resolveServiceSecret(serviceSecret, serviceSecretFile);
  const drawingQueues = new WeakMap();
  const lastEditors = new WeakMap();
  const mcpQueues = new Map();
  async function lockMcp(documentName) {
    const previous = mcpQueues.get(documentName) ?? Promise.resolve();
    let unlock;
    const pending = new Promise(resolve => { unlock = resolve; });
    mcpQueues.set(documentName, pending); await previous;
    return () => { unlock(); if (mcpQueues.get(documentName) === pending) mcpQueues.delete(documentName); };
  }
  async function lockDrawing(document) {
    const previous = drawingQueues.get(document) ?? Promise.resolve();
    let unlock;
    const pending = new Promise(resolve => { unlock = resolve; });
    drawingQueues.set(document, pending);
    await previous;
    let released = false;
    return () => { if (released) return; released = true; unlock(); if (drawingQueues.get(document) === pending) drawingQueues.delete(document); };
  }

  async function checkAccess(context, documentName, force = false) {
    if (!context?.token || Date.now() >= context.expiresAt + TOKEN_CLOCK_SKEW_MS) throw DENIED;
    if (!force && Date.now() - context.authorizedAt < reauthorizeMs) return;
    // Share a pending check between the timer and arriving document updates.
    const generation = context.authorizationGeneration ?? 0;
    context.pendingAuthorization ??= api.authorize(documentName, context.token).then(permission => {
      if (generation !== (context.authorizationGeneration ?? 0) || (permission.lifecycleVersion ?? 0) !== documentLifecycleFromName(documentName)) throw DENIED;
      const changed = context.canEdit !== permission.canEdit;
      context.expiresAt = permission.expiresAt;
      context.canRead = permission.canRead;
      context.canEdit = permission.canEdit;
      context.accessVersion = permission.accessVersion;
      context.editorName = permission.editorName;
      if (context.connection && documentDomainFromName(documentName) === 'drawing') {
        context.connection.readOnly = permission.canEdit !== true;
        if (changed) context.connection.sendStateless(JSON.stringify({ type: 'permissions', canRead: true, canEdit: permission.canEdit, accessVersion: permission.accessVersion }));
      }
      context.authorizedAt = Date.now();
    }).catch(() => { throw DENIED; }).finally(() => { context.pendingAuthorization = null; });
    await context.pendingAuthorization;
  }

  function stopSession(context) {
    if (!context) return;
    clearInterval(context.reauthorizationTimer);
    clearTimeout(context.expiryTimer);
    context.closed = true;
    sessions.delete(context);
  }

  async function persistDocument(documentName, document, mcpChange = undefined) {
    const state = Y.encodeStateAsUpdate(document), vector = Y.encodeStateVector(document);
    const fingerprint = fingerprintHash(state, vector);
    const editorName = lastEditors.get(document);
    try {
      const result = await api.store(documentName, state, fingerprint, mcpChange, { editorName });
      const metadata = typeof result === 'string' ? { savedAtUtc: result } : result;
      if (fingerprint === fingerprintHash(Y.encodeStateAsUpdate(document), Y.encodeStateVector(document))) {
        document.broadcastStateless(JSON.stringify({
          type: 'persisted', ...metadata,
          stateVector: Buffer.from(vector).toString('base64'),
          stateHash: createHash('sha256').update(state).digest('hex'), fingerprintHash: fingerprint,
        }));
      }
      return { id: documentIdFromName(documentName), fingerprint, ...metadata };
    } catch (error) {
      if (error instanceof StaleDocumentSessionError) {
        for (const context of sessions) {
          if (context.documentName !== documentName) continue;
          stopSession(context);
          context.connection.close(DENIED);
        }
        throw error;
      }
      document.broadcastStateless(JSON.stringify({ type: 'persistenceError', message: 'Indholdet er endnu ikke gemt i skyen. Prøver igen.' }));
      throw new Error('Document persistence failed');
    }
  }

  const server = new Server({
    name: 'lup-notion-collaboration', port, address, quiet: true, stopOnSignals: false,
    timeout: 30000, debounce: debounceMs, maxDebounce: maxDebounceMs,
    websocketOptions: { maxPayload: 2 * 1024 * 1024 },
    maxUnauthenticatedQueueSize: 512 * 1024,
    maxUnauthenticatedQueueMessages: 64,
    maxPendingDocuments: 4,

    async onUpgrade({ request, socket }) {
      const pathname = new URL(request.url ?? '/', 'http://localhost').pathname;
      const origin = request.headers.origin;
      if (!['/collaboration', '/collaboration/', '/'].includes(pathname) || (origin && !allowedOrigins.includes(origin))) {
        socket.write('HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n');
        socket.destroy();
        throw null;
      }
    },

    async onRequest({ request, response }) {
      if (request.method === 'POST' && ['/internal/notion/mcp/read', '/internal/notion/mcp/apply', '/internal/notion/mcp/undo'].includes(request.url)) {
        const supplied = request.headers['x-notion-service-key'];
        const trusted = typeof supplied === 'string' && supplied.length <= 1024 && cloneSecret.length >= 32
          && timingSafeEqual(createHash('sha256').update(supplied).digest(), createHash('sha256').update(cloneSecret).digest());
        if (!trusted || request.headers.origin) {
          response.writeHead(401, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); response.end(JSON.stringify({ error: 'unauthorized' })); throw null;
        }
        let connection, release;
        try {
          const chunks = []; let size = 0;
          for await (const chunk of request) { size += chunk.length; if (size > 24 * 1024 * 1024) throw new McpDocumentError('request_too_large', 'Opgaven er for stor.', 413); chunks.push(chunk); }
          let body; try { body = JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { throw new McpDocumentError('invalid_request', 'Ugyldig JSON.'); }
          if (typeof body?.pageId !== 'string' || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(body.pageId)) throw new McpDocumentError('invalid_request', 'Ugyldigt side-id.');
          const pageId = documentIdFromName(`notion:${body.pageId}`), baseName = `notion:${pageId}`;
          if (body.pageId !== pageId || typeof body.accessToken !== 'string') throw new McpDocumentError('invalid_request', 'Ugyldigt side-id eller adgangstoken.');
          let lifecycle;
          const authorize = async () => {
            try {
              const permission = await api.authorize(baseName, body.accessToken);
              if (!permission.canEdit || lifecycle !== undefined && lifecycle !== (permission.lifecycleVersion ?? 0)) throw new Error();
              return permission;
            }
            catch { throw new McpDocumentError('access_denied', 'Din aktuelle redigeringsadgang kunne ikke bekræftes.', 403); }
          };
          lifecycle = (await authorize()).lifecycleVersion ?? 0;
          const name = versionedDocumentName(baseName, lifecycle);
          release = await lockMcp(name);
          connection = await server.hocuspocus.openDirectConnection(name, { source: 'mcp', token: body.accessToken });
          const document = connection.document;
          let result;
          if (request.url.endsWith('/read')) {
            await authorize();
            result = readMcpPage(document, body);
          } else {
            let sub;
            try { const token = body.accessToken.replace(/^Bearer /, ''); sub = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8')).sub; } catch { /* Denied below after signature-backed API authorization. */ }
            if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(body.actorSub || '') || typeof sub !== 'string' || sub.toLowerCase() !== body.actorSub
                || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(body.operationId || '') || !/^[0-9a-f]{64}$/.test(body.requestHash || '')) throw new McpDocumentError('invalid_request', 'Ugyldig operationsidentitet.');
            const savedReceipt = document.getMap(MCP_RECEIPTS).get(body.operationId);
            if (savedReceipt) {
              if (!verifyMcpReceipt(cloneSecret, body, savedReceipt)) throw new McpDocumentError('operation_conflict', 'Operations-id’et er allerede brugt eller har en ugyldig kvittering.', 409);
              await authorize();
            } else {
              const plan = request.url.endsWith('/undo') ? prepareMcpUndo(document, body) : prepareMcpApply(document, body);
              await api.prepareMcpChange(pageId, { operationId: body.operationId, ownerSub: body.actorSub, requestHash: body.requestHash,
                summary: typeof body.summary === 'string' ? body.summary.slice(0, 1000) : (request.url.endsWith('/undo') ? 'Fortryd Codex-ændring' : 'Codex redigerede dokumentet'),
                ...(body.undoOf ? { undoOf: body.undoOf } : {}), before: plan.before, after: plan.after });
              const permission = await authorize();
              revalidateMcpPlan(document, plan);
              applyPreparedMcpPlan(document, plan, makeMcpReceipt(cloneSecret, body, plan), permission.editorName);
            }
            // onStoreDocument/disconnect intentionally swallow hook errors. This explicit path must propagate the DB acknowledgement.
            const stored = await document.saveMutex.runExclusive(() => persistDocument(name, document,
              { operationId: body.operationId, ownerSub: body.actorSub, requestHash: body.requestHash }));
            result = { pageId, operationId: body.operationId, status: 'applied', replayed: Boolean(savedReceipt), savedAtUtc: stored.savedAtUtc, fingerprint: stored.fingerprint };
          }
          response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); response.end(JSON.stringify(result));
        } catch (error) {
          const known = error instanceof McpDocumentError;
          response.writeHead(known ? error.status : 503, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
          response.end(JSON.stringify({ error: known ? error.code : 'document_unavailable', detail: known ? error.message : 'Ændringen er endnu ikke bekræftet gemt. Prøv igen med samme operations-id.' }));
        } finally {
          // Keeping the room warm also avoids a second swallowed store being mistaken for the explicit acknowledgement above.
          await connection?.disconnect({ unloadImmediately: false }).catch(() => {}); release?.();
        }
        throw null;
      }
      if (request.method === 'POST' && ['/internal/notebook/clone', '/internal/notion/project', '/internal/drawing/clone', '/internal/drawing/checkpoint', '/internal/drawing/invalidate'].includes(request.url)) {
        const supplied = request.headers['x-notion-service-key'];
        const authorized = typeof supplied === 'string' && supplied.length <= 1024 && cloneSecret.length >= 32
          && timingSafeEqual(createHash('sha256').update(supplied).digest(), createHash('sha256').update(cloneSecret).digest());
        if (!authorized || request.headers.origin) {
          response.writeHead(401, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
          response.end(JSON.stringify({ error: 'unauthorized' }));
          throw null;
        }
        try {
          const chunks = []; let size = 0;
          for await (const chunk of request) {
            size += chunk.length;
            if (size > 24 * 1024 * 1024) throw new Error('Clone request too large');
            chunks.push(chunk);
          }
          const body = JSON.parse(Buffer.concat(chunks).toString('utf8'));
          let result;
          if (request.url === '/internal/notebook/clone') result = cloneNotebookDocument(body);
          else if (request.url === '/internal/notion/project') result = projectNotionContext(body);
          else if (request.url === '/internal/drawing/clone') result = cloneDrawingDocument(body);
          else if (request.url === '/internal/drawing/checkpoint') {
            if (!Array.isArray(body?.drawingIds) || body.drawingIds.length > 500) throw new Error('Invalid drawing IDs');
            const ids = [...new Set(body.drawingIds.map(id => documentIdFromName(`drawing:${id}`)))];
            const drawings = [];
            for (const id of ids) {
              const name = versionedDocumentName(`drawing:${id}`, await api.drawingLifecycle?.(id) ?? 0);
              const document = server.hocuspocus.documents.get(name);
              if (document) {
                if (document.isLoading) throw new Error('Drawing is loading');
                drawings.push(await document.saveMutex.runExclusive(() => persistDocument(name, document)));
              } else {
                const state = await api.load(name);
                if (!state) throw new Error('Drawing has no stored state');
                const fingerprint = fingerprintHash(state, Y.encodeStateVectorFromUpdate(state));
                drawings.push({ id, fingerprint, ...await api.store(name, state, fingerprint, undefined, { preserveEditMetadata: true }) });
              }
            }
            result = { drawings };
          } else {
            const id = documentIdFromName(`drawing:${body?.drawingId}`);
            const matching = [...sessions].filter(context => documentDomainFromName(context.documentName) === 'drawing' && documentIdFromName(context.documentName) === id);
            for (const context of matching) {
              context.authorizationGeneration = (context.authorizationGeneration ?? 0) + 1;
              context.authorizedAt = 0;
              context.connection.readOnly = true;
            }
            await Promise.all(matching.map(async context => {
              await context.pendingAuthorization?.catch(() => {});
              try { await checkAccess(context, context.documentName, true); }
              catch { stopSession(context); context.connection.close(DENIED); }
            }));
            result = { invalidated: true };
          }
          response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
          response.end(JSON.stringify(result));
        } catch {
          response.writeHead(400, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
          response.end(JSON.stringify({ error: request.url === '/internal/notion/project' ? 'invalid_projection_request' : 'invalid_clone_request' }));
        }
        throw null;
      }
      const healthy = request.method === 'GET' && ['/health', '/collaboration/health'].includes((request.url ?? '').split('?')[0]);
      response.writeHead(healthy ? 200 : 404, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
      response.end(JSON.stringify(healthy ? { status: 'ok', service: 'notion-collaboration' } : { error: 'not_found' }));
      throw null;
    },

    async onAuthenticate({ documentName, token, requestParameters, connectionConfig }) {
      // An older editor can discard block types or props it does not know.
      if (requestParameters?.get('schemaVersion') !== '9') throw SCHEMA_MISMATCH;
      const documentId = documentIdFromName(documentName);
      // Hocuspocus keys live rooms by the raw name, while storage uses the UUID.
      // Do not allow two differently-cased notebook rooms to overwrite one document.
      const domain = documentDomainFromName(documentName);
      const lifecycle = documentLifecycleFromName(documentName);
      if ((domain !== 'notion' || lifecycle > 0) && documentName !== versionedDocumentName(`${domain}:${documentId}`, lifecycle)) throw DENIED;
      const permission = await api.authorize(documentName, token);
      if ((permission.lifecycleVersion ?? 0) !== lifecycle) throw DENIED;
      if (domain === 'drawing') connectionConfig.readOnly = permission.canEdit !== true;
      return { token, documentName, ...permission, authorizedAt: Date.now(), windowStarted: Date.now(), messagesInWindow: 0 };
    },

    async connected({ context, documentName, connection }) {
      context.connection = connection;
      sessions.add(context);
      if (documentDomainFromName(documentName) === 'drawing') connection.sendStateless(JSON.stringify({ type: 'permissions', canRead: true, canEdit: context.canEdit, accessVersion: context.accessVersion }));
      const close = () => { stopSession(context); connection.close(DENIED); };
      // Keep the socket open through the same skew window the authorize check allows.
      context.expiryTimer = setTimeout(close, Math.min(
        Math.max(context.expiresAt + TOKEN_CLOCK_SKEW_MS - Date.now(), 1000),
        2147483647,
      ));
      context.expiryTimer.unref();
      context.reauthorizationTimer = setInterval(() => {
        checkAccess(context, documentName, true).catch(close);
      }, reauthorizeMs);
      context.reauthorizationTimer.unref();
    },

    async beforeHandleMessage({ context, documentName, update }) {
      await checkAccess(context, documentName);
      if (documentDomainFromName(documentName) === 'drawing' && update.byteLength > MAX_DRAWING_BYTES + 1024) throw { code: 4409, reason: 'drawing_state_limit' };
      if (Date.now() - context.windowStarted >= 1000) {
        context.windowStarted = Date.now();
        context.messagesInWindow = 0;
      }
      if (++context.messagesInWindow > 200) throw { code: 4429, reason: 'Too many document updates' };

      const incoming = new IncomingMessage(update);
      incoming.readVarString();
      const type = incoming.readVarUint();
      // Only this server may issue durable-save acknowledgements. A user's
      // BroadcastStateless message must never impersonate a successful save.
      if (type === MessageType.Stateless || type === MessageType.BroadcastStateless) throw DENIED;
    },

    async beforeSync({ context, documentName, document, connection, type, payload }) {
      if (documentDomainFromName(documentName) !== 'drawing' || type === 0) return;
      if (payload.byteLength > MAX_DRAWING_BYTES) throw { code: 4409, reason: 'drawing_state_limit' };
      if (connection.readOnly) return;
      const release = await lockDrawing(document);
      try {
        if (context.closed) throw DENIED;
        if (connection.readOnly) { release(); return; }
        validateDrawingUpdate(document, payload);
        context.releaseDrawingMessage = release;
      } catch (error) { release(); throw { code: 4409, reason: 'invalid_drawing_state' }; }
    },

    async afterHandleMessage({ context }) {
      context.releaseDrawingMessage?.();
      context.releaseDrawingMessage = null;
    },

    async onLoadDocument({ documentName }) {
      const state = await api.load(documentName);
      if (state && documentDomainFromName(documentName) === 'drawing') drawingStateProjection(state);
      // Stamp legacy persisted documents with canonical publication metadata before editing.
      // Re-saving an identical fingerprint does not advance the API's draft revision.
      if (state) await api.store(documentName, state, fingerprintHash(state, Y.encodeStateVectorFromUpdate(state)),
        undefined, { preserveEditMetadata: true });
      return state ?? undefined;
    },

    onChange({ document, context, transactionOrigin }) {
      // A durable save may combine updates from several sessions. Track the actor of the
      // last actual Yjs change, independent of which connection caused the debounced save.
      const editorName = transactionOrigin?.source === 'mcp' ? transactionOrigin.editorName
        : context?.canEdit === true ? context.editorName : undefined;
      lastEditors.set(document, editorName);
    },

    async onStoreDocument({ documentName, document }) {
      try { await persistDocument(documentName, document); }
      catch (error) { if (!(error instanceof StaleDocumentSessionError)) throw error; }
    },

    async onDisconnect({ context }) { stopSession(context); },
    async onDestroy() { for (const context of sessions) stopSession(context); },
  });
  return server;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const server = createCollaborationServer();
  await server.listen();
  console.log('LUP document collaboration is listening');
  let stopping = false;
  const shutdown = async () => {
    if (stopping) return;
    stopping = true;
    const timeout = setTimeout(() => process.exit(1), 20000);
    timeout.unref();
    await server.destroy();
    clearTimeout(timeout);
    process.exit(0);
  };
  process.on('SIGINT', shutdown);
  process.on('SIGTERM', shutdown);
}
