import test, { beforeEach, after } from 'node:test';
import assert from 'node:assert/strict';
import { TTL_MS, JOB_TTL_MS } from '../core.mjs';

const id = 'a1234567-89ab-4cde-8f01-23456789abcd';
const records = new Map();
const localRecords = new Map();
const accessLevels = [];
const createdTabs = [];
const createdMenus = [];
const listeners = {};
let createHook = null;
let nextTabId = 100;
let removedMenus = 0;
const clone = value => structuredClone(value);
const extensionUrl = path => `chrome-extension://lup-test-id/${path}`;
const originalChrome = globalThis.chrome;
const originalFetch = globalThis.fetch;

globalThis.chrome = {
  runtime: {
    getURL: extensionUrl,
    onInstalled: { addListener: callback => { listeners.installed = callback; } },
    onStartup: { addListener: callback => { listeners.startup = callback; } },
    onMessage: { addListener: callback => { listeners.message = callback; } },
  },
  contextMenus: {
    onClicked: { addListener: callback => { listeners.clicked = callback; } },
    removeAll: async () => { removedMenus++; },
    create: options => { createdMenus.push(clone(options)); },
  },
  storage: {
    local: {
      get: async key => localRecords.has(key) ? { [key]: clone(localRecords.get(key)) } : {},
      set: async entries => { for (const [key, value] of Object.entries(entries)) localRecords.set(key, clone(value)); },
      remove: async key => localRecords.delete(key),
      setAccessLevel: async options => { accessLevels.push(clone(options)); },
    },
    session: {
      get: async keys => {
        const names = keys === null ? [...records.keys()] : typeof keys === 'string' ? [keys] : keys;
        return Object.fromEntries(names.filter(key => records.has(key)).map(key => [key, clone(records.get(key))]));
      },
      set: async entries => { for (const [key, value] of Object.entries(entries)) records.set(key, clone(value)); },
      remove: async keys => { for (const key of Array.isArray(keys) ? keys : [keys]) records.delete(key); },
    },
  },
  tabs: {
    create: async options => {
      const tab = { id: nextTabId++, url: options.url };
      createdTabs.push(clone(tab));
      if (createHook) await createHook(tab);
      return tab;
    },
  },
};
const unexpectedFetch = () => { throw new Error('This worker scenario must not make a network request.'); };
globalThis.fetch = unexpectedFetch;
const { handleMessage } = await import('../worker.mjs');

beforeEach(() => {
  records.clear();
  localRecords.clear();
  accessLevels.length = 0;
  globalThis.fetch = unexpectedFetch;
  createdTabs.length = 0;
  createdMenus.length = 0;
  removedMenus = 0;
  createHook = null;
  nextTabId = 100;
});
after(() => {
  globalThis.chrome = originalChrome;
  globalThis.fetch = originalFetch;
});

function job(changes = {}) {
  return { id, srcUrl: 'https://images.example.com/photo.png', prepareTabId: 7,
    expiresAt: Date.now() + JOB_TTL_MS - 1000, ...changes };
}
function draft(changes = {}) {
  return { id, name: 'min_emoji', contentType: 'image/png', base64: 'YQ==',
    destinationTabId: 42, expiresAt: Date.now() + TTL_MS - 1000, ...changes };
}
const prepareSender = (tabId = 7, url = extensionUrl(`prepare.html?job=${id}`)) => ({ url, tab: { id: tabId } });
const lupSender = (tabId = 42, url = `https://lup.dk/emojis?lupImport=${id}`) => ({ url, tab: { id: tabId } });
const prepareMessage = changes => ({ type: 'prepare:continue', id, name: 'min_emoji', contentType: 'image/png', base64: 'YQ==', ...changes });
const searchId = 'c1234567-89ab-4cde-8f01-23456789abcd';
const searchSender = (tabId = 81, url = extensionUrl('search.html')) => ({ url, tab: { id: tabId } });
const searchItem = changes => ({ id: searchId, title: 'En glad kat', creator: 'Fixturefotograf', license: 'cc0',
  licenseLabel: 'CC0', licenseUrl: 'https://creativecommons.org/publicdomain/zero/1.0/',
  sourceUrl: 'https://commons.wikimedia.org/wiki/File:Cat.jpg',
  thumbnailUrl: `https://api.openverse.org/v1/images/${searchId}/thumb/`,
  imageUrl: `https://api.openverse.org/v1/images/${searchId}/thumb/?full_size=true`, ...changes });
const cachedSearch = changes => ({ ...searchItem(), expiresAt: Date.now() + JOB_TTL_MS - 1000, ...changes });
const rawSearchItem = changes => ({ id: searchId, title: 'En glad kat', creator: 'Fixturefotograf', license: 'cc0',
  license_url: 'https://creativecommons.org/publicdomain/zero/1.0/', foreign_landing_url: 'https://commons.wikimedia.org/wiki/File:Cat.jpg',
  url: 'https://images.example.test/cat.jpg', thumbnail: 'https://images.example.test/cat-thumb.jpg',
  width: 640, height: 480, ...changes });

test('worker installs its entry-point listeners synchronously', () => {
  for (const name of ['installed', 'startup', 'message', 'clicked']) assert.equal(typeof listeners[name], 'function');
});

test('installation creates exactly the Danish image-only context menu', async () => {
  await listeners.installed();
  assert.equal(removedMenus, 1);
  assert.deepEqual(createdMenus, [{ id: 'lup-add-emoji', title: 'Tilføj som emoji på LUP.dk', contexts: ['image'] }]);
  assert.deepEqual(accessLevels, [{ accessLevel: 'TRUSTED_CONTEXTS' }]);
});

const nextTurn = () => new Promise(resolve => setImmediate(resolve));
async function eventually(predicate) {
  for (let attempt = 0; attempt < 25; attempt++) {
    if (predicate()) return;
    await nextTurn();
  }
  assert.fail('The registered event handler did not finish its job/tab binding.');
}

test('the actual context-menu listener ignores unrelated menu events', async () => {
  listeners.clicked({ menuItemId: 'unrelated-menu', srcUrl: 'https://images.example.com/photo.png' });
  await nextTurn();
  assert.equal(createdTabs.length, 0);
  assert.equal(records.size, 0);
});

test('the actual context-menu event stores a bounded source job and binds its prepare tab', async () => {
  const started = Date.now();
  listeners.clicked({ menuItemId: 'lup-add-emoji', srcUrl: 'https://images.example.com/photo.png?size=large' });
  await eventually(() => createdTabs.length === 1 && [...records.values()].some(item => item.prepareTabId === createdTabs[0].id));
  const entries = [...records.entries()];
  assert.equal(entries.length, 1);
  const [key, selected] = entries[0];
  assert.equal(key, `job:${selected.id}`);
  assert.match(selected.id, /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
  assert.equal(selected.srcUrl, 'https://images.example.com/photo.png?size=large');
  assert.equal(selected.prepareTabId, createdTabs[0].id);
  assert.equal(createdTabs[0].url, extensionUrl(`prepare.html?job=${selected.id}`));
  assert.ok(selected.expiresAt >= started + JOB_TTL_MS);
  assert.ok(selected.expiresAt <= Date.now() + JOB_TTL_MS);
  assert.equal(selected.base64, undefined);
  const response = await handleMessage({ type: 'prepare:get', id: selected.id }, { url: createdTabs[0].url, tab: { id: createdTabs[0].id } });
  assert.deepEqual(response, { ok: true, srcUrl: selected.srcUrl });
});

test('a context-menu image with an unsupported source still opens manual file fallback without fetching it', async () => {
  listeners.clicked({ menuItemId: 'lup-add-emoji', srcUrl: 'blob:https://example.com/local-image' });
  await eventually(() => createdTabs.length === 1 && [...records.values()].some(item => item.prepareTabId === createdTabs[0].id));
  assert.equal([...records.values()][0].srcUrl, null);
  assert.equal(createdTabs[0].url.startsWith(extensionUrl('prepare.html?job=')), true);
});

test('the same bound tab can request a draft repeatedly until successful completion', async () => {
  records.set(`pending:${id}`, draft());
  const first = await handleMessage({ type: 'emoji:request', id }, lupSender());
  assert.deepEqual(first, { ok: true, id, name: 'min_emoji', contentType: 'image/png', base64: 'YQ==' });
  assert.equal(records.has(`pending:${id}`), true);
  const reloaded = await handleMessage({ type: 'emoji:request', id }, { ...lupSender(), documentId: 'after-reload' });
  assert.deepEqual(reloaded, first);
  const complete = await handleMessage({ type: 'emoji:complete', id }, lupSender(42, 'https://lup.dk/emojis'));
  assert.deepEqual(complete, { ok: true });
  assert.equal(records.has(`pending:${id}`), false);
  assert.equal((await handleMessage({ type: 'emoji:request', id }, lupSender())).ok, false);
});

test('a different tab or origin cannot read or delete the draft', async () => {
  records.set(`pending:${id}`, draft());
  for (const sender of [lupSender(43), lupSender(42, `https://example.com/emojis?lupImport=${id}`),
    lupSender(42, `https://lup.dk/login?lupImport=${id}`), {},
  ]) {
    for (const type of ['emoji:request', 'emoji:complete']) {
      const result = await handleMessage({ type, id }, sender);
      assert.equal(result.ok, false);
      assert.equal(result.base64, undefined);
      assert.equal(records.has(`pending:${id}`), true);
    }
  }
});

test('expired or malformed stored drafts never release bytes', async () => {
  for (const value of [draft({ expiresAt: Date.now() - 1 }), draft({ base64: 'invalid' }), draft({ name: '<script>' })]) {
    records.set(`pending:${id}`, value);
    const result = await handleMessage({ type: 'emoji:request', id }, lupSender());
    assert.equal(result.ok, false);
    assert.equal(result.base64, undefined);
  }
});

test('unknown messages and invalid IDs cannot mutate stored drafts', async () => {
  records.set(`pending:${id}`, draft());
  for (const message of [null, {}, { type: 'emoji:request', id: 'invalid' }, { type: 'upload', id }]) {
    const result = await handleMessage(message, lupSender());
    assert.equal(result.ok, false);
    assert.equal(records.has(`pending:${id}`), true);
  }
});

test('unbound pending drafts ask for a retry and never release bytes', async () => {
  records.set(`pending:${id}`, draft({ destinationTabId: null }));
  const result = await handleMessage({ type: 'emoji:request', id }, lupSender());
  assert.equal(result.ok, false);
  assert.equal(result.retry, true);
  assert.equal(result.base64, undefined);
});

test('prepare jobs are visible only in their bound extension tab', async () => {
  records.set(`job:${id}`, job());
  assert.deepEqual(await handleMessage({ type: 'prepare:get', id }, prepareSender()), { ok: true, srcUrl: 'https://images.example.com/photo.png' });
  for (const sender of [prepareSender(8), prepareSender(7, `https://lup.dk/prepare.html?job=${id}`),
    prepareSender(7, extensionUrl(`prepare.html?job=${id}&job=${id}`)), {},
  ]) {
    const result = await handleMessage({ type: 'prepare:get', id }, sender);
    assert.equal(result.ok, false);
    assert.equal(result.srcUrl, undefined);
  }
});

test('an expired prepare job is rejected and a still-binding job can retry', async () => {
  records.set(`job:${id}`, job({ expiresAt: Date.now() - 1 }));
  assert.equal((await handleMessage({ type: 'prepare:get', id }, prepareSender())).ok, false);
  records.set(`job:${id}`, job({ prepareTabId: null }));
  const result = await handleMessage({ type: 'prepare:get', id }, prepareSender());
  assert.equal(result.ok, false);
  assert.equal(result.retry, true);
  assert.equal(result.srcUrl, undefined);
});

test('continue binds the newly created LUP tab before allowing access to image bytes', async () => {
  records.set(`job:${id}`, job());
  let earlyResponse;
  createHook = async tab => {
    const newId = new URL(tab.url).searchParams.get('lupImport');
    const pending = records.get(`pending:${newId}`);
    assert.equal(pending.destinationTabId, null);
    earlyResponse = await handleMessage({ type: 'emoji:request', id: newId }, { url: tab.url, tab: { id: tab.id } });
  };
  assert.deepEqual(await handleMessage(prepareMessage(), prepareSender()), { ok: true });
  assert.equal(createdTabs.length, 1);
  const tab = createdTabs[0];
  assert.equal(new URL(tab.url).origin, 'https://lup.dk');
  assert.equal(new URL(tab.url).pathname, '/emojis');
  const newId = new URL(tab.url).searchParams.get('lupImport');
  assert.equal(earlyResponse.ok, false);
  assert.equal(earlyResponse.retry, true);
  assert.equal(earlyResponse.base64, undefined);
  assert.equal(records.has(`job:${id}`), false);
  assert.equal(records.get(`pending:${newId}`).destinationTabId, tab.id);
  const ready = await handleMessage({ type: 'emoji:request', id: newId }, { url: tab.url, tab: { id: tab.id } });
  assert.equal(ready.ok, true);
  assert.equal(ready.base64, 'YQ==');
});

test('failed tab creation removes the undeliverable pending draft and preserves the source job', async () => {
  records.set(`job:${id}`, job());
  createHook = async () => { throw new Error('Could not open tab'); };
  const result = await handleMessage(prepareMessage(), prepareSender());
  assert.equal(result.ok, false);
  assert.equal(records.has(`job:${id}`), true);
  assert.equal([...records.keys()].some(key => key.startsWith('pending:')), false);
});

test('malformed prepared image data cannot create a LUP tab', async () => {
  records.set(`job:${id}`, job());
  for (const changes of [{ contentType: 'image/svg+xml' }, { base64: 'invalid' }, { name: '<script>' }]) {
    const result = await handleMessage(prepareMessage(changes), prepareSender());
    assert.equal(result.ok, false);
  }
  assert.equal(createdTabs.length, 0);
  assert.equal(records.has(`job:${id}`), true);
});

test('creating a pending draft removes expired storage entries and keeps a bounded number of drafts', async () => {
  records.set(`job:${id}`, job());
  records.set('job:expired', job({ expiresAt: Date.now() - 1 }));
  for (let index = 0; index < 8; index++) {
    const oldId = `${index}1234567-89ab-4cde-8f01-23456789abcd`;
    records.set(`pending:${oldId}`, draft({ id: oldId, expiresAt: Date.now() + 1000 * (index + 1) }));
  }
  assert.equal((await handleMessage(prepareMessage(), prepareSender())).ok, true);
  assert.equal(records.has('job:expired'), false);
  assert.equal([...records.keys()].filter(key => key.startsWith('pending:')).length, 8);
  assert.equal(records.has('pending:01234567-89ab-4cde-8f01-23456789abcd'), false);
});

// These are deliberately artificial fixture values, never real upload credentials.
const accessKey = 'lup_' + 'A'.repeat(43);
const replacementKey = 'lup_' + 'B'.repeat(43);
const accessSlot = 'lup.upload.access';
const popupSender = () => ({ url: extensionUrl('popup.html') });
const statusResponse = changes => ({ connected: true, scope: 'emoji:create', email: 'teacher@example.test', ...changes });
const jsonResponse = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { 'content-type': 'application/json' } });
const uploadMessage = changes => ({ type: 'prepare:upload', id, name: 'min_emoji', altText: 'En prøveemoji',
  contentType: 'image/png', base64: 'iVBORw0KGgo=', ...changes });

test('only the extension popup can inspect, replace or clear upload settings', async () => {
  localRecords.set(accessSlot, accessKey);
  for (const sender of [lupSender(), prepareSender(), {}, { url: 'chrome-extension://other-id/popup.html' },
    { url: extensionUrl('prepare.html') }, { url: 'https://lup.dk/popup.html' }]) {
    for (const type of ['settings:status', 'settings:save', 'settings:clear']) {
      const result = await handleMessage({ type, key: replacementKey }, sender);
      assert.equal(result.ok, false);
      assert.equal(JSON.stringify(result).includes(accessKey), false);
      assert.equal(localRecords.get(accessSlot), accessKey);
    }
  }
});

test('upload key saving requires well-formed credentials and a known server scope', async () => {
  let requests = 0;
  globalThis.fetch = async () => { requests++; return jsonResponse(statusResponse()); };
  for (const key of ['', 'not-a-key', 'lup_' + 'A'.repeat(42), 'lup_' + 'A'.repeat(44), null])
    assert.equal((await handleMessage({ type: 'settings:save', key }, popupSender())).ok, false);
  assert.equal(requests, 0);
  for (const state of [statusResponse({ scope: 'admin:*' }), statusResponse({ connected: false }), null]) {
    globalThis.fetch = async () => jsonResponse(state);
    assert.equal((await handleMessage({ type: 'settings:save', key: accessKey }, popupSender())).ok, false);
    assert.equal(localRecords.has(accessSlot), false);
  }
});

test('successful connection and status reveal identity/scope status without returning the credential', async () => {
  const calls = [];
  globalThis.fetch = async (url, options) => { calls.push({ url, options }); return jsonResponse(statusResponse()); };
  const saved = await handleMessage({ type: 'settings:save', key: '  ' + accessKey + '  ' }, popupSender());
  assert.deepEqual(saved, { ok: true, connected: true, email: 'teacher@example.test' });
  assert.equal(localRecords.get(accessSlot), accessKey);
  const status = await handleMessage({ type: 'settings:status' }, popupSender());
  assert.deepEqual(status, saved);
  assert.equal(JSON.stringify([saved, status]).includes(accessKey), false);
  for (const call of calls) {
    assert.equal(call.url, 'https://lup.dk/api/extension/status');
    assert.equal(call.options.headers.Authorization, 'Bearer ' + accessKey);
    assert.equal(call.options.credentials, 'omit');
    assert.equal(call.options.redirect, 'error');
    assert.equal(call.options.referrerPolicy, 'no-referrer');
  }
  assert.deepEqual(await handleMessage({ type: 'settings:clear' }, popupSender()), { ok: true });
  assert.equal(localRecords.has(accessSlot), false);
});

test('failed replacement key validation preserves a previously working local connection', async () => {
  localRecords.set(accessSlot, accessKey);
  globalThis.fetch = async () => jsonResponse({ error: 'extension_access_invalid' }, 401);
  const result = await handleMessage({ type: 'settings:save', key: replacementKey }, popupSender());
  assert.equal(result.ok, false);
  assert.equal(localRecords.get(accessSlot), accessKey);
  assert.equal(JSON.stringify(result).includes(replacementKey), false);
  globalThis.fetch = async () => { throw new TypeError('Fixture connection failed'); };
  assert.equal((await handleMessage({ type: 'settings:save', key: replacementKey }, popupSender())).ok, false);
  assert.equal(localRecords.get(accessSlot), accessKey);
});

test('direct upload requires a bound valid source job and a configured credential', async () => {
  records.set(`job:${id}`, job());
  for (const sender of [prepareSender(8), lupSender(), popupSender(), {}])
    assert.equal((await handleMessage(uploadMessage(), sender)).ok, false);
  assert.equal((await handleMessage(uploadMessage(), prepareSender())).ok, false);
  localRecords.set(accessSlot, 'invalid-local-value');
  assert.equal((await handleMessage(uploadMessage(), prepareSender())).ok, false);
  localRecords.set(accessSlot, accessKey);
  records.set(`job:${id}`, job({ expiresAt: Date.now() - 1 }));
  assert.equal((await handleMessage(uploadMessage(), prepareSender())).ok, false);
  assert.equal(createdTabs.length, 0);
});

test('invalid direct image/metadata input does not send credentials or create a new tab', async () => {
  records.set(`job:${id}`, job()); localRecords.set(accessSlot, accessKey);
  for (const changes of [{ contentType: 'image/svg+xml' }, { base64: 'invalid' }, { name: '<script>' },
    { altText: '' }, { altText: '   ' }, { altText: 'a'.repeat(241) }])
    assert.equal((await handleMessage(uploadMessage(changes), prepareSender())).ok, false);
  assert.equal(records.has(`job:${id}`), true); assert.equal(createdTabs.length, 0);
});

test('direct upload sends only the prepared bytes and metadata to the fixed API and removes the job after success', async () => {
  records.set(`job:${id}`, job()); localRecords.set(accessSlot, accessKey);
  let received;
  globalThis.fetch = async (url, options) => {
    received = { url, options };
    assert.equal(options.method, 'POST');
    assert.equal(options.credentials, 'omit'); assert.equal(options.cache, 'no-store');
    assert.equal(options.redirect, 'error'); assert.equal(options.referrerPolicy, 'no-referrer');
    assert.equal(options.headers.Authorization, 'Bearer ' + accessKey);
    assert.deepEqual([...options.body.keys()].sort(), ['altText', 'category', 'file', 'name']);
    assert.equal(options.body.get('name'), 'min_emoji');
    assert.equal(options.body.get('altText'), 'En prøveemoji');
    assert.equal(options.body.get('category'), 'custom');
    const image = options.body.get('file');
    assert.equal(image.name, 'min_emoji.png'); assert.equal(image.type, 'image/png');
    assert.deepEqual(Buffer.from(await image.arrayBuffer()), Buffer.from(uploadMessage().base64, 'base64'));
    return jsonResponse({ id: 'f34bc0d0-c04b-4f3c-a818-c73a3bff1524', name: 'min_emoji' });
  };
  const result = await handleMessage(uploadMessage(), prepareSender());
  assert.equal(result.ok, true); assert.equal(result.name, 'min_emoji');
  assert.equal(received.url, 'https://lup.dk/api/extension/emojis');
  assert.equal(records.has(`job:${id}`), false); assert.equal(createdTabs.length, 0);
  assert.equal(JSON.stringify(result).includes(accessKey), false);
});

test('server conflict, authorization failure, invalid response and network failure keep the editable job', async () => {
  records.set(`job:${id}`, job()); localRecords.set(accessSlot, accessKey);
  for (const fixture of [() => jsonResponse({ error: 'name_taken' }, 409),
    () => jsonResponse({ error: 'extension_access_invalid' }, 401), () => jsonResponse({}),
    () => { throw new TypeError('Fixture disconnected'); }]) {
    globalThis.fetch = async () => fixture();
    const result = await handleMessage(uploadMessage(), prepareSender());
    assert.equal(result.ok, false); assert.equal(records.has(`job:${id}`), true); assert.equal(createdTabs.length, 0);
    assert.equal(JSON.stringify(result).includes(accessKey), false);
  }
  // A later retry succeeds, proving the active guard is released after every failure.
  globalThis.fetch = async () => jsonResponse({ id: 'success', name: 'min_emoji' });
  assert.equal((await handleMessage(uploadMessage(), prepareSender())).ok, true);
});

test('parallel submit cannot create duplicate direct uploads for one editable job', async () => {
  records.set(`job:${id}`, job()); localRecords.set(accessSlot, accessKey);
  let release, calls = 0;
  globalThis.fetch = () => { calls++; return new Promise(resolve => { release = resolve; }); };
  const first = handleMessage(uploadMessage(), prepareSender());
  await eventually(() => calls === 1);
  assert.equal((await handleMessage(uploadMessage(), prepareSender())).ok, false);
  assert.equal(calls, 1);
  release(jsonResponse({ id: 'success', name: 'min_emoji' }));
  assert.equal((await first).ok, true); assert.equal(records.has(`job:${id}`), false);
});

test('only the exact extension search page can query or select a public image', async () => {
  records.set(`search:${searchId}`, cachedSearch()); localRecords.set(accessSlot, accessKey);
  for (const sender of [lupSender(), prepareSender(), popupSender(), {}, searchSender(0), searchSender(-1),
    searchSender(81, 'chrome-extension://other-id/search.html'), searchSender(81, 'https://lup.dk/search.html'),
    searchSender(81, extensionUrl('search.html/')), { url: extensionUrl('search.html') }]) {
    for (const message of [{ type: 'search:query', query: 'cat', page: 1 }, { type: 'search:select', resultId: searchId }]) {
      const result = await handleMessage(message, sender);
      assert.equal(result.ok, false);
      assert.equal(JSON.stringify(result).includes(accessKey), false);
    }
  }
  assert.equal(createdTabs.length, 0); assert.equal(localRecords.get(accessSlot), accessKey);
});

test('invalid search inputs never request the provider or mutate image jobs', async () => {
  for (const message of [{ type: 'search:query', query: '', page: 1 }, { type: 'search:query', query: '   ', page: 1 },
    { type: 'search:query', query: null, page: 1 }, { type: 'search:query', query: 'cat', page: 0 },
    { type: 'search:query', query: 'cat', page: 11 }, { type: 'search:query', query: 'cat', page: 1.5 },
    { type: 'search:query', query: 'cat', page: '1' }, { type: 'search:select', resultId: 'not-a-uuid' },
    { type: 'search:unknown' }]) assert.equal((await handleMessage(message, searchSender())).ok, false);
  assert.equal(records.size, 0); assert.equal(createdTabs.length, 0);
});

test('public image search sends no LUP authorization, caches normalized items and returns no credential', async () => {
  localRecords.set(accessSlot, accessKey); let requests = 0;
  globalThis.fetch = async (url, options = {}) => {
    requests++; const actual = new URL(url);
    assert.equal(actual.origin, 'https://api.openverse.org'); assert.equal(actual.pathname, '/v1/images/');
    assert.equal(actual.searchParams.get('q'), 'cat'); assert.equal(actual.searchParams.get('page'), '1');
    assert.equal(new Headers(options.headers).has('authorization'), false);
    assert.equal(options.credentials, 'omit');
    return jsonResponse({ results: [rawSearchItem()], result_count: 1, page_count: 1, page: 1, next: null });
  };
  const response = await handleMessage({ type: 'search:query', query: '  cat  ', page: 1 }, searchSender());
  assert.equal(response.ok, true); assert.equal(response.query, 'cat'); assert.equal(response.page, 1);
  assert.equal(response.items.length, 1); assert.equal(response.items[0].id, searchId);
  assert.equal(response.items[0].imageUrl, searchItem().imageUrl);
  assert.equal(JSON.stringify(response).includes(accessKey), false);
  assert.equal(requests, 1); assert.equal(createdTabs.length, 0);
  const cached = records.get(`search:${searchId}`);
  assert.equal(cached.id, searchId); assert(cached.expiresAt > Date.now());
  assert(cached.expiresAt <= Date.now() + JOB_TTL_MS);
});

test('search selection opens an existing bound crop job from cached provider identity, never page-supplied URLs', async () => {
  records.set(`search:${searchId}`, cachedSearch()); localRecords.set(accessSlot, accessKey);
  const localGet = chrome.storage.local.get;
  chrome.storage.local.get = async () => { throw new Error('Search must not inspect the upload credential.'); };
  let response;
  try {
    response = await handleMessage({ type: 'search:select', resultId: searchId, imageUrl: 'https://attacker.test/private.png' }, searchSender());
  } finally { chrome.storage.local.get = localGet; }
  assert.equal(response.ok, true); assert.equal(createdTabs.length, 1);
  const stored = records.get(`job:${response.id}`);
  assert.equal(stored.srcUrl, searchItem().imageUrl); assert.equal(stored.prepareTabId, createdTabs[0].id);
  assert.equal(createdTabs[0].url, extensionUrl(`prepare.html?job=${response.id}`));
  const { imageUrl, ...metadata } = searchItem();
  assert.deepEqual(stored.searchSource, metadata);
  assert.equal(stored.searchTabId, 81);
  assert.equal(JSON.stringify(response).includes(accessKey), false);
  assert.equal(localRecords.get(accessSlot), accessKey); assert.equal(records.has(`search:${searchId}`), true);
  const sender = { url: createdTabs[0].url, tab: { id: createdTabs[0].id } };
  const prepared = await handleMessage({ type: 'prepare:get', id: response.id }, sender);
  assert.deepEqual(prepared.searchSource, metadata); assert.equal(prepared.srcUrl, imageUrl);
  assert.equal(prepared.searchTabId, 81);
  for (const untrusted of [searchSender(), lupSender(), { ...sender, tab: { id: sender.tab.id + 1 } }]) {
    const denied = await handleMessage({ type: 'prepare:get', id: response.id }, untrusted);
    assert.equal(denied.ok, false); assert.equal(denied.searchSource, undefined);
    assert.equal(denied.searchTabId, undefined);
  }
});

test('expired, unrecorded or tampered search entries cannot open a crop job', async () => {
  for (const changes of [{ expiresAt: Date.now() - 1 }, { expiresAt: Date.now() + JOB_TTL_MS + 10000 },
    { expiresAt: 'tomorrow' }, { license: 'by' }, { imageUrl: 'https://attacker.test/image.png' },
    { imageUrl: 'http://api.openverse.org/v1/images/' + searchId + '/thumb/?full_size=true' },
    { thumbnailUrl: 'https://attacker.test/thumb.jpg' }, { licenseUrl: 'https://attacker.test/license' },
    { sourceUrl: 'javascript:alert(1)' }, { sourceUrl: 'https://user:password@example.test/' }]) {
    records.set(`search:${searchId}`, cachedSearch(changes));
    assert.equal((await handleMessage({ type: 'search:select', resultId: searchId }, searchSender())).ok, false);
  }
  records.delete(`search:${searchId}`);
  assert.equal((await handleMessage({ type: 'search:select', resultId: searchId }, searchSender())).ok, false);
  assert.equal(createdTabs.length, 0); assert.equal([...records.keys()].some(key => key.startsWith('job:')), false);
});

test('a failed image tab creation removes its unbound job and preserves the cached search result for retry', async () => {
  records.set(`search:${searchId}`, cachedSearch());
  createHook = async () => { throw new Error('Fixture tab unavailable'); };
  const result = await handleMessage({ type: 'search:select', resultId: searchId }, searchSender());
  assert.equal(result.ok, false); assert.equal([...records.keys()].some(key => key.startsWith('job:')), false);
  assert.equal(records.has(`search:${searchId}`), true);
  createHook = null;
  assert.equal((await handleMessage({ type: 'search:select', resultId: searchId }, searchSender())).ok, true);
});

test('search result storage is bounded to 200 and prunes expired entries without evicting the latest response', async () => {
  for (let i = 0; i < 201; i++) records.set(`search:old-${i}`, { expiresAt: Date.now() + 1000 + i });
  records.set('search:expired', { expiresAt: Date.now() - 1 });
  globalThis.fetch = async () => jsonResponse({ results: [rawSearchItem()], result_count: 1, page_count: 1, page: 1, next: null });
  assert.equal((await handleMessage({ type: 'search:query', query: 'cat', page: 1 }, searchSender())).ok, true);
  assert.equal([...records.keys()].filter(key => key.startsWith('search:')).length, 200);
  assert.equal(records.has('search:expired'), false); assert.equal(records.has('search:old-0'), false);
  assert.equal(records.has(`search:${searchId}`), true);
});

test('repeated search selections retain a bounded eight image jobs while preserving search results', async () => {
  records.set(`search:${searchId}`, cachedSearch());
  for (let i = 0; i < 10; i++) assert.equal((await handleMessage({ type: 'search:select', resultId: searchId }, searchSender())).ok, true);
  assert.equal(createdTabs.length, 10); assert.equal([...records.keys()].filter(key => key.startsWith('job:')).length, 8);
  assert.equal(records.has(`search:${searchId}`), true);
});

test('provider failures preserve previous cached results and never disclose the local upload credential', async () => {
  records.set(`search:${searchId}`, cachedSearch()); localRecords.set(accessSlot, accessKey);
  globalThis.fetch = async () => { throw new TypeError('Fixture disconnected'); };
  const response = await handleMessage({ type: 'search:query', query: 'cat', page: 1 }, searchSender());
  assert.equal(response.ok, false); assert.equal(records.has(`search:${searchId}`), true);
  assert.equal(JSON.stringify(response).includes(accessKey), false); assert.equal(localRecords.get(accessSlot), accessKey);
});

test('concurrent search selections cannot race past the eight editable image job bound', async () => {
  records.set(`search:${searchId}`, cachedSearch());
  const results = await Promise.all(Array.from({ length: 12 }, () => handleMessage({ type: 'search:select', resultId: searchId }, searchSender())));
  assert(results.every(result => result.ok)); assert.equal(createdTabs.length, 12);
  assert.equal([...records.keys()].filter(key => key.startsWith('job:')).length, 8);
});
