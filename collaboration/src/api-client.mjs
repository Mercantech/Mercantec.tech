import { readFileSync } from 'node:fs';

export const MAX_DOCUMENT_BYTES = 16 * 1024 * 1024;
const DOCUMENT_NAME = /^notion:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

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
  return match[1].toLowerCase();
}

// This only bounds the lifetime of an already API-verified JWT. The ASP.NET API
// remains responsible for signature, issuer, audience and document permissions.
export function tokenExpiresAt(token, now = Date.now()) {
  try {
    if (typeof token !== 'string' || token.length > 16384) throw new Error();
    const parts = token.split('.');
    if (parts.length !== 3) throw new Error();
    const { exp } = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
    if (!Number.isSafeInteger(exp) || exp * 1000 <= now) throw new Error();
    return exp * 1000;
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
      const expiresAt = tokenExpiresAt(token);
      const response = await request('/api/notion/collaboration/authorize', {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ documentName }),
      });
      if (!response.ok) throw new Error('Document access denied');
      const permission = await response.json().catch(() => null);
      if (permission?.documentId?.toLowerCase() !== documentId || permission.canEdit !== true) {
        throw new Error('Document access denied');
      }
      return { documentId, expiresAt };
    },

    async load(documentName) {
      const id = documentIdFromName(documentName);
      const response = await request(`/api/internal/notion/documents/${id}/state`, {
        headers: storageHeaders,
      });
      // Only an explicit empty-success response creates a blank Y.Doc. A missing
      // document, broken database or bad service key must never overwrite it.
      if (response.status === 204) return null;
      if (!response.ok) throw new Error('Document state could not be loaded');
      const contentLength = Number(response.headers.get('content-length'));
      if (contentLength > MAX_DOCUMENT_BYTES) throw new Error('Document exceeds the storage limit');
      const state = new Uint8Array(await response.arrayBuffer());
      if (state.length === 0 || state.length > MAX_DOCUMENT_BYTES) {
        throw new Error('Invalid document state');
      }
      return state;
    },

    async store(documentName, state) {
      const id = documentIdFromName(documentName);
      if (!(state instanceof Uint8Array) || state.length === 0 || state.length > MAX_DOCUMENT_BYTES) {
        throw new Error('Document exceeds the storage limit');
      }
      const response = await request(`/api/internal/notion/documents/${id}/state`, {
        method: 'PUT',
        headers: { ...storageHeaders, 'Content-Type': 'application/octet-stream' },
        body: state,
      });
      if (!response.ok) throw new Error('Document state could not be saved');
      const result = await response.json().catch(() => null);
      return typeof result?.savedAtUtc === 'string' ? result.savedAtUtc : new Date().toISOString();
    },
  };
}
