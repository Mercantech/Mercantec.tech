export const MAX_BYTES = 256 * 1024;
export const MAX_DOWNLOAD_BYTES = 8 * 1024 * 1024;
export const MAX_PIXELS = 16 * 1024 * 1024;
export const TTL_MS = 30 * 60 * 1000;
export const JOB_TTL_MS = TTL_MS;
export const MIME_TYPES = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp']);

export const validId = value => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(value);

export function sanitizeName(value) {
  if (typeof value !== 'string') return 'emoji';
  let name = value.replaceAll('\\', '/').split('/').at(-1).replace(/\.[a-zA-Z0-9]{1,5}$/, '');
  name = name.toLowerCase().replaceAll('æ', 'ae').replaceAll('ø', 'o').normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '').replace(/[^a-z0-9_-]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 32).replace(/_+$/g, '');
  return name.length >= 2 ? name : 'emoji';
}

export function sourceUrl(value) {
  if (typeof value !== 'string' || value.length > 8192) return null;
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password ? url : null;
  } catch { return null; }
}

export function originPattern(value) {
  const url = sourceUrl(value);
  return url ? url.origin + '/*' : null;
}

export function strictBase64Size(value) {
  if (typeof value !== 'string' || !value.length || value.length > Math.ceil(MAX_BYTES / 3) * 4
    || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(value)) return 0;
  try {
    const bytes = atob(value);
    return btoa(bytes) === value ? bytes.length : 0;
  } catch { return 0; }
}

export function validDraft(draft, now = Date.now()) {
  if (!draft || typeof draft !== 'object' || !validId(draft.id)
    || typeof draft.name !== 'string' || !/^[a-z0-9_-]{2,32}$/.test(draft.name)
    || !MIME_TYPES.has(draft.contentType) || !Number.isInteger(draft.destinationTabId) || draft.destinationTabId <= 0
    || !Number.isFinite(draft.expiresAt) || draft.expiresAt <= now || draft.expiresAt > now + TTL_MS) return false;
  const size = strictBase64Size(draft.base64);
  return size > 0 && size <= MAX_BYTES;
}

export function trustedDestination(sender, id) {
  if (!validId(id) || !Number.isInteger(sender?.tab?.id) || sender.tab.id <= 0) return false;
  try {
    const url = new URL(sender.url);
    return url.origin === 'https://lup.dk' && url.pathname === '/emojis'
      && url.searchParams.getAll('lupImport').length === 1 && url.searchParams.get('lupImport') === id;
  } catch { return false; }
}

export const validateDraftAccess = (draft, sender, id, now = Date.now()) => validDraft(draft, now)
  && draft.id === id && trustedDestination(sender, id) && draft.destinationTabId === sender.tab.id;

// LUP removes its import query as soon as upload succeeds. Completion may arrive
// after that history change; byte delivery still requires the full query check.
export function validateDraftCompletion(draft, sender, id, now = Date.now()) {
  if (!validDraft(draft, now) || draft.id !== id || sender?.tab?.id !== draft.destinationTabId) return false;
  try {
    const url = new URL(sender.url);
    return url.origin === 'https://lup.dk' && url.pathname === '/emojis';
  } catch { return false; }
}

export const pageMessage = value => Boolean(value && typeof value === 'object' && value.source === 'lup-emoji-page'
  && ['request', 'complete'].includes(value.type) && validId(value.id));
