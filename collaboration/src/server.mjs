import { createHash, timingSafeEqual } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { Server, IncomingMessage, MessageType } from '@hocuspocus/server';
import * as Y from 'yjs';
import { createApiClient, documentDomainFromName, documentIdFromName, resolveServiceSecret, TOKEN_CLOCK_SKEW_MS } from './api-client.mjs';
import { fingerprintHash } from './fingerprint.mjs';
import { cloneNotebookDocument } from './notebook-documents.mjs';

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

  async function checkAccess(context, documentName, force = false) {
    if (!context?.token || Date.now() >= context.expiresAt + TOKEN_CLOCK_SKEW_MS) throw DENIED;
    if (!force && Date.now() - context.authorizedAt < reauthorizeMs) return;
    // Share a pending check between the timer and arriving document updates.
    context.pendingAuthorization ??= api.authorize(documentName, context.token).then(permission => {
      context.expiresAt = permission.expiresAt;
      context.authorizedAt = Date.now();
    }).catch(() => { throw DENIED; }).finally(() => { context.pendingAuthorization = null; });
    await context.pendingAuthorization;
  }

  function stopSession(context) {
    if (!context) return;
    clearInterval(context.reauthorizationTimer);
    clearTimeout(context.expiryTimer);
    sessions.delete(context);
  }

  return new Server({
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
      if (request.method === 'POST' && request.url === '/internal/notebook/clone') {
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
          const result = cloneNotebookDocument(JSON.parse(Buffer.concat(chunks).toString('utf8')));
          response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
          response.end(JSON.stringify(result));
        } catch {
          response.writeHead(400, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
          response.end(JSON.stringify({ error: 'invalid_clone_request' }));
        }
        throw null;
      }
      const healthy = request.method === 'GET' && ['/health', '/collaboration/health'].includes((request.url ?? '').split('?')[0]);
      response.writeHead(healthy ? 200 : 404, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
      response.end(JSON.stringify(healthy ? { status: 'ok', service: 'notion-collaboration' } : { error: 'not_found' }));
      throw null;
    },

    async onAuthenticate({ documentName, token, requestParameters }) {
      // An older editor can discard block types or props it does not know.
      if (requestParameters?.get('schemaVersion') !== '5') throw SCHEMA_MISMATCH;
      const documentId = documentIdFromName(documentName);
      // Hocuspocus keys live rooms by the raw name, while storage uses the UUID.
      // Do not allow two differently-cased notebook rooms to overwrite one document.
      if (documentDomainFromName(documentName) === 'notebook' && documentName !== `notebook:${documentId}`) throw DENIED;
      const permission = await api.authorize(documentName, token);
      return { token, ...permission, authorizedAt: Date.now(), windowStarted: Date.now(), messagesInWindow: 0 };
    },

    async connected({ context, documentName, connection }) {
      sessions.add(context);
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

    async onLoadDocument({ documentName }) {
      const state = await api.load(documentName);
      // Stamp legacy persisted documents with canonical publication metadata before editing.
      // Re-saving an identical fingerprint does not advance the API's draft revision.
      if (state) await api.store(documentName, state, fingerprintHash(state, Y.encodeStateVectorFromUpdate(state)));
      return state ?? undefined;
    },

    async onStoreDocument({ documentName, document }) {
      const state = Y.encodeStateAsUpdate(document);
      const vector = Y.encodeStateVector(document);
      const fingerprint = fingerprintHash(state, vector);
      try {
        const savedAtUtc = await api.store(documentName, state, fingerprint);
        // New edits, including deletes (which need not advance a state vector),
        // can arrive during PUT. Never call such a newer state "saved".
        if (fingerprint === fingerprintHash(Y.encodeStateAsUpdate(document), Y.encodeStateVector(document))) {
          document.broadcastStateless(JSON.stringify({
            type: 'persisted', savedAtUtc,
            stateVector: Buffer.from(vector).toString('base64'),
            stateHash: createHash('sha256').update(state).digest('hex'),
            fingerprintHash: fingerprint,
          }));
        }
      } catch {
        document.broadcastStateless(JSON.stringify({ type: 'persistenceError', message: 'Indholdet er endnu ikke gemt i skyen. Prøver igen.' }));
        // Hocuspocus retains the dirty document and retries failed storage.
        throw new Error('Document persistence failed');
      }
    },

    async onDisconnect({ context }) { stopSession(context); },
    async onDestroy() { for (const context of sessions) stopSession(context); },
  });
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
