const VIEW_KEY = 'lup.imageSearch.view.v1';
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
const pendingSelections = new Set();
let currentQuery = '', items = [], nextPage = null, expiresAt = Date.now() + TTL_MS;
let loading = false, generation = 0, expired = false, storageTimer = null, activeRequest = null;
let openedIds = new Set();

function text(value, limit = 180) {
  return typeof value === 'string' ? value.replace(/[\u0000-\u001f\u007f]/g, ' ').replace(/\s+/g, ' ').trim().slice(0, limit) : '';
}
function query(value) {
  if (typeof value !== 'string' || value.length > 200 || /[\u0000-\u001f\u007f]/.test(value)) return null;
  const normalized = value.trim().replace(/\s+/g, ' ');
  return normalized.length >= 2 && normalized.length <= 100 ? normalized : null;
}
function sourceUrl(value) {
  if (typeof value !== 'string' || value.length > 2048) return null;
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password && !url.port ? url.href : null;
  } catch { return null; }
}
function normalizedItem(value) {
  if (!value || typeof value !== 'object' || !UUID.test(value.id) || !['cc0', 'pdm'].includes(value.license)) return null;
  const source = sourceUrl(value.sourceUrl);
  if (!source) return null;
  const thumbnailUrl = `https://api.openverse.org/v1/images/${value.id}/thumb/`;
  // Never let cached view data or remote metadata choose a thumbnail host.
  if (value.thumbnailUrl !== thumbnailUrl) return null;
  return { id: value.id, title: text(value.title) || 'Billede uden titel', creator: text(value.creator, 160),
    license: value.license, licenseLabel: value.license === 'cc0' ? 'CC0' : 'Public domain',
    licenseUrl: value.license === 'cc0' ? 'https://creativecommons.org/publicdomain/zero/1.0/' : 'https://creativecommons.org/publicdomain/mark/1.0/',
    sourceUrl: source, thumbnailUrl };
}
function message(value, kind = 'info') {
  status.textContent = text(value, 360); status.hidden = false;
  status.classList.toggle('error', kind === 'error'); status.dataset.kind = kind;
  status.setAttribute('role', kind === 'error' ? 'alert' : 'status');
  status.setAttribute('aria-live', kind === 'error' ? 'assertive' : 'polite');
}
function persistView() {
  clearTimeout(storageTimer);
  try { sessionStorage.setItem(VIEW_KEY, JSON.stringify({ query: currentQuery, draft: input.value.slice(0, 100),
    items: items.slice(0, MAX_ITEMS), nextPage, expiresAt, scrollY: window.scrollY,
    openedIds: [...openedIds].slice(0, MAX_ITEMS) })); } catch { /* View persistence is optional. */ }
}
function schedulePersistence() { clearTimeout(storageTimer); storageTimer = setTimeout(persistView, 120); }
function updateButtons() {
  submit.querySelector('span').textContent = loading ? 'Søger…' : 'Søg';
  submit.setAttribute('aria-busy', String(loading));
  gallery.setAttribute('aria-busy', String(loading));
  more.disabled = loading;
  more.firstChild.textContent = loading ? 'Henter billeder…' : 'Vis flere billeder';
  document.querySelector('#more-wrap').hidden = !items.length || !nextPage || items.length >= MAX_ITEMS || expired;
  document.body.dataset.expired = String(expired);
  for (const button of gallery.querySelectorAll('[data-select-id]')) button.disabled = expired || pendingSelections.has(button.dataset.selectId);
  for (const suggestion of document.querySelectorAll('[data-query]')) suggestion.dataset.active = String(suggestion.dataset.query === currentQuery.toLowerCase());
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
function createCard(item) {
  const card = element('article', 'search-result'); card.dataset.resultId = item.id;
  const picture = element('div', 'result-picture');
  const image = element('img', 'result-thumb'); image.alt = `Forhåndsvisning: ${item.title}`;
  image.loading = 'lazy'; image.decoding = 'async'; image.referrerPolicy = 'no-referrer';
  // The public proxy permits CORS; anonymous images also omit provider cookies.
  image.crossOrigin = 'anonymous'; image.src = item.thumbnailUrl;
  const fallback = element('div', 'result-image-fallback'); fallback.hidden = true;
  fallback.append(icon('image'), element('span', null, 'Billedet kan ikke vises her. Du kan stadig prøve at åbne det.'));
  image.addEventListener('error', () => { image.hidden = true; fallback.hidden = false; });
  const opened = element('span', 'result-opened'); opened.hidden = !openedIds.has(item.id);
  opened.append(icon('check'), document.createTextNode('Åbnet'));
  picture.append(image, fallback, opened);
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
  choose.setAttribute('aria-label', `Vælg billede: ${item.title}`);
  const label = element('span', null, 'Vælg billede'); choose.append(label, icon('arrow-right'));
  choose.addEventListener('click', () => void selectItem(item, card, choose, label, opened));
  body.append(title, creator, links, choose); card.append(picture, body);
  card.dataset.opened = String(openedIds.has(item.id));
  return card;
}
function renderResults(appendFrom = null) {
  if (Number.isInteger(appendFrom)) gallery.append(...items.slice(appendFrom).map(createCard));
  else gallery.replaceChildren(...items.map(createCard));
  document.querySelector('#results-section').hidden = items.length === 0;
  document.querySelector('#results-heading').textContent = currentQuery ? `Billeder for “${currentQuery}”` : 'Dine søgeresultater';
  document.querySelector('#results-count').textContent = `${items.length} billeder`;
  empty.hidden = items.length > 0;
  document.querySelector('#empty-heading').textContent = currentQuery ? 'Prøv et andet søgeord' : 'Et godt billede er en god start';
  document.querySelector('#empty-description').textContent = currentQuery
    ? `Vi fandt ikke billeder markeret CC0 eller Public domain for “${currentQuery}”. Prøv et kortere eller engelsk søgeord, fx cat, robot eller flowers.`
    : 'Søg efter dyr, ting eller noget helt tredje. Når du vælger et billede, kan du beskære det og fjerne baggrunden før gem.';
  updateButtons();
}
function checkExpiry() {
  if (!items.length || expiresAt > Date.now() || expired) return;
  expired = true; updateButtons();
  message('Disse søgeresultater er udløbet. Søg igen for at vælge et billede. Din søgning er bevaret.');
}
async function selectItem(item, card, button, label, opened) {
  checkExpiry();
  if (expired || pendingSelections.has(item.id)) return;
  pendingSelections.add(item.id); button.disabled = true; button.setAttribute('aria-busy', 'true'); label.textContent = 'Åbner…';
  const viewGeneration = generation;
  try {
    const result = await chrome.runtime.sendMessage({ type: 'search:select', resultId: item.id });
    if (viewGeneration !== generation) return;
    if (!result?.ok) { message(result?.message || 'Billedet kunne ikke åbnes. Søg igen, og prøv et andet resultat.', 'error'); return; }
    openedIds.add(item.id); card.dataset.opened = 'true'; opened.hidden = false;
    if (viewGeneration === generation) message('Billedet er åbnet i billedværkstedet. Dine søgeresultater bliver her, klar til det næste billede.', 'success');
    persistView();
  } catch { if (viewGeneration === generation) message('Billedet kunne ikke åbnes. Prøv igen om et øjeblik.', 'error'); }
  finally {
    pendingSelections.delete(item.id); button.disabled = expired; button.setAttribute('aria-busy', 'false'); label.textContent = 'Vælg billede';
    updateButtons();
  }
}
async function search(raw, page = 1, append = false) {
  const requested = query(raw);
  if (!requested) { message('Skriv et søgeord på 2–100 tegn.', 'error'); input.focus(); return; }
  if (!Number.isInteger(page) || page < 1 || page > MAX_PAGE || (append && loading)) return;
  const requestKey = `${requested}\n${page}`;
  if (loading && activeRequest === requestKey) return;
  const requestGeneration = ++generation;
  activeRequest = requestKey;
  loading = true; updateButtons();
  message(append ? 'Henter flere billeder…' : `Finder billeder for “${requested}”…`, 'progress');
  try {
    const response = await chrome.runtime.sendMessage({ type: 'search:query', query: requested, page });
    if (requestGeneration !== generation) return;
    if (!response?.ok) { message(response?.message || 'Billedsøgningen svarede ikke. Prøv igen om et øjeblik.', 'error'); return; }
    if (!Array.isArray(response.items)) { message('Søgeresultaterne kunne ikke læses. Prøv igen.', 'error'); return; }
    const incoming = response.items.slice(0, 48).map(normalizedItem).filter(Boolean);
    const appendToView = append && currentQuery === requested;
    const previousCount = appendToView ? items.length : 0;
    const merged = appendToView ? [...items] : [];
    const seen = new Set(merged.map(item => item.id));
    for (const item of incoming) if (!seen.has(item.id)) { merged.push(item); seen.add(item.id); }
    items = merged.slice(0, MAX_ITEMS); currentQuery = requested;
    nextPage = Number.isInteger(response.nextPage) && response.nextPage > page && response.nextPage <= MAX_PAGE ? response.nextPage : null;
    if (!append) { expiresAt = Date.now() + TTL_MS; openedIds = new Set(); }
    expired = false; renderResults(appendToView ? previousCount : null);
    const added = items.length - previousCount;
    message(!items.length ? 'Ingen billeder fundet. Prøv et andet søgeord.' : append
      ? `${Math.max(0, added)} nye billeder er tilføjet nedenfor.`
      : `${items.length} billeder klar. Vælg et billede for at åbne billedværkstedet.`);
    announcement.textContent = `${items.length} billeder for ${requested}.`;
    const url = new URL(location.href); url.searchParams.set('q', requested); history.replaceState(null, '', url);
    persistView();
  } catch {
    if (requestGeneration === generation) message('Billedsøgningen kunne ikke gennemføres. Kontrollér forbindelsen, og prøv igen.', 'error');
  } finally { if (requestGeneration === generation) { loading = false; activeRequest = null; updateButtons(); } }
}

form.addEventListener('submit', event => { event.preventDefault(); void search(input.value); });
input.addEventListener('input', schedulePersistence);
more.addEventListener('click', () => { if (nextPage) void search(currentQuery, nextPage, true); });
for (const suggestion of document.querySelectorAll('[data-query]')) suggestion.addEventListener('click', () => {
  input.value = suggestion.dataset.query; void search(input.value);
});
window.addEventListener('scroll', schedulePersistence, { passive: true });
window.addEventListener('pagehide', persistView);
window.addEventListener('focus', checkExpiry);
document.addEventListener('visibilitychange', () => { if (!document.hidden) checkExpiry(); });

function start() {
  const requested = query(new URL(location.href).searchParams.get('q'));
  let restored = null;
  try {
    const raw = sessionStorage.getItem(VIEW_KEY);
    if (raw && raw.length <= 500_000) {
      const value = JSON.parse(raw);
      if (value && Number.isFinite(value.expiresAt) && value.expiresAt > Date.now() && value.expiresAt <= Date.now() + TTL_MS
          && Array.isArray(value.items) && (!requested || requested === value.query)) restored = value;
    }
  } catch { /* A bad view cache never prevents a fresh search. */ }
  if (restored) {
    currentQuery = query(restored.query) || '';
    input.value = typeof restored.draft === 'string' ? restored.draft.slice(0, 100) : currentQuery;
    items = restored.items.slice(0, MAX_ITEMS).map(normalizedItem).filter(Boolean);
    nextPage = Number.isInteger(restored.nextPage) && restored.nextPage >= 2 && restored.nextPage <= MAX_PAGE ? restored.nextPage : null;
    expiresAt = restored.expiresAt;
    openedIds = new Set(Array.isArray(restored.openedIds) ? restored.openedIds.filter(value => typeof value === 'string' && UUID.test(value)).slice(0, MAX_ITEMS) : []);
    renderResults();
    const position = Number.isFinite(restored.scrollY) ? Math.max(0, Math.min(restored.scrollY, 100_000)) : 0;
    requestAnimationFrame(() => window.scrollTo(0, position));
  } else {
    renderResults();
    if (requested) { input.value = requested; void search(requested); }
  }
}
start();
