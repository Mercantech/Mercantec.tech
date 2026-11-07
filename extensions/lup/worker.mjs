import { JOB_TTL_MS, TTL_MS, validId, sourceUrl, validDraft, validateDraftAccess, validateDraftCompletion } from './core.mjs';
import { searchImages, normalizeSearchQuery, MAX_SEARCH_PAGE } from './image-search.mjs';

const MENU_ID = 'lup-add-emoji';
const MAX_PENDING = 8;
const MAX_SEARCH_RESULTS = 200;
const ACCESS_KEY = 'lup.upload.access';
const KEY_PATTERN = /^lup_[A-Za-z0-9_-]{43}$/;
const fail = (message, retry = false) => ({ ok: false, message, retry });
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
    const query = normalizeSearchQuery(message.query);
    if (!query || !Number.isInteger(message.page) || message.page < 1 || message.page > MAX_SEARCH_PAGE)
      return fail('Skriv et søgeord, og vælg en gyldig resultatside.');
    try {
      const response = await searchImages(query, message.page);
      const items = (Array.isArray(response.items) ? response.items : []).map(cachedSearchItem).filter(Boolean).slice(0, MAX_SEARCH_RESULTS);
      await cacheSearchItems(items);
      return { ok: true, query, page: message.page, items, nextPage: response.nextPage };
    } catch (error) {
      return fail(error instanceof TypeError ? 'Billedsøgningen kunne ikke kontaktes. Prøv igen senere.'
        : error?.name === 'TimeoutError' ? 'Billedsøgningen svarede ikke i tide. Prøv igen.'
          : error?.message || 'Billederne kunne ikke hentes. Prøv igen.');
    }
  }
  if (message.type !== 'search:select' || !validId(message.resultId)) return fail('Vælg et billede fra søgeresultaterne.');
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
}

async function makeRoom(prefix) {
  await prune();
  const entries = await chrome.storage.session.get(null);
  const items = Object.entries(entries).filter(([key]) => key.startsWith(prefix)).sort((a, b) => a[1].expiresAt - b[1].expiresAt);
  if (items.length >= MAX_PENDING) await chrome.storage.session.remove(items.slice(0, items.length - MAX_PENDING + 1).map(([key]) => key));
}

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.storage.local.setAccessLevel?.({ accessLevel: 'TRUSTED_CONTEXTS' });
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({ id: MENU_ID, title: 'Tilføj som emoji på LUP.dk', contexts: ['image'] });
  await prune();
});
chrome.runtime.onStartup.addListener(() => { void prune(); });

export function openImageJob(srcUrl, searchSource = null, searchTabId = null) {
  // Multiple search tabs may select images at once. Keep pruning and creation
  // together so concurrent selections cannot exceed the eight-job bound.
  const operation = imageJobQueue.then(() => createImageJob(srcUrl, searchSource, searchTabId));
  imageJobQueue = operation.catch(() => {});
  return operation;
}

async function createImageJob(srcUrl, searchSource, searchTabId) {
  await makeRoom('job:');
  const id = crypto.randomUUID();
  const selected = sourceUrl(srcUrl);
  const job = { id, srcUrl: selected?.href ?? null, expiresAt: Date.now() + JOB_TTL_MS, prepareTabId: null };
  if (searchSource) job.searchSource = searchSource;
  if (searchSource && Number.isInteger(searchTabId) && searchTabId > 0) job.searchTabId = searchTabId;
  await chrome.storage.session.set({ [`job:${id}`]: job });
  try {
    const tab = await chrome.tabs.create({ url: chrome.runtime.getURL(`prepare.html?job=${id}`) });
    await chrome.storage.session.set({ [`job:${id}`]: { ...job, prepareTabId: tab.id } });
    return { id };
  } catch {
    await chrome.storage.session.remove(`job:${id}`);
    throw new Error('Billedvinduet kunne ikke åbnes.');
  }
}

chrome.contextMenus.onClicked.addListener((info) => {
  if (info.menuItemId !== MENU_ID) return;
  void openImageJob(info.srcUrl).catch(error => console.error('LUP kunne ikke åbne billedkladden.', error));
});

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
  if (!message || typeof message !== 'object' || !validId(message.id)) return fail('Importen er ugyldig. Vælg billedet igen.');
  if (['prepare:get', 'prepare:continue', 'prepare:status', 'prepare:upload'].includes(message.type)) {
    const key = `job:${message.id}`;
    const job = (await chrome.storage.session.get(key))[key];
    if (job && job.prepareTabId === null && job.expiresAt > Date.now()) return fail('Forbereder…', true);
    if (!prepareSender(sender, message.id, job)) return fail('Billedkladden er udløbet. Højreklik på billedet igen.');
    if (message.type === 'prepare:get') return { ok: true, srcUrl: job.srcUrl,
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
