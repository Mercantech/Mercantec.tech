import { readFileSync } from 'node:fs';
import { notebookStateProjection } from './notebook-documents.mjs';
import { assertDrawingBytes, drawingStateProjection, MAX_DRAWING_BYTES } from './drawing-documents.mjs';

export const MAX_DOCUMENT_BYTES = 16 * 1024 * 1024;
// Match ASP.NET's default JWT clock skew so collaboration does not reject tokens
// the API would still accept after a short client/server time drift.
export const TOKEN_CLOCK_SKEW_MS = 5 * 60 * 1000;
const DOCUMENT_NAME = /^(notion|notebook|drawing):([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;
const route = domain => domain === 'notebook' ? 'notebooks' : domain === 'drawing' ? 'drawings' : 'notion';
const statePath = (domain, id) => `/api/internal/${route(domain)}/${domain === 'drawing' ? '' : 'documents/'}${id}/state`;

export function resolveServiceSecret(serviceSecret, serviceSecretFile) {
  if (typeof serviceSecret === 'string' && serviceSecret.trim()) return serviceSecret.trim();
  if (!serviceSecretFile) return '';
  try {
    const secret = readFileSync(serviceSecretFile, 'utf8').trim();
    if (secret.length < 32 || secret.length > 16384) throw new Error();
    return secret;
  } catch {
    throw new Error('The shared document service key could not be loaded');
  }
}

export function documentIdFromName(name) {
  const match = typeof name === 'string' && DOCUMENT_NAME.exec(name);
  if (!match) throw new Error('Invalid document name');
  return match[2].toLowerCase();
}

export function documentDomainFromName(name) {
  documentIdFromName(name);
  return name.slice(0, name.indexOf(':')).toLowerCase();
}

function decodeJwtPayload(part) {
  const normalized = String(part).replace(/-/g, '+').replace(/_/g, '/');
  const padded = normalized + '='.repeat((4 - (normalized.length % 4)) % 4);
  return JSON.parse(Buffer.from(padded, 'base64').toString('utf8'));
}

// Bounds collaboration session lifetime from the JWT exp claim. The ASP.NET API
// remains responsible for signature, issuer, audience and document permissions.
export function tokenExpiresAt(token, now = Date.now(), skewMs = TOKEN_CLOCK_SKEW_MS) {
  try {
    if (typeof token !== 'string' || token.length > 16384) throw new Error();
    const value = token.startsWith('Bearer ') ? token.slice(7) : token;
    const parts = value.split('.');
    if (parts.length !== 3) throw new Error();
    const { exp } = decodeJwtPayload(parts[1]);
    if (!Number.isSafeInteger(exp)) throw new Error();
    const expiresAt = exp * 1000;
    if (expiresAt + skewMs <= now) throw new Error();
    return expiresAt;
  } catch {
    throw new Error('A valid, unexpired access token is required');
  }
}

export function createApiClient({ apiBaseUrl, serviceSecret, fetchImpl = fetch, timeoutMs = 8000 }) {
  const base = new URL(apiBaseUrl);
  if (!['http:', 'https:'].includes(base.protocol) || base.username || base.password) {
    throw new Error('API_BASE_URL must be an HTTP(S) URL without credentials');
  }
  if (typeof serviceSecret !== 'string' || serviceSecret.length < 32) {
    throw new Error('NOTION_SERVICE_SECRET must contain at least 32 characters');
  }
  const request = async (path, options = {}) => {
    try {
      return await fetchImpl(new URL(path, base), {
        ...options,
        redirect: 'error',
        signal: AbortSignal.timeout(timeoutMs),
      });
    } catch {
      // Never propagate a URL, headers or a fetch error containing credentials.
      throw new Error('The document API is unavailable');
    }
  };
  const storageHeaders = { 'X-Notion-Service-Key': serviceSecret };

  return {
    async authorize(documentName, token) {
      const documentId = documentIdFromName(documentName);
      const domain = documentDomainFromName(documentName);
      const accessToken = typeof token === 'string' && token.startsWith('Bearer ') ? token.slice(7) : token;
      const expiresAt = tokenExpiresAt(accessToken);
      const canonicalName = `${domain}:${documentId}`;
      const response = await request(`/api/${route(domain)}/collaboration/authorize`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ documentName: canonicalName }),
      });
      if (!response.ok) throw new Error('Document access denied');
      const permission = await response.json().catch(() => null);
      if (permission?.documentId?.toLowerCase() !== documentId || (domain === 'drawing' ? permission.canRead !== true || typeof permission.canEdit !== 'boolean' : permission.canEdit !== true)) {
        throw new Error('Document access denied');
      }
      return { documentId, expiresAt, canRead: true, canEdit: permission.canEdit, accessVersion: permission.accessVersion };
    },

    async load(documentName) {
      const id = documentIdFromName(documentName);
      const domain = documentDomainFromName(documentName);
      const drawing = domain === 'drawing';
      const limit = drawing ? MAX_DRAWING_BYTES : MAX_DOCUMENT_BYTES;
      const response = await request(statePath(domain, id), {
        headers: storageHeaders,
      });
      // Only an explicit empty-success response creates a blank Y.Doc. A missing
      // document, broken database or bad service key must never overwrite it.
      if (response.status === 204) return null;
      if (!response.ok) throw new Error('Document state could not be loaded');
      const contentLength = Number(response.headers.get('content-length'));
      if (contentLength > limit) throw new Error('Document exceeds the storage limit');
      let state;
      if (drawing && response.body) {
        const reader = response.body.getReader(), chunks = []; let size = 0;
        try {
          for (;;) {
            const { value, done } = await reader.read();
            if (done) break;
            size += value.byteLength;
            if (size > limit) { await reader.cancel(); throw new Error('Document exceeds the storage limit'); }
            chunks.push(value);
          }
          state = new Uint8Array(Buffer.concat(chunks));
        } finally { reader.releaseLock(); }
      } else state = new Uint8Array(await response.arrayBuffer());
      if (state.length === 0 || state.length > limit) {
        throw new Error('Invalid document state');
      }
      if (drawing) drawingStateProjection(state);
      return state;
    },

    async prepareMcpChange(pageId, change) {
      const response = await request(`/api/internal/notion/mcp/changes/prepare?pageId=${encodeURIComponent(pageId)}`, {
        method: 'POST', headers: { ...storageHeaders, 'Content-Type': 'application/json' }, body: JSON.stringify(change),
      });
      if (!response.ok) throw new Error('MCP change audit could not be prepared');
      return response.json().catch(() => ({}));
    },

    async store(documentName, state, fingerprint, mcpChange = undefined) {
      const id = documentIdFromName(documentName);
      const domain = documentDomainFromName(documentName), drawing = domain === 'drawing';
      if (!(state instanceof Uint8Array) || state.length === 0 || state.length > MAX_DOCUMENT_BYTES) {
        throw new Error('Document exceeds the storage limit');
      }
      if (drawing) assertDrawingBytes(state);
      if (typeof fingerprint !== 'string' || !/^[0-9a-f]{64}$/.test(fingerprint)) {
        throw new Error('A canonical document fingerprint is required');
      }
      const projected = drawing ? drawingStateProjection(state) : notebookStateProjection(state, domain);
      const projection = domain === 'notion' ? { stateBase64: projected.stateBase64, fingerprint: projected.fingerprint, drawingIds: projected.drawingIds, drawingBlocks: projected.drawingBlocks, references: projected.references } : projected;
      if (mcpChange) {
        if (domain !== 'notion') throw new Error('MCP only supports teaching documents');
        projection.mcpChange = mcpChange;
      }
      if (projection.fingerprint !== fingerprint) throw new Error(`${drawing ? 'Drawing' : domain === 'notebook' ? 'Notebook' : 'Document'} fingerprint mismatch`);
      const response = await request(statePath(domain, id), {
        method: 'PUT',
        headers: { ...storageHeaders, 'Content-Type': 'application/json', 'X-Notion-State-Fingerprint': fingerprint },
        body: JSON.stringify(projection),
      });
      if (!response.ok) throw new Error('Document state could not be saved');
      const result = await response.json().catch(() => null);
      if (mcpChange) {
        if (result?.mcpOperationId !== mcpChange.operationId || typeof result.savedAtUtc !== 'string') throw new Error('MCP storage acknowledgement is invalid');
        return { savedAtUtc: result.savedAtUtc, mcpOperationId: result.mcpOperationId };
      }
      if (drawing) {
        if (!Number.isSafeInteger(result?.revision) || result.revision < 0 || result.fingerprint !== fingerprint || typeof result.savedAtUtc !== 'string') throw new Error('Drawing storage acknowledgement is invalid');
        return { savedAtUtc: result.savedAtUtc, revision: result.revision, fingerprint: result.fingerprint, accessVersion: result.accessVersion };
      }
      return typeof result?.savedAtUtc === 'string' ? result.savedAtUtc : new Date().toISOString();
    },
  };
}
