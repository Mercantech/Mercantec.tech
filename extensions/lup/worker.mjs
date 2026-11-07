import { JOB_TTL_MS, TTL_MS, validId, sourceUrl, validDraft, validateDraftAccess, validateDraftCompletion } from './core.mjs';
import { searchImages, normalizeSearchQuery, MAX_SEARCH_PAGE } from './image-search.mjs';
import { searchGifs, normalizeGifSearchResult } from './gif-search.mjs';
import { captureSelectedImage } from './page-capture.mjs';
import { IMPORT_ISSUES } from './import-errors.mjs';
import { putCapture, getCapture, deleteCapture, pruneCaptures } from './capture-store.mjs';

const MENU_ID = 'lup-add-emoji';
const MAX_PENDING = 8;
const MAX_SEARCH_RESULTS = 200;
const ACCESS_KEY = 'lup.upload.access';
const KEY_PATTERN = /^lup_[A-Za-z0-9_-]{43}$/;
const fail = (message, retry = false) => ({ ok: false, message, retry });
const importFailure = code => ({ ok: false, code });
const activeUploads = new Set();
let imageJobQueue = Promise.resolve();
const publicError = error => error instanceof TypeError ? 'LUP kunne ikke kontaktes. Kontrollér forbindelsen, og prøv igen.'
  : error?.name === 'TimeoutError' ? 'LUP svarede ikke i tide. Prøv igen senere.' : error?.message || 'Forbindelsen kunne ikke gennemføres.';

function ownPopup(sender) {
  try {
    const actual = new URL(sender.url), expected = new URL(chrome.runtime.getURL('popup.html'));
    return actual.protocol === expected.protocol && actual.host === expected.host && actual.pathname === expected.pathname;
  } catch { return false; }
}

function ownSearch(sender) {
  if (!Number.isInteger(sender?.tab?.id) || sender.tab.id <= 0) return false;
  try {
    const actual = new URL(sender.url), expected = new URL(chrome.runtime.getURL('search.html'));
    return actual.protocol === expected.protocol && actual.host === expected.host && actual.pathname === expected.pathname;
  } catch { return false; }
}

function cachedSearchItem(value) {
  if (typeof value?.id === 'string' && value.id.startsWith('gif:')) return normalizeGifSearchResult(value);
  if (!value || !validId(value.id) || !['cc0', 'pdm'].includes(value.license)
    || typeof value.title !== 'string' || !value.title.trim() || value.title.length > 240
    || typeof value.creator !== 'string' || value.creator.length > 240
    || typeof value.licenseLabel !== 'string' || !value.licenseLabel.trim() || value.licenseLabel.length > 80) return null;
  const thumbnailUrl = `https://api.openverse.org/v1/images/${value.id}/thumb/`;
  const imageUrl = thumbnailUrl + '?full_size=true';
  const attributionSource = sourceUrl(value.sourceUrl);
  const licenseUrl = value.license === 'cc0'
    ? 'https://creativecommons.org/publicdomain/zero/1.0/' : 'https://creativecommons.org/publicdomain/mark/1.0/';
  if (value.thumbnailUrl !== thumbnailUrl || value.imageUrl !== imageUrl || value.licenseUrl !== licenseUrl || !attributionSource) return null;
  return { id: value.id, title: value.title, creator: value.creator, license: value.license,
    licenseLabel: value.licenseLabel, licenseUrl, sourceUrl: attributionSource.href, thumbnailUrl, imageUrl };
}

async function cacheSearchItems(items) {
  await prune();
  const expiresAt = Date.now() + JOB_TTL_MS;
  const entries = Object.fromEntries(items.map(item => [`search:${item.id}`, { ...item, expiresAt }]));
  if (items.length) await chrome.storage.session.set(entries);
  const cached = Object.entries(await chrome.storage.session.get(null)).filter(([key]) => key.startsWith('search:'))
    .sort((a, b) => a[1].expiresAt - b[1].expiresAt);
  if (cached.length > MAX_SEARCH_RESULTS) await chrome.storage.session.remove(cached.slice(0, cached.length - MAX_SEARCH_RESULTS).map(([key]) => key));
}

async function searchMessage(message, sender) {
  if (!ownSearch(sender)) return fail('Billedsøgning kan kun bruges fra LUP-udvidelsens søgeside.');
  if (message.type === 'search:query') {
    const mode = message.mode === undefined ? 'images' : message.mode;
    if (!['images', 'gifs'].includes(mode)) return fail('Vælg billeder eller GIF’er.');
    const blankGifQuery = mode === 'gifs' && typeof message.query === 'string'
      && !/[\u0000-\u001f\u007f]/u.test(message.query) && !message.query.trim();
    const query = blankGifQuery ? '' : normalizeSearchQuery(message.query);
    if (query === null || !Number.isInteger(message.page) || message.page < 1 || message.page > MAX_SEARCH_PAGE)
      return fail('Skriv et søgeord, og vælg en gyldig resultatside.');
    try {
      const response = await (mode === 'gifs' ? searchGifs(query, message.page) : searchImages(query, message.page));
      const items = (Array.isArray(response.items) ? response.items : []).map(cachedSearchItem)
        .filter(item => item && (item.id.startsWith('gif:') ? mode === 'gifs' : mode === 'images')).slice(0, MAX_SEARCH_RESULTS);
      await cacheSearchItems(items);
      return { ok: true, query, page: message.page, mode, items, nextPage: response.nextPage };
    } catch (error) {
      return fail(error instanceof TypeError ? 'Billedsøgningen kunne ikke kontaktes. Prøv igen senere.'
        : error?.name === 'TimeoutError' ? 'Billedsøgningen svarede ikke i tide. Prøv igen.'
          : error?.message || 'Billederne kunne ikke hentes. Prøv igen.');
    }
  }
  const validResultId = validId(message.resultId) || (typeof message.resultId === 'string'
    && message.resultId.startsWith('gif:') && validId(message.resultId.slice(4)));
  if (message.type !== 'search:select' || !validResultId) return fail('Vælg et billede fra søgeresultaterne.');
  const stored = (await chrome.storage.session.get(`search:${message.resultId}`))[`search:${message.resultId}`];
  const item = cachedSearchItem(stored);
  if (!item || !Number.isFinite(stored.expiresAt) || stored.expiresAt <= Date.now()
    || stored.expiresAt > Date.now() + JOB_TTL_MS) return fail('Søgeresultatet er udløbet. Søg efter billedet igen.');
  try {
    const { imageUrl, ...searchSource } = item;
    return { ok: true, ...await openImageJob(imageUrl, searchSource, sender.tab.id) };
  } catch { return fail('Billedvinduet kunne ikke åbnes. Prøv igen.'); }
}

async function apiRequest(path, key, init = {}) {
  const response = await fetch('https://lup.dk/api/extension/' + path, {
    ...init, headers: { Authorization: 'Bearer ' + key }, credentials: 'omit', cache: 'no-store',
    redirect: 'error', referrerPolicy: 'no-referrer', signal: AbortSignal.timeout(30_000),
  });
  let data = null;
  try { data = await response.json(); } catch { }
  if (!response.ok) {
    const messages = {
      extension_access_invalid: 'Forbindelsen er udløbet eller tilbagekaldt. Tilføj en ny uploadnøgle i indstillingerne.',
      extension_access_forbidden: 'Denne forbindelse har ikke adgang til at tilføje emojis.',
      name_taken: 'Navnet bruges allerede. Vælg et andet kort navn.',
      emoji_limit: 'Emojibiblioteket er fyldt. Fjern en emoji på LUP.dk først.',
      extension_rate_limit: 'Der er gemt mange emojis. Vent et minut, og prøv igen.',
      invalid_name: 'Brug 2–32 små bogstaver, tal, _ eller - i navnet.',
      file_too_large: 'Billedet skal være på højst 256 KB.',
      invalid_file_signature: 'Billedet kunne ikke læses. Vælg det igen.',
      object_storage_disabled: 'LUPs billedlager er ikke tilgængeligt lige nu. Prøv igen senere.',
    };
    throw new Error(messages[data?.error] ?? (response.status === 401 || response.status === 403
      ? 'Uploadnøglen virker ikke. Kontrollér forbindelsen i indstillingerne.' : 'LUP kunne ikke gemme billedet. Prøv igen senere.'));
  }
  return data;
}

async function connectionStatus() {
  const key = (await chrome.storage.local.get(ACCESS_KEY))[ACCESS_KEY];
  if (!KEY_PATTERN.test(key ?? '')) return { ok: true, connected: false };
  try {
    const state = await apiRequest('status', key);
    return { ok: true, connected: state?.connected === true && state.scope === 'emoji:create', email: state?.email };
  } catch (error) { return { ok: true, connected: false, message: publicError(error) }; }
}

async function settingsMessage(message, sender) {
  if (!ownPopup(sender)) return fail('Indstillinger kan kun ændres i LUP-udvidelsen.');
  if (message.type === 'settings:status') return connectionStatus();
  if (message.type === 'settings:clear') { await chrome.storage.local.remove(ACCESS_KEY); return { ok: true }; }
  if (message.type !== 'settings:save' || typeof message.key !== 'string' || !KEY_PATTERN.test(message.key.trim()))
    return fail('Indsæt din personlige LUP-uploadnøgle.');
  const key = message.key.trim();
  try {
    const state = await apiRequest('status', key);
    if (state?.connected !== true || state.scope !== 'emoji:create') return fail('Nøglen giver ikke adgang til emoji-upload.');
    // Local profile only, never sync storage or a source-controlled file.
    await chrome.storage.local.set({ [ACCESS_KEY]: key });
    return { ok: true, connected: true, email: state.email };
  } catch (error) { return fail(publicError(error)); }
}

async function prune() {
  const entries = await chrome.storage.session.get(null);
  const obsolete = Object.entries(entries).filter(([key, item]) => /^(pending|job|search):/.test(key)
    && (!item || !Number.isFinite(item.expiresAt) || item.expiresAt <= Date.now())).map(([key]) => key);
  if (obsolete.length) await chrome.storage.session.remove(obsolete);
  const active = new Set(Object.entries(entries).filter(([key, item]) => key.startsWith('job:') && item?.expiresAt > Date.now()).map(([, item]) => item.id));
  await pruneCaptures(active).catch(() => {});
}

async function makeRoom(prefix) {
  await prune();
  const entries = await chrome.storage.session.get(null);
  const items = Object.entries(entries).filter(([key]) => key.startsWith(prefix)).sort((a, b) => a[1].expiresAt - b[1].expiresAt);
  if (items.length >= MAX_PENDING) {
    const removed = items.slice(0, items.length - MAX_PENDING + 1);
    await chrome.storage.session.remove(removed.map(([key]) => key));
    if (prefix === 'job:') await Promise.all(removed.map(([, item]) => deleteCapture(item.id).catch(() => {})));
  }
}

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.storage.local.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({ id: MENU_ID, title: 'Tilføj som emoji på LUP.dk', contexts: ['image'] });
  await prune();
});
chrome.runtime.onStartup.addListener(() => { void prune(); });

export function openImageJob(srcUrl, searchSource = null, searchTabId = null, context = null) {
  // Multiple search tabs may select images at once. Keep pruning and creation
  // together so concurrent selections cannot exceed the eight-job bound.
  const operation = imageJobQueue.then(() => createImageJob(srcUrl, searchSource, searchTabId, context));
  imageJobQueue = operation.catch(() => {});
  return operation;
}

async function createImageJob(srcUrl, searchSource, searchTabId, context) {
  await makeRoom('job:');
  const id = crypto.randomUUID();
  const selected = sourceUrl(srcUrl);
  const job = { id, srcUrl: selected?.href ?? null, expiresAt: Date.now() + JOB_TTL_MS, prepareTabId: null };
  if (searchSource) job.searchSource = searchSource;
  if (searchSource && Number.isInteger(searchTabId) && searchTabId > 0) job.searchTabId = searchTabId;
  if (context) {
    for (const name of ['sourceTabId', 'sourceFrameId', 'sourceFrameOrigin', 'contextSrcUrl', 'candidates', 'captureIssue', 'captureMethod', 'canvasAvailable', 'sourceHost', 'sourceAltText'])
      if (context[name] !== undefined) job[name] = context[name];
  }
  await chrome.storage.session.set({ [`job:${id}`]: job });
  if (context?.capture) {
    try {
      await putCapture(id, context.capture.blob, context.capture, job.expiresAt);
      job.captureAvailable = true;
    } catch { job.captureIssue = { code: 'LUP-IMP-004' }; }
    await chrome.storage.session.set({ [`job:${id}`]: job });
  }
  try {
    const tab = await chrome.tabs.create({ url: chrome.runtime.getURL(`prepare.html?job=${id}`) });
    await chrome.storage.session.set({ [`job:${id}`]: { ...job, prepareTabId: tab.id } });
    return { id };
  } catch {
    await chrome.storage.session.remove(`job:${id}`);
    await deleteCapture(id).catch(() => {});
    throw new Error('Billedvinduet kunne ikke åbnes.');
  }
}

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== MENU_ID) return;
  void openContextImageJob(info, tab).catch(() => console.error('LUP kunne ikke åbne billedkladden.'));
});

const CAPTURE_LIMIT = 8 * 1024 * 1024;
const CAPTURE_METHODS = new Set(['page-fetch', 'embedded', 'canvas']);
function safeIssue(issue, fallback = 'LUP-IMP-002') {
  const code = typeof issue?.code === 'string' && Object.hasOwn(IMPORT_ISSUES, issue.code) ? issue.code : fallback;
  const status = issue?.httpStatus ?? issue?.context?.status ?? issue?.context?.httpStatus;
  return { code, ...(Number.isInteger(status) && status >= 100 && status <= 599 ? { context: { httpStatus: status } } : {}) };
}
function scriptFailure(error) {
  return safeIssue(null, /No tab with id|No frame with id|No document with id|frame.*removed|tab.*closed|Cannot find frame/i.test(error?.message ?? '') ? 'LUP-IMP-006' : 'LUP-IMP-002');
}
function capturedCandidates(values, original) {
  const result = [];
  for (const value of [original, ...(Array.isArray(values) ? values.slice(0, 6) : [])]) {
    const url = sourceUrl(value);
    if (url && !result.includes(url.href)) result.push(url.href);
    if (result.length === 6) break;
  }
  return result;
}

export function validateCapturedImage(value) {
  if (!value?.ok || typeof value.base64 !== 'string' || !value.base64.length || value.base64.length > Math.ceil(CAPTURE_LIMIT / 3) * 4
      || !CAPTURE_METHODS.has(value.method) || value.base64.length % 4 !== 0) return null;
  try {
    const binary = atob(value.base64);
    if (!binary.length || binary.length > CAPTURE_LIMIT || btoa(binary) !== value.base64) return null;
    const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
    const ascii = (start, length) => String.fromCharCode(...bytes.subarray(start, start + length));
    let type = null;
    if (bytes.length >= 8 && bytes[0] === 137 && ascii(1, 7) === 'PNG\r\n\x1a\n') type = 'image/png';
    else if (bytes.length >= 3 && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255) type = 'image/jpeg';
    else if (bytes.length >= 13 && ['GIF87a', 'GIF89a'].includes(ascii(0, 6))) type = 'image/gif';
    else if (bytes.length >= 12 && ascii(0, 4) === 'RIFF' && ascii(8, 4) === 'WEBP') type = 'image/webp';
    else if (bytes.length >= 16 && ascii(4, 4) === 'ftyp' && /avif|avis/.test(ascii(8, Math.min(56, bytes.length - 8)))) type = 'image/avif';
    if (!type || (value.method === 'canvas' && type !== 'image/png')) return null;
    return { blob: new Blob([bytes], { type }), method: value.method, animationLost: value.animationLost === true || value.method === 'canvas' };
  } catch { return null; }
}

async function captureInPage(tabId, frameId, srcUrl, capturePixels = false) {
  if (!Number.isInteger(tabId) || tabId <= 0 || !Number.isInteger(frameId) || frameId < 0 || typeof srcUrl !== 'string' || !srcUrl.length
      || srcUrl.length > Math.ceil(CAPTURE_LIMIT / 3) * 4 + 256 || typeof chrome.scripting?.executeScript !== 'function')
    return { issue: { code: 'LUP-IMP-002' } };
  let timeout;
  try {
    const results = await Promise.race([
      chrome.scripting.executeScript({ target: { tabId, frameIds: [frameId] }, func: captureSelectedImage, args: [{ srcUrl, capturePixels }] }),
      new Promise((_, reject) => { timeout = setTimeout(() => reject(new Error('capture_timeout')), 8_000); }),
    ]);
    const returned = Array.isArray(results) ? results.find(item => item.frameId === frameId)?.result : null;
    if (!returned || typeof returned !== 'object') return { issue: { code: 'LUP-IMP-002' } };
    const capture = validateCapturedImage(returned);
    return { ...returned, capture, issue: returned.ok && !capture
      ? { code: 'LUP-IMP-005' } : returned.issue ? safeIssue(returned.issue) : undefined };
  } catch (error) { return { issue: error?.message === 'capture_timeout' ? { code: 'LUP-IMP-109' } : scriptFailure(error) }; }
  finally { clearTimeout(timeout); }
}

export async function openContextImageJob(info, tab) {
  const rawSource = typeof info?.srcUrl === 'string' ? info.srcUrl : '';
  const sourceTabId = Number.isInteger(tab?.id) && tab.id > 0 ? tab.id : null;
  const sourceFrameId = Number.isInteger(info?.frameId) && info.frameId >= 0 ? info.frameId : 0;
  const captured = await captureInPage(sourceTabId, sourceFrameId, rawSource);
  const selected = sourceUrl(rawSource);
  let contextSrcUrl = selected?.href ?? null;
  if (!contextSrcUrl && rawSource.length <= 8192) {
    try { if (new URL(rawSource).protocol === 'blob:') contextSrcUrl = rawSource; } catch { }
  }
  const framePage = sourceUrl(info?.frameUrl) || (sourceFrameId === 0 ? sourceUrl(info?.pageUrl ?? tab?.url) : null);
  const sourcePage = framePage || sourceUrl(info?.pageUrl ?? tab?.url);
  const context = { sourceTabId, sourceFrameId, contextSrcUrl,
    ...(framePage ? { sourceFrameOrigin: framePage.origin } : {}),
    candidates: capturedCandidates(captured.candidates, selected?.href),
    canvasAvailable: captured.canvasAvailable === true, sourceHost: selected?.hostname || sourcePage?.hostname || '',
    sourceAltText: typeof captured.altText === 'string' ? captured.altText.replace(/[\u0000-\u001f\u007f]/g, ' ').slice(0, 240) : '' };
  if (captured.capture) { context.capture = captured.capture; context.captureMethod = captured.capture.method; }
  if (captured.issue) context.captureIssue = captured.issue;
  return openImageJob(selected?.href ?? null, null, null, context);
}

function prepareSender(sender, id, job) {
  if (!validId(id) || !job || job.id !== id || job.expiresAt <= Date.now() || sender?.tab?.id !== job.prepareTabId) return false;
  try {
    const url = new URL(sender.url);
    const expected = new URL(chrome.runtime.getURL('prepare.html'));
    return url.protocol === expected.protocol && url.host === expected.host && url.pathname === expected.pathname
      && url.searchParams.getAll('job').length === 1 && url.searchParams.get('job') === id;
  } catch { return false; }
}

export async function handleMessage(message, sender) {
  if (typeof message?.type === 'string' && message.type.startsWith('settings:')) return settingsMessage(message, sender);
  if (typeof message?.type === 'string' && message.type.startsWith('search:')) return searchMessage(message, sender);
  if (!message || typeof message !== 'object' || !validId(message.id)) return typeof message?.type === 'string' && message.type.startsWith('prepare:')
    ? { ...fail('Importen er ugyldig. Vælg billedet igen.'), code: 'LUP-IMP-001' } : fail('Importen er ugyldig. Vælg billedet igen.');
  if (['prepare:get', 'prepare:continue', 'prepare:status', 'prepare:upload', 'prepare:source', 'prepare:capture'].includes(message.type)) {
    const key = `job:${message.id}`;
    const job = (await chrome.storage.session.get(key))[key];
    if (job && job.prepareTabId === null && job.expiresAt > Date.now()) return { ...fail('Forbereder…', true), code: 'LUP-IMP-007' };
    if (!prepareSender(sender, message.id, job)) return { ...fail('Billedkladden er udløbet. Højreklik på billedet igen.'), code: 'LUP-IMP-001' };
    if (message.type === 'prepare:source' || message.type === 'prepare:capture') {
      if (message.type === 'prepare:capture') {
        if (!job.contextSrcUrl || !Number.isInteger(job.sourceTabId) || job.sourceTabId <= 0) return importFailure('LUP-IMP-006');
        const result = await captureInPage(job.sourceTabId, job.sourceFrameId ?? 0, job.contextSrcUrl, message.capturePixels === true);
        job.candidates = capturedCandidates([...(job.candidates ?? []), ...(result.candidates ?? [])], job.srcUrl);
        job.canvasAvailable = result.canvasAvailable === true;
        job.captureIssue = result.issue;
        if (result.capture) {
          try {
            await putCapture(job.id, result.capture.blob, result.capture, job.expiresAt);
            job.captureAvailable = true; job.captureMethod = result.capture.method;
          } catch { job.captureIssue = { code: 'LUP-IMP-004' }; }
        }
        await chrome.storage.session.set({ [key]: job });
        if (!result.capture || job.captureIssue?.code === 'LUP-IMP-004') return { ...importFailure(job.captureIssue?.code ?? 'LUP-IMP-002'), ...(job.captureIssue?.context ? { context: job.captureIssue.context } : {}) };
      }
      if (!job.captureAvailable) return importFailure('LUP-IMP-003');
      try {
        const capture = await getCapture(job.id);
        if (!capture) return importFailure('LUP-IMP-003');
        const bytes = new Uint8Array(await capture.blob.arrayBuffer());
        let binary = ''; for (let offset = 0; offset < bytes.length; offset += 32768)
          binary += String.fromCharCode(...bytes.subarray(offset, offset + 32768));
        return { ok: true, base64: btoa(binary), contentType: capture.blob.type, method: capture.method, animationLost: capture.animationLost === true };
      } catch { return importFailure('LUP-IMP-004'); }
    }
    if (message.type === 'prepare:get') return { ok: true, srcUrl: job.srcUrl,
      ...(job.sourceTabId !== undefined || job.captureAvailable !== undefined ? { hasCapture: job.captureAvailable === true } : {}),
      ...(Number.isInteger(job.sourceTabId) && job.sourceTabId > 0 ? { sourceTabId: job.sourceTabId, sourceFrameId: job.sourceFrameId ?? 0 } : {}),
      ...(job.sourceFrameOrigin ? { sourceFrameOrigin: job.sourceFrameOrigin } : {}),
      ...(job.candidates ? { candidates: job.candidates } : {}),
      ...(job.captureIssue ? { captureIssue: job.captureIssue } : {}),
      ...(job.captureMethod ? { captureMethod: job.captureMethod } : {}),
      ...(job.canvasAvailable !== undefined ? { canvasAvailable: job.canvasAvailable } : {}),
      ...(job.sourceHost ? { sourceHost: job.sourceHost } : {}),
      ...(job.sourceAltText ? { sourceAltText: job.sourceAltText } : {}),
      ...(job.searchSource ? { searchSource: job.searchSource } : {}),
      ...(job.searchSource && Number.isInteger(job.searchTabId) && job.searchTabId > 0 ? { searchTabId: job.searchTabId } : {}) };
    if (message.type === 'prepare:status') return connectionStatus();
    if (message.type === 'prepare:upload') {
      const candidate = { id: message.id, name: message.name, contentType: message.contentType,
        base64: message.base64, expiresAt: Date.now() + TTL_MS, destinationTabId: 1 };
      if (!validDraft(candidate) || typeof message.altText !== 'string' || message.altText.trim().length < 1
        || message.altText.length > 240) return fail('Kontrollér billedet, det korte navn og beskrivelsen.');
      const keyValue = (await chrome.storage.local.get(ACCESS_KEY))[ACCESS_KEY];
      if (!KEY_PATTERN.test(keyValue ?? '')) return fail('Forbind LUP i udvidelsens indstillinger først.');
      if (activeUploads.has(message.id)) return fail('Billedet er allerede ved at blive gemt.');
      activeUploads.add(message.id);
      try {
        const binary = atob(message.base64), bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
        const extension = { 'image/png': '.png', 'image/jpeg': '.jpg', 'image/gif': '.gif', 'image/webp': '.webp' }[message.contentType];
        const form = new FormData();
        form.append('file', new Blob([bytes], { type: message.contentType }), message.name + extension);
        form.append('name', message.name); form.append('altText', message.altText.trim()); form.append('category', 'custom');
        const created = await apiRequest('emojis', keyValue, { method: 'POST', body: form });
        if (!created?.id || typeof created.name !== 'string') return fail('LUP gav ikke et gyldigt svar. Kontrollér biblioteket før et nyt forsøg.');
        await chrome.storage.session.remove(key);
        await deleteCapture(job.id).catch(() => {});
        return { ok: true, name: created.name, id: created.id };
      } catch (error) { return fail(error.name === 'TimeoutError' ? 'LUP svarede ikke i tide. Kontrollér biblioteket før et nyt forsøg.' : publicError(error)); }
      finally { activeUploads.delete(message.id); }
    }
    const id = crypto.randomUUID();
    const draft = { id, name: message.name, contentType: message.contentType, base64: message.base64,
      expiresAt: Date.now() + TTL_MS, destinationTabId: 1 };
    if (!validDraft(draft)) return fail('Billedet skal være PNG, JPEG, GIF eller WebP på højst 256 KB med et kort navn.');
    await makeRoom('pending:');
    const pendingKey = `pending:${id}`;
    await chrome.storage.session.set({ [pendingKey]: { ...draft, destinationTabId: null } });
    try {
      const tab = await chrome.tabs.create({ url: `https://lup.dk/emojis?lupImport=${id}` });
      await chrome.storage.session.set({ [pendingKey]: { ...draft, destinationTabId: tab.id } });
      await chrome.storage.session.remove(key);
      await deleteCapture(job.id).catch(() => {});
      return { ok: true };
    } catch {
      await chrome.storage.session.remove(pendingKey);
      return fail('LUP.dk kunne ikke åbnes. Prøv igen.');
    }
  }
  if (!['emoji:request', 'emoji:complete'].includes(message.type)) return fail('Ukendt besked.');
  const key = `pending:${message.id}`;
  const draft = (await chrome.storage.session.get(key))[key];
  if (draft?.destinationTabId === null && draft.expiresAt > Date.now()) return fail('Forbereder…', true);
  const allowed = message.type === 'emoji:complete'
    ? validateDraftCompletion(draft, sender, message.id) : validateDraftAccess(draft, sender, message.id);
  if (!allowed) return fail('Billedkladden er udløbet eller tilhører en anden fane. Højreklik på billedet igen.');
  if (message.type === 'emoji:complete') {
    await chrome.storage.session.remove(key);
    return { ok: true };
  }
  return { ok: true, id: draft.id, name: draft.name, contentType: draft.contentType, base64: draft.base64 };
}

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  void handleMessage(message, sender).then(respond).catch(() => respond(fail('Importen kunne ikke læses. Prøv igen.')));
  return true;
});
