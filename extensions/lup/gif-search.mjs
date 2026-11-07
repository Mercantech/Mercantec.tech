import { validId } from './core.mjs';
import { normalizeSearchImage, normalizeSearchQuery, MAX_SEARCH_PAGE } from './image-search.mjs';

const API = 'https://api.openverse.org/v1/images/';
const LIMIT = 20;
export function isGifResultId(value) {
  return typeof value === 'string' && value.startsWith('gif:') && validId(value.slice(4));
}

function gifUrl(value) {
  if (typeof value !== 'string' || value.length > 2048) return null;
  try {
    const url = new URL(value);
    if (url.protocol !== 'https:' || url.hostname !== 'upload.wikimedia.org' || url.port || url.username || url.password || url.search || url.hash
        || !/^\/wikipedia\/commons\/[a-f0-9]\/[a-f0-9]{2}\/[^/]+\.gif$/iu.test(url.pathname)) return null;
    const filename = decodeURIComponent(url.pathname.split('/').at(-1));
    return !filename.includes('/') && !filename.includes('\\') && !/[\u0000-\u001f\u007f]/u.test(filename) ? url.href : null;
  } catch { return null; }
}

export function normalizeGifSearchResult(value) {
  if (!value || typeof value !== 'object') return null;
  const id = isGifResultId(value.id) ? value.id.slice(4) : value.id;
  if (!validId(id)) return null;
  const base = normalizeSearchImage({ ...value, id });
  const imageUrl = gifUrl(value.url ?? value.imageUrl ?? value.thumbnailUrl);
  if (!base || !imageUrl) return null;
  if (Number(value.filesize) > 8 * 1024 * 1024 || (Number(value.width) > 0 && Number(value.height) > 0 && Number(value.width) * Number(value.height) > 4 * 1024 * 1024)) return null;
  if (value.filetype !== undefined && value.filetype !== null && value.filetype !== 'gif') return null;
  return { ...base, id: `gif:${id}`, provider: 'openverse-gif', thumbnailUrl: imageUrl, imageUrl, animated: true };
}

export async function searchGifs(value = '', page = 1) {
  const query = typeof value === 'string' && /^[ ]*$/u.test(value) ? '' : normalizeSearchQuery(value);
  if (query === null || !Number.isInteger(page) || page < 1 || page > MAX_SEARCH_PAGE)
    throw new Error('Skriv et søgeord på 2–100 tegn, eller udforsk GIF’erne uden et søgeord.');
  const url = new URL(API);
  url.search = new URLSearchParams({ q: query || 'animated icon', extension: 'gif', source: 'wikimedia',
    license: 'cc0,pdm', mature: 'false', page_size: String(LIMIT), page: String(page) });
  let response;
  try {
    response = await fetch(url.href, { headers: { Accept: 'application/json' }, credentials: 'omit', cache: 'no-store',
      redirect: 'error', referrerPolicy: 'no-referrer', signal: AbortSignal.timeout(15_000) });
  } catch { throw new Error('GIF-søgningen kunne ikke kontaktes. Prøv igen om et øjeblik.'); }
  if (response.status === 429) throw new Error('Openverse har nået sin søgegrænse. Vent lidt, og prøv igen senere.');
  if (!response.ok) throw new Error('GIF-søgningen er ikke tilgængelig lige nu. Prøv igen senere.');
  const reader = response.body?.getReader();
  if (!reader || Number(response.headers.get('content-length')) > 1024 * 1024) throw new Error('GIF-søgesvaret kunne ikke læses. Prøv igen.');
  const chunks = []; let length = 0;
  try {
    while (true) {
      const { value, done } = await reader.read(); if (done) break;
      length += value.byteLength;
      if (length > 1024 * 1024) { await reader.cancel(); throw new Error('GIF-søgesvaret var for stort. Prøv et mere præcist søgeord.'); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(length); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  let data;
  try { data = JSON.parse(new TextDecoder().decode(bytes)); }
  catch { throw new Error('GIF-søgetjenesten gav et ugyldigt svar. Prøv igen.'); }
  if (!data || !Array.isArray(data.results) || data.results.length > LIMIT) throw new Error('GIF-søgetjenesten gav et ugyldigt svar. Prøv igen.');
  const seen = new Set();
  const items = data.results.map(normalizeGifSearchResult).filter(item => item && !seen.has(item.id) && seen.add(item.id));
  return { mode: 'gifs', query, page, items, nextPage: Number.isInteger(data.page_count) && data.page_count > page && page < MAX_SEARCH_PAGE ? page + 1 : null };
}
