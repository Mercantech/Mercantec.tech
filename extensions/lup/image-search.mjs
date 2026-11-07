import { validId } from './core.mjs';

export const MAX_SEARCH_PAGE = 10;
const PAGE_SIZE = 20;
const API = 'https://api.openverse.org/v1/images/';
const LICENSES = {
  cc0: { label: 'CC0', url: 'https://creativecommons.org/publicdomain/zero/1.0/' },
  pdm: { label: 'Public domain', url: 'https://creativecommons.org/publicdomain/mark/1.0/' },
};

export function normalizeSearchQuery(value) {
  if (typeof value !== 'string' || /[\u0000-\u001f\u007f]/u.test(value)) return null;
  const query = value.replace(/\s+/gu, ' ').trim();
  return query.length >= 2 && query.length <= 100 ? query : null;
}

function text(value, limit) {
  return typeof value === 'string' ? value.replace(/<[^>]*>/gu, '').replace(/[\u0000-\u001f\u007f]/gu, ' ')
    .replace(/\s+/gu, ' ').trim().slice(0, limit) : '';
}

function sourceLink(value) {
  if (typeof value !== 'string' || value.length > 2048) return null;
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password ? url.href : null;
  } catch { return null; }
}

export function normalizeSearchImage(value) {
  if (!value || typeof value !== 'object' || !validId(value.id) || !Object.hasOwn(LICENSES, value.license)) return null;
  const sourceUrl = sourceLink(value.foreign_landing_url ?? value.sourceUrl);
  if (!sourceUrl || value.mature === true) return null;
  const license = LICENSES[value.license];
  return {
    id: value.id,
    title: text(value.title, 160) || 'Billede uden titel',
    creator: text(value.creator, 120),
    license: value.license,
    licenseLabel: license.label,
    licenseUrl: license.url,
    sourceUrl,
    // The proxy endpoint is rebuilt from a validated UUID; provider URLs never become download targets.
    thumbnailUrl: `${API}${value.id}/thumb/`,
    imageUrl: `${API}${value.id}/thumb/?full_size=true`,
  };
}

async function readJson(response) {
  const max = 1024 * 1024;
  if (Number(response.headers.get('content-length')) > max) throw new Error('Søgesvaret var for stort. Prøv en mere præcis søgning.');
  const reader = response.body?.getReader();
  if (!reader) throw new Error('Søgetjenesten gav ikke et læsbart svar. Prøv igen.');
  const chunks = []; let length = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      length += value.byteLength;
      if (length > max) { await reader.cancel(); throw new Error('Søgesvaret var for stort. Prøv en mere præcis søgning.'); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(length); let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  try { return JSON.parse(new TextDecoder().decode(bytes)); }
  catch { throw new Error('Søgetjenesten gav et ugyldigt svar. Prøv igen.'); }
}

export async function searchImages(value, page = 1) {
  const query = normalizeSearchQuery(value);
  if (!query) throw new Error('Skriv 2–100 tegn i søgefeltet.');
  if (!Number.isInteger(page) || page < 1 || page > MAX_SEARCH_PAGE) throw new Error('Søgesiden er ugyldig. Start en ny søgning.');
  const url = new URL(API);
  url.search = new URLSearchParams({ q: query, license: 'cc0,pdm', mature: 'false', page_size: String(PAGE_SIZE), page: String(page) });
  let response;
  try {
    response = await fetch(url.href, {
      headers: { Accept: 'application/json' }, credentials: 'omit', referrerPolicy: 'no-referrer',
      redirect: 'error', cache: 'no-store', signal: AbortSignal.timeout(15_000),
    });
  } catch { throw new Error('Openverse kunne ikke kontaktes. Kontrollér forbindelsen, og prøv igen.'); }
  if (response.status === 429) throw new Error('Openverse har nået sin søgegrænse. Vent lidt, og prøv igen senere.');
  if (!response.ok) throw new Error('Openverse kunne ikke gennemføre søgningen lige nu. Prøv igen senere.');
  const data = await readJson(response);
  if (!data || !Array.isArray(data.results) || data.results.length > PAGE_SIZE)
    throw new Error('Søgetjenesten gav et ugyldigt svar. Prøv igen.');
  const seen = new Set();
  const items = data.results.map(normalizeSearchImage).filter(item => item && !seen.has(item.id) && seen.add(item.id));
  const nextPage = Number.isInteger(data.page_count) && data.page_count > page && page < MAX_SEARCH_PAGE ? page + 1 : null;
  return { query, page, items, nextPage };
}
