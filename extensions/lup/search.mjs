import { normalizeGifSearchResult } from './gif-search.mjs';

const VIEW_KEY = 'lup.imageSearch.view.v2';
const LEGACY_VIEW_KEY = 'lup.imageSearch.view.v1';
const TTL_MS = 30 * 60 * 1000;
const MAX_ITEMS = 200;
const MAX_PAGE = 10;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const form = document.querySelector('#search-form');
const input = document.querySelector('#search-query');
const submit = document.querySelector('#search-submit');
const gallery = document.querySelector('#search-gallery');
const status = document.querySelector('#search-status');
const announcement = document.querySelector('#search-announcement');
const more = document.querySelector('#load-more');
const empty = document.querySelector('#search-empty');
const suggestions = document.querySelector('#search-suggestions');
const modeButtons = [...document.querySelectorAll('.search-mode-switch button')];
const pendingSelections = new Set();
const settings = {
  images: {
    title: 'Find dit næste emoji-billede', subtitle: 'Find et motiv, du kan lide. Vælg det, og gør det til dit eget i billedværkstedet.',
    label: 'Søg efter billeder', placeholder: 'Hvad skal din næste emoji forestille?', noun: 'billeder', singular: 'billede',
    hint: 'Tip: Engelske søgeord giver ofte flere billeder.',
    suggestions: [['cat', 'Katte'], ['dog', 'Hunde'], ['robot', 'Robotter'], ['space', 'Rummet'], ['flowers', 'Blomster']],
  },
  gifs: {
    title: 'Find en GIF med personlighed', subtitle: 'Find en lille animation, og gør den til en levende emoji på LUP.',
    label: 'Søg efter GIF’er', placeholder: 'Søg efter en reaktion eller et motiv…', noun: 'GIF’er', singular: 'GIF',
    hint: 'Søg efter en reaktion, eller vælg et forslag. En tom søgning viser GIF’er at starte med.',
    suggestions: [['dance', 'Dans'], ['smile', 'Smil'], ['happy', 'Glæde'], ['reaction', 'Reaktioner'], ['cat', 'Katte']],
  },
};
function emptyView() {
  return { query: '', draft: '', items: [], nextPage: null, expiresAt: Date.now() + TTL_MS, scrollY: 0,
    openedIds: new Set(), expired: false, hasSearched: false, feedback: null, generation: 0, loading: false, activeRequest: null };
}
const views = { images: emptyView(), gifs: emptyView() };
let mode = 'images', storageTimer = null;
const view = () => views[mode];

function text(value, limit = 180) {
  return typeof value === 'string' ? value.replace(/[\u0000-\u001f\u007f]/g, ' ').replace(/\s+/g, ' ').trim().slice(0, limit) : '';
}
function query(value, requestedMode = mode) {
  if (typeof value !== 'string' || value.length > 200 || /[\u0000-\u001f\u007f]/.test(value)) return null;
  const normalized = value.trim().replace(/\s+/g, ' ');
  return (requestedMode === 'gifs' && normalized === '') || (normalized.length >= 2 && normalized.length <= 100) ? normalized : null;
}
function sourceUrl(value) {
  if (typeof value !== 'string' || value.length > 2048) return null;
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password && !url.port ? url.href : null;
  } catch { return null; }
}
function normalizedItem(value, requestedMode) {
  if (requestedMode === 'gifs') {
    const item = normalizeGifSearchResult(value);
    if (!item || !sourceUrl(item.sourceUrl) || !sourceUrl(item.licenseUrl)) return null;
    return { ...item, title: text(item.title) || 'GIF uden titel', creator: text(item.creator, 160),
      licenseLabel: text(item.licenseLabel, 80) || 'Se kilde', animated: true };
  }
  if (!value || typeof value !== 'object' || !UUID.test(value.id) || !['cc0', 'pdm'].includes(value.license)) return null;
  const source = sourceUrl(value.sourceUrl);
  if (!source) return null;
  const thumbnailUrl = `https://api.openverse.org/v1/images/${value.id}/thumb/`;
  if (value.thumbnailUrl !== thumbnailUrl) return null;
  return { id: value.id, provider: 'openverse', title: text(value.title) || 'Billede uden titel', creator: text(value.creator, 160),
    license: value.license, licenseLabel: value.license === 'cc0' ? 'CC0' : 'Public domain',
    licenseUrl: value.license === 'cc0' ? 'https://creativecommons.org/publicdomain/zero/1.0/' : 'https://creativecommons.org/publicdomain/mark/1.0/',
    sourceUrl: source, thumbnailUrl };
}
function displayMessage(value, kind) {
  status.textContent = text(value, 360); status.hidden = false;
  status.classList.toggle('error', kind === 'error'); status.dataset.kind = kind;
  status.setAttribute('role', kind === 'error' ? 'alert' : 'status');
  status.setAttribute('aria-live', kind === 'error' ? 'assertive' : 'polite');
}
function message(value, kind = 'info', requestedMode = mode) {
  views[requestedMode].feedback = { value: text(value, 360), kind };
  if (requestedMode === mode) displayMessage(value, kind);
}
function saveCurrentView() {
  view().draft = input.value.slice(0, 100);
  view().scrollY = window.scrollY;
}
function persistView(captureCurrent = true) {
  clearTimeout(storageTimer);
  if (captureCurrent) saveCurrentView();
  const modes = {};
  for (const name of ['images', 'gifs']) {
    const state = views[name];
    modes[name] = { query: state.query, draft: state.draft, items: state.items.slice(0, MAX_ITEMS), nextPage: state.nextPage,
      expiresAt: state.expiresAt, scrollY: state.scrollY, openedIds: [...state.openedIds].slice(0, MAX_ITEMS), hasSearched: state.hasSearched };
  }
  try { sessionStorage.setItem(VIEW_KEY, JSON.stringify({ mode, modes })); } catch { /* View persistence is optional. */ }
}
function schedulePersistence() { clearTimeout(storageTimer); storageTimer = setTimeout(persistView, 120); }
function updateUrl() {
  const url = new URL(location.href); url.searchParams.set('mode', mode);
  if (view().query) url.searchParams.set('q', view().query); else url.searchParams.delete('q');
  history.replaceState(null, '', url);
}
function updateButtons() {
  const current = view(), copy = settings[mode];
  submit.querySelector('span').textContent = current.loading ? 'Søger…' : 'Søg';
  submit.setAttribute('aria-busy', String(current.loading));
  gallery.setAttribute('aria-busy', String(current.loading));
  more.disabled = current.loading;
  more.querySelector('span').textContent = current.loading ? `Henter ${copy.noun}…` : `Vis flere ${copy.noun}`;
  document.querySelector('#more-wrap').hidden = !current.items.length || !current.nextPage || current.items.length >= MAX_ITEMS || current.expired;
  document.body.dataset.expired = String(current.expired);
  for (const button of gallery.querySelectorAll('[data-select-id]')) button.disabled = current.expired || pendingSelections.has(`${mode}:${button.dataset.selectId}`);
  for (const suggestion of suggestions.querySelectorAll('[data-query]')) suggestion.dataset.active = String(suggestion.dataset.query === current.query.toLowerCase());
}
function element(tag, className, content) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (content !== undefined) node.textContent = content;
  return node;
}
function icon(name) {
  const image = element('img'); image.src = `assets/icons/${name}.svg`; image.alt = ''; return image;
}
function externalLink(label, href, className) {
  const link = element('a', className, label); link.href = href; link.target = '_blank'; link.rel = 'noopener noreferrer'; return link;
}
function createCard(item, itemMode = mode) {
  const state = views[itemMode], gif = itemMode === 'gifs';
  const card = element('article', 'search-result'); card.dataset.resultId = item.id; card.dataset.mediaType = gif ? 'gif' : 'image';
  const picture = element('div', 'result-picture');
  const image = element('img', 'result-thumb'); image.alt = `Forhåndsvisning: ${item.title}`;
  image.loading = 'lazy'; image.decoding = 'async'; image.referrerPolicy = 'no-referrer';
  image.crossOrigin = 'anonymous'; image.src = item.thumbnailUrl;
  const fallback = element('div', 'result-image-fallback'); fallback.hidden = true;
  fallback.append(icon('image'), element('span', null, 'Billedet kan ikke vises her. Du kan stadig prøve at åbne det.'));
  image.addEventListener('error', () => { image.hidden = true; fallback.hidden = false; });
  const opened = element('span', 'result-opened'); opened.hidden = !state.openedIds.has(item.id);
  opened.append(icon('check'), document.createTextNode('Åbnet'));
  picture.append(image, fallback, opened);
  if (gif) picture.append(element('span', 'result-gif-badge', 'GIF'));
  const body = element('div', 'result-body'), title = element('h3', 'result-title', item.title);
  title.title = item.title;
  const creator = element('p', 'result-creator', item.creator ? `Af ${item.creator}` : 'Ophav ikke oplyst');
  creator.title = creator.textContent;
  const links = element('div', 'result-links');
  const source = externalLink('Kildeside', item.sourceUrl, 'result-source'); source.append(icon('arrow-up-right'));
  source.setAttribute('aria-label', `Åbn kildesiden til ${item.title}`);
  const license = externalLink(item.licenseLabel, item.licenseUrl, 'result-license');
  license.title = `Licensoplysninger: ${item.licenseLabel}`;
  links.append(source, license);
  const choose = element('button', 'result-select'); choose.type = 'button'; choose.dataset.selectId = item.id;
  const buttonText = gif ? 'Vælg GIF' : 'Vælg billede';
  choose.setAttribute('aria-label', `${buttonText}: ${item.title}`);
  const label = element('span', null, buttonText); choose.append(label, icon('arrow-right'));
  choose.addEventListener('click', () => void selectItem(item, itemMode, card, choose, label, opened));
  body.append(title, creator, links, choose); card.append(picture, body);
  card.dataset.opened = String(state.openedIds.has(item.id));
  return card;
}
function renderResults(appendFrom = null) {
  const current = view(), copy = settings[mode], gif = mode === 'gifs';
  if (Number.isInteger(appendFrom)) gallery.append(...current.items.slice(appendFrom).map(item => createCard(item)));
  else gallery.replaceChildren(...current.items.map(item => createCard(item)));
  document.querySelector('#results-section').hidden = current.items.length === 0;
  document.querySelector('#results-heading').textContent = current.query ? `${gif ? 'GIF’er' : 'Billeder'} for “${current.query}”` : gif ? 'GIF’er at starte med' : 'Dine søgeresultater';
  document.querySelector('#results-count').textContent = `${current.items.length} ${copy.noun}`;
  empty.hidden = current.items.length > 0;
  document.querySelector('#empty-heading').textContent = current.hasSearched ? 'Prøv et andet søgeord' : gif ? 'En lille animation siger det hele' : 'Et godt billede er en god start';
  document.querySelector('#empty-description').textContent = current.hasSearched
    ? gif ? 'Ingen GIF’er fundet. Prøv et kortere eller engelsk søgeord, eller vælg et af forslagene ovenfor.'
      : `Vi fandt ikke billeder markeret CC0 eller Public domain for “${current.query}”. Prøv et kortere eller engelsk søgeord, fx cat, robot eller flowers.`
    : gif ? 'Find en reaktion, der passer til dig. Når du gemmer en GIF, skal animationen følge med til LUP.'
      : 'Søg efter dyr, ting eller noget helt tredje. Når du vælger et billede, kan du beskære det og fjerne baggrunden før gem.';
  updateButtons();
}
function renderMode() {
  const copy = settings[mode], gif = mode === 'gifs';
  document.body.dataset.searchMode = mode;
  for (const button of modeButtons) button.setAttribute('aria-pressed', String(button.dataset.searchMode === mode));
  document.querySelector('#search-title').replaceChildren(document.createTextNode(copy.title), element('span', 'heading-dot', '.'));
  document.querySelector('#search-subtitle').textContent = copy.subtitle;
  document.querySelector('#search-query-label').textContent = copy.label;
  form.setAttribute('aria-label', gif ? 'GIF-søgning' : 'Billedsøgning');
  input.placeholder = copy.placeholder; input.required = !gif; input.minLength = 2;
  document.querySelector('#search-hint').textContent = copy.hint;
  suggestions.replaceChildren(element('span', null, 'Prøv fx'), ...copy.suggestions.map(([value, label]) => {
    const button = element('button', null, label); button.type = 'button'; button.dataset.query = value; return button;
  }));
  document.querySelector('#search-source-badge').textContent = gif ? 'GIF’er markeret CC0 / Public domain' : 'Markeret CC0 / Public domain';
  const description = document.querySelector('#search-source-description');
  const credit = document.querySelector('#search-provider-credit');
  if (gif) {
    description.replaceChildren(document.createTextNode('Søger via '), externalLink('Openverse', 'https://openverse.org'), document.createTextNode(' i animerede Wikimedia-billeder. Se kilde og licens ved den enkelte GIF.'));
    credit.replaceChildren(document.createTextNode('GIF-søgning via '), externalLink('Openverse', 'https://openverse.org'), document.createTextNode('. LUP er ikke godkendt eller certificeret af Openverse.'));
  } else {
    description.replaceChildren(document.createTextNode('Søger via '), externalLink('Openverse', 'https://openverse.org'), document.createTextNode('. Se kildesiden for oplysninger om det enkelte billede.'));
    credit.replaceChildren(document.createTextNode('Billedsøgning via '), externalLink('Openverse', 'https://openverse.org'), document.createTextNode('. LUP er ikke godkendt eller certificeret af Openverse.'));
  }
  document.querySelector('#search-footer-caption').textContent = gif ? 'Find en reaktion. Giv den et hjem på LUP.' : 'Vælg et billede. Lav det til dit eget.';
  document.title = gif ? 'Find GIF’er · LUP' : 'Find billeder · LUP';
  if (view().feedback) displayMessage(view().feedback.value, view().feedback.kind); else status.hidden = true;
}
function checkExpiry(requestedMode = mode) {
  const current = views[requestedMode];
  if (!current.items.length || current.expiresAt > Date.now() || current.expired) return;
  current.expired = true;
  if (requestedMode === mode) updateButtons();
  message(`Disse søgeresultater er udløbet. Søg igen for at vælge en ${settings[requestedMode].singular}. Din søgning er bevaret.`, 'info', requestedMode);
}
async function selectItem(item, itemMode, card, button, label, opened) {
  const current = views[itemMode], key = `${itemMode}:${item.id}`;
  checkExpiry(itemMode);
  if (current.expired || pendingSelections.has(key)) return;
  pendingSelections.add(key); button.disabled = true; button.setAttribute('aria-busy', 'true'); label.textContent = 'Åbner…';
  const viewGeneration = current.generation;
  try {
    const result = await chrome.runtime.sendMessage({ type: 'search:select', resultId: item.id });
    if (viewGeneration !== current.generation) return;
    if (!result?.ok) { message(result?.message || 'Billedværkstedet kunne ikke åbnes. Søg igen, og prøv et andet resultat.', 'error', itemMode); return; }
    current.openedIds.add(item.id); card.dataset.opened = 'true'; opened.hidden = false;
    message('Billedværkstedet er åbnet. Dine søgeresultater bliver her, klar til det næste motiv.', 'success', itemMode);
    persistView();
  } catch { if (viewGeneration === current.generation) message('Billedværkstedet kunne ikke åbnes. Prøv igen om et øjeblik.', 'error', itemMode); }
  finally {
    pendingSelections.delete(key); button.disabled = current.expired; button.setAttribute('aria-busy', 'false'); label.textContent = itemMode === 'gifs' ? 'Vælg GIF' : 'Vælg billede';
    updateButtons();
  }
}
async function search(raw, page = 1, append = false) {
  const requestedMode = mode, current = views[requestedMode], copy = settings[requestedMode];
  const requested = query(raw, requestedMode);
  if (requested === null) { message('Skriv et søgeord på 2–100 tegn.', 'error'); input.focus(); return; }
  if (!Number.isInteger(page) || page < 1 || page > MAX_PAGE || (append && current.loading)) return;
  const requestKey = `${requested}\n${page}`;
  if (current.loading && current.activeRequest === requestKey) return;
  const requestGeneration = ++current.generation;
  current.draft = input.value.slice(0, 100); current.activeRequest = requestKey; current.loading = true; updateButtons();
  message(append ? `Henter flere ${copy.noun}…` : requested ? `Finder ${copy.noun} for “${requested}”…` : 'Finder GIF’er at starte med…', 'progress', requestedMode);
  try {
    const response = await chrome.runtime.sendMessage({ type: 'search:query', mode: requestedMode, query: requested, page });
    if (requestGeneration !== current.generation) return;
    if (!response?.ok) { message(response?.message || 'Søgningen svarede ikke. Prøv igen om et øjeblik.', 'error', requestedMode); return; }
    if (!Array.isArray(response.items)) { message('Søgeresultaterne kunne ikke læses. Prøv igen.', 'error', requestedMode); return; }
    const incoming = response.items.slice(0, 48).map(item => normalizedItem(item, requestedMode)).filter(Boolean);
    const appendToView = append && current.query === requested;
    const previousCount = appendToView ? current.items.length : 0;
    const merged = appendToView ? [...current.items] : [];
    const seen = new Set(merged.map(item => item.id));
    for (const item of incoming) if (!seen.has(item.id)) { merged.push(item); seen.add(item.id); }
    current.items = merged.slice(0, MAX_ITEMS); current.query = requested; current.hasSearched = true;
    current.nextPage = Number.isInteger(response.nextPage) && response.nextPage > page && response.nextPage <= MAX_PAGE ? response.nextPage : null;
    if (!append) { current.expiresAt = Date.now() + TTL_MS; current.openedIds = new Set(); }
    current.expired = false;
    if (requestedMode === mode) renderResults(appendToView ? previousCount : null);
    const added = current.items.length - previousCount;
    message(!current.items.length ? `Ingen ${copy.noun} fundet. Prøv et andet søgeord.` : append
      ? `${Math.max(0, added)} nye ${copy.noun} er tilføjet nedenfor.`
      : `${current.items.length} ${copy.noun} klar. Vælg et motiv for at åbne billedværkstedet.`, 'info', requestedMode);
    if (requestedMode === mode) { announcement.textContent = `${current.items.length} ${copy.noun}${requested ? ` for ${requested}` : ''}.`; updateUrl(); }
    persistView();
  } catch {
    if (requestGeneration === current.generation) message('Søgningen kunne ikke gennemføres. Kontrollér forbindelsen, og prøv igen.', 'error', requestedMode);
  } finally { if (requestGeneration === current.generation) { current.loading = false; current.activeRequest = null; updateButtons(); } }
}
function activateMode(next) {
  if (!['images', 'gifs'].includes(next) || next === mode) return;
  saveCurrentView(); mode = next; input.value = view().draft;
  const target = mode, position = view().scrollY;
  renderMode(); renderResults(); checkExpiry(); updateUrl(); persistView(false);
  requestAnimationFrame(() => { if (mode === target) window.scrollTo(0, position); });
  if (mode === 'gifs' && !view().hasSearched && !view().loading) void search('');
}
form.addEventListener('submit', event => { event.preventDefault(); void search(input.value); });
input.addEventListener('input', () => { view().draft = input.value.slice(0, 100); schedulePersistence(); });
more.addEventListener('click', () => { if (view().nextPage) void search(view().query, view().nextPage, true); });
for (const button of modeButtons) button.addEventListener('click', () => activateMode(button.dataset.searchMode));
suggestions.addEventListener('click', event => {
  const button = event.target.closest('button[data-query]');
  if (!button || !suggestions.contains(button)) return;
  input.value = button.dataset.query; void search(input.value);
});
window.addEventListener('scroll', schedulePersistence, { passive: true });
window.addEventListener('pagehide', persistView);
window.addEventListener('focus', () => checkExpiry());
document.addEventListener('visibilitychange', () => { if (!document.hidden) checkExpiry(); });

function restoreView(value, requestedMode) {
  if (!value || !Number.isFinite(value.expiresAt) || value.expiresAt <= Date.now() || value.expiresAt > Date.now() + TTL_MS || !Array.isArray(value.items)) return null;
  const state = emptyView();
  state.query = query(value.query, requestedMode) ?? '';
  state.draft = typeof value.draft === 'string' ? value.draft.slice(0, 100) : state.query;
  state.items = value.items.slice(0, MAX_ITEMS).map(item => normalizedItem(item, requestedMode)).filter(Boolean);
  state.nextPage = Number.isInteger(value.nextPage) && value.nextPage >= 2 && value.nextPage <= MAX_PAGE ? value.nextPage : null;
  state.expiresAt = value.expiresAt;
  state.scrollY = Number.isFinite(value.scrollY) ? Math.max(0, Math.min(value.scrollY, 100_000)) : 0;
  const ids = new Set(state.items.map(item => item.id));
  state.openedIds = new Set(Array.isArray(value.openedIds) ? value.openedIds.filter(id => ids.has(id)) : []);
  state.hasSearched = value.hasSearched === true || state.items.length > 0;
  return state;
}
function start() {
  const url = new URL(location.href);
  mode = url.searchParams.get('mode') === 'gifs' ? 'gifs' : 'images';
  try {
    const raw = sessionStorage.getItem(VIEW_KEY);
    if (raw && raw.length <= 1_000_000) {
      const saved = JSON.parse(raw);
      for (const name of ['images', 'gifs']) views[name] = restoreView(saved?.modes?.[name], name) || emptyView();
    } else {
      const legacy = sessionStorage.getItem(LEGACY_VIEW_KEY);
      if (legacy && legacy.length <= 500_000) views.images = restoreView(JSON.parse(legacy), 'images') || emptyView();
    }
  } catch { /* A bad view cache never prevents a fresh search. */ }
  const requested = url.searchParams.has('q') ? query(url.searchParams.get('q'), mode) : null;
  const useRequested = requested !== null && (!view().hasSearched || requested !== view().query);
  input.value = useRequested ? requested : view().draft;
  renderMode(); renderResults(); checkExpiry();
  const target = mode, position = useRequested ? 0 : view().scrollY;
  requestAnimationFrame(() => { if (mode === target) window.scrollTo(0, position); });
  if (useRequested) void search(requested);
  else if (mode === 'gifs' && !view().hasSearched) void search('');
}
start();
