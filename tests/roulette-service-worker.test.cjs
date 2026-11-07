const test = require('node:test');
const assert = require('node:assert/strict');
const { readFile } = require('node:fs/promises');
const { resolve } = require('node:path');
const vm = require('node:vm');

// Execute the published worker itself against a small CacheStorage/network
// adapter, including old unversioned entries left by the preceding deployment.
async function worker() {
  const origin = 'https://platform.test';
  const entries = new Map(), requests = [], puts = [];
  const absolute = request => typeof request === 'string' ? new URL(request, origin).href : request.url;
  const state = {
    failNetwork: false, failCache: false, status: 200, skipped: 0,
    responseText: request => `deployed:${absolute(request)}`
  };
  const cache = {
    async match(request) { return entries.get(absolute(request))?.clone(); },
    async put(request, response) {
      if (state.failCache) throw new Error('cache quota unavailable');
      puts.push(absolute(request)); entries.set(absolute(request), response.clone());
    }
  };
  const assets = ['js/group-roulette-show.js', 'audio/roulette/announcer-v1/manifest.json',
    'audio/roulette/cues-v1/manifest.json',
    '_framework/app.abcd.wasm', 'js/unrelated.js', 'css/app.css'];
  const sandbox = {
    URL, Request, Response, console: { info() {}, warn() {} },
    self: {
      origin, location: new URL(origin + '/service-worker.js'),
      assetsManifest: { version: 'new-deployment', assets: assets.map(url => ({ url })) },
      importScripts() {}, addEventListener() {},
      skipWaiting() { state.skipped++; }, clients: { async claim() {} }
    },
    caches: { async open() { return cache; }, async keys() { return []; }, async delete() {} },
    async fetch(request) {
      requests.push(request);
      if (state.failNetwork) throw new Error('offline');
      return new Response(state.responseText(request), { status: state.status });
    }
  };
  const source = await readFile(resolve(__dirname, '../web/wwwroot/service-worker.published.js'), 'utf8');
  vm.createContext(sandbox); vm.runInContext(source, sandbox, { filename: 'service-worker.published.js' });
  return {
    state, requests, entries, puts,
    seed(path, text) { entries.set(new URL(path, origin).href, new Response(text)); },
    install: () => sandbox.onInstall({}),
    fetch: path => sandbox.onFetch({ request: new Request(new URL(path, origin)) })
  };
}

for (const path of ['/js/group-roulette-show.js', '/audio/roulette/announcer-v1/manifest.json', '/audio/roulette/cues-v1/manifest.json']) {
  test(`${path} uses the deployed network response even with an older cached copy`, async () => {
    const app = await worker();
    app.seed(path, 'old deployment');

    const response = await app.fetch(path);

    assert.equal(await response.text(), `deployed:https://platform.test${path}`);
    assert.equal(app.requests.length, 1);
    assert.equal(app.requests[0].cache, 'no-cache');
    assert.deepEqual(app.puts, [`https://platform.test${path}`]);
    assert.equal(await (await app.fetch(path)).text(), `deployed:https://platform.test${path}`);
    assert.equal(app.requests.length, 2, 'a successful cached request must not restore cache-first behaviour');
  });

  test(`${path} only falls back to the exact requested version when offline`, async () => {
    const app = await worker();
    const current = path + '?v=announcer-v1-2';
    app.seed(path, 'old unversioned deployment');

    assert.equal((await app.fetch(current)).status, 200);
    app.state.failNetwork = true;
    assert.equal(await (await app.fetch(current)).text(), `deployed:https://platform.test${current}`);
    assert.equal((await app.fetch(path + '?v=announcer-v1-3')).status, 504,
      'another runtime version must never receive the old cached voice contract');
  });
}

test('installation does not precache the roulette runtime or voice manifest and keeps other app assets', async () => {
  const app = await worker();

  await app.install();

  assert.equal(app.state.skipped, 1);
  assert.deepEqual(app.requests, ['_framework/app.abcd.wasm', 'js/unrelated.js', 'css/app.css']);
  assert.equal(app.entries.size, 3);
});

test('fingerprinted framework and unrelated JavaScript keep the existing cache-first policy', async () => {
  const app = await worker();
  app.seed('/_framework/app.abcd.wasm', 'cached wasm');
  app.seed('/js/unrelated.js', 'cached unrelated module');

  assert.equal(await (await app.fetch('/_framework/app.abcd.wasm')).text(), 'cached wasm');
  assert.equal(await (await app.fetch('/js/unrelated.js')).text(), 'cached unrelated module');
  assert.equal(app.requests.length, 0);
});

test('an HTTP failure is returned instead of hiding a missing manifest behind an older copy', async () => {
  const app = await worker();
  const path = '/audio/roulette/announcer-v1/manifest.json?v=announcer-v1-2';
  app.seed(path, 'old manifest'); app.state.status = 404;

  const response = await app.fetch(path);

  assert.equal(response.status, 404);
  assert.equal(app.puts.length, 0);
});

test('cache quota errors do not replace a successfully fetched roulette module with stale data', async () => {
  const app = await worker();
  const path = '/js/group-roulette-show.js?v=announcer-v1-2';
  app.seed(path, 'old module'); app.state.failCache = true;

  assert.equal(await (await app.fetch(path)).text(), `deployed:https://platform.test${path}`);
});
