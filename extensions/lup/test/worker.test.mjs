import test, { beforeEach, after } from 'node:test';
import assert from 'node:assert/strict';
import { TTL_MS, JOB_TTL_MS } from '../core.mjs';

const id = 'a1234567-89ab-4cde-8f01-23456789abcd';
const records = new Map();
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
globalThis.fetch = () => { throw new Error('The handoff worker must not fetch images, API data, or auth tokens.'); };
const { handleMessage } = await import('../worker.mjs');

beforeEach(() => {
  records.clear();
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

test('worker installs its entry-point listeners synchronously', () => {
  for (const name of ['installed', 'startup', 'message', 'clicked']) assert.equal(typeof listeners[name], 'function');
});

test('installation creates exactly the Danish image-only context menu', async () => {
  await listeners.installed();
  assert.equal(removedMenus, 1);
  assert.deepEqual(createdMenus, [{ id: 'lup-add-emoji', title: 'Tilføj som emoji på LUP.dk', contexts: ['image'] }]);
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
