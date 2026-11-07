// Standalone Chromium regression: real service workers, CacheStorage and ESM module identity.
// Run: node editor/tests/qa-editor-upgrade.mjs (PLAYWRIGHT_MODULE may point to an existing install).
// Uses only an isolated browser context and an ephemeral loopback server. No app/API/build is needed.
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const playwrightPath = process.env.PLAYWRIGHT_MODULE || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs');
const workerPath = process.env.EDITOR_UPGRADE_WORKER || resolve(root, 'web/wwwroot/service-worker.published.js');
const { chromium } = await import(pathToFileURL(playwrightPath).href);
const replacementWorker = await readFile(workerPath, 'utf8');
const currentManifestVersion = 'editor-upgrade-browser-fixture-current';
const entry = '/notion-editor/editor.js', stylesheet = '/notion-editor/editor.css';
const lazyAssets = [
  '/notion-editor/excalidraw-sdk-lazy.js',
  '/notion-editor/da-DK-locale-lazy.js',
  '/notion-editor/excalidraw/fonts/fixture-font.woff',
  '/notion-editor/excalidraw/fonts/fixture-font.woff2',
];
const manifestAssets = ['/index.html', '/_framework/fixture-bootstrap.js', entry, stylesheet, ...lazyAssets];
const requests = [], results = [], pageErrors = [];
let workerGeneration = 'legacy', build = 'old-before-portal', denyAssetNetwork = false;
let browser, context;

// A frozen old deployment: cache-first exact URLs, with all manifest assets eagerly cached.
// This intentionally predates the production policy under test below.
const legacyWorker = String.raw`
importScripts('/service-worker-assets.js');
const cacheName = 'offline-cache-' + self.assetsManifest.version;
self.addEventListener('install', event => event.waitUntil((async () => {
  const cache = await caches.open(cacheName);
  await cache.addAll(self.assetsManifest.assets.map(asset => asset.url));
  await self.skipWaiting();
})()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', event => event.respondWith((async () => {
  const cache = await caches.open(cacheName);
  return await cache.match(event.request) || fetch(event.request);
})()));
`;

function resource(pathname) {
  if (pathname === '/' || pathname === '/index.html') return ['text/html', '<!doctype html><html lang="da"><meta charset="utf-8"><title>Editor upgrade fixture</title><body>Isoleret service worker-test</body></html>'];
  if (pathname === entry) return ['application/javascript', `globalThis.__fixtureEditorLoads = (globalThis.__fixtureEditorLoads || []).concat(${JSON.stringify(build)}); export const build = ${JSON.stringify(build)};`];
  if (pathname === stylesheet) return ['text/css', `:root { --fixture-editor-build: ${JSON.stringify(build)}; }`];
  if (pathname === '/_framework/fixture-bootstrap.js') return ['application/javascript', 'export const fixtureBootstrap = true;'];
  if (lazyAssets.includes(pathname)) return /\.woff2?$/.test(pathname) ? ['font/woff2', 'fixture-font-bytes'] : ['application/javascript', 'export const optionalEditorAsset = true;'];
  return null;
}

const server = createServer((request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1');
  requests.push({ path: url.pathname + url.search, generation: workerGeneration, build });
  response.setHeader('Cache-Control', 'no-store');
  if (denyAssetNetwork && url.pathname.startsWith('/notion-editor/')) {
    // Also enforce a real socket failure if browser network emulation misses a worker target.
    request.socket.destroy();
    return;
  }
  if (url.pathname === '/service-worker.js') {
    response.setHeader('Content-Type', 'application/javascript');
    response.setHeader('Service-Worker-Allowed', '/');
    response.end(workerGeneration === 'legacy' ? legacyWorker : replacementWorker);
    return;
  }
  if (url.pathname === '/service-worker-assets.js') {
    response.setHeader('Content-Type', 'application/javascript');
    response.end('self.assetsManifest = ' + JSON.stringify({
      version: workerGeneration === 'legacy' ? 'editor-upgrade-browser-fixture-legacy' : currentManifestVersion,
      assets: manifestAssets.map(url => ({ url })),
    }) + ';');
    return;
  }
  const result = resource(url.pathname);
  if (!result) { response.writeHead(404); response.end(); return; }
  response.setHeader('Content-Type', result[0]); response.end(result[1]);
});

async function step(name, operation) {
  await operation(); results.push({ name, passed: true }); process.stdout.write('PASS ' + name + '\n');
}

try {
  await new Promise((accept, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', accept); });
  const origin = 'http://127.0.0.1:' + server.address().port;
  browser = await chromium.launch({ headless: true });
  context = await browser.newContext({ serviceWorkers: 'allow' });
  const page = await context.newPage();
  page.on('pageerror', error => pageErrors.push(error.message));
  page.setDefaultTimeout(20000);
  await page.goto(origin);
  await page.evaluate(async () => {
    await navigator.serviceWorker.register('/service-worker.js', { updateViaCache: 'none' });
    await navigator.serviceWorker.ready;
    if (!navigator.serviceWorker.controller) await new Promise(accept => navigator.serviceWorker.addEventListener('controllerchange', accept, { once: true }));
  });
  const fetchResource = url => page.evaluate(async path => {
    const response = await fetch(path, { cache: 'no-store' });
    return { status: response.status, body: await response.text() };
  }, url);
  const importBuild = url => page.evaluate(async path => (await import(path)).build, url);
  const cacheInfo = () => page.evaluate(async version => {
    const names = (await caches.keys()).filter(name => name.includes(version));
    if (names.length !== 1) throw new Error('Expected one activated current-version cache, found ' + names.length);
    const cache = await caches.open(names[0]);
    return { name: names[0], urls: (await cache.keys()).map(request => request.url) };
  }, currentManifestVersion);

  await step('Old cache-first worker reproduces stale unversioned editor despite a new network deployment', async () => {
    assert.equal(await importBuild(entry), 'old-before-portal');
    assert.match((await fetchResource(stylesheet)).body, /old-before-portal/);
    build = 'new-with-portal';
    const marker = requests.length;
    assert.match((await fetchResource(entry)).body, /old-before-portal/);
    assert.match((await fetchResource(stylesheet)).body, /old-before-portal/);
    assert.equal(await importBuild(entry), 'old-before-portal');
    assert.equal(requests.slice(marker).filter(item => [entry, stylesheet].includes(item.path)).length, 0, 'old worker must actually serve its cache rather than the HTTP server');
  });

  await step('Versioned ESM and CSS URLs bypass the already controlling old worker before its upgrade', async () => {
    assert.equal(await importBuild(entry + '?v=portal-fix'), 'new-with-portal');
    assert.match((await fetchResource(stylesheet + '?v=portal-fix')).body, /new-with-portal/);
    assert.ok(requests.some(item => item.path === entry + '?v=portal-fix' && item.build === 'new-with-portal'));
    assert.equal(await importBuild(entry), 'old-before-portal', 'an existing ESM instance cannot be replaced by updating a service worker');
    assert.deepEqual(await page.evaluate(() => globalThis.__fixtureEditorLoads), ['old-before-portal', 'new-with-portal']);
  });

  let activeCache;
  await step('Actual published worker upgrades without eagerly fetching optional editor SDK, locale or font assets', async () => {
    workerGeneration = 'current';
    const marker = requests.length;
    await page.evaluate(async () => {
      const registration = await navigator.serviceWorker.getRegistration();
      const changed = new Promise((accept, reject) => {
        const timeout = setTimeout(() => reject(new Error('Timed out waiting for the replacement worker to control the existing page')), 20000);
        navigator.serviceWorker.addEventListener('controllerchange', () => { clearTimeout(timeout); accept(); }, { once: true });
      });
      await registration.update(); await changed;
    });
    activeCache = await cacheInfo();
    const installRequests = requests.slice(marker).map(item => new URL(item.path, origin).pathname);
    assert.ok(installRequests.includes('/_framework/fixture-bootstrap.js'), 'the real worker must finish its ordinary application-shell precache');
    for (const lazy of lazyAssets) {
      assert.ok(!installRequests.includes(lazy), 'optional asset was downloaded during worker installation: ' + lazy);
      assert.ok(!activeCache.urls.some(url => new URL(url).pathname === lazy), 'optional asset was put in the current precache: ' + lazy);
    }
  });

  const currentQuery = '?v=current-editor-build';
  const cachedUrls = [entry, stylesheet, entry + currentQuery, stylesheet + currentQuery];
  const requestedLazyUrls = [lazyAssets[0] + '?v=requested-runtime', lazyAssets[3] + '?v=requested-runtime'];
  const requestedLazyBodies = new Map();
  await step('Current worker fetches fresh editor JS and CSS online even when each exact URL already has stale cached bytes', async () => {
    await page.evaluate(async ({ cacheName, urls }) => {
      const cache = await caches.open(cacheName);
      for (const url of urls) await cache.put(url, new Response('stale-exact-cache-entry', { headers: { 'Content-Type': url.includes('.css') ? 'text/css' : 'application/javascript' } }));
    }, { cacheName: activeCache.name, urls: cachedUrls });
    build = 'online-current-editor';
    const marker = requests.length;
    for (const url of cachedUrls) {
      const response = await fetchResource(url);
      assert.equal(response.status, 200); assert.match(response.body, /online-current-editor/);
      assert.ok(requests.slice(marker).some(item => item.path === url), 'editor URL did not reach the network: ' + url);
    }
    assert.equal(await importBuild(entry + currentQuery), 'online-current-editor');
    const cached = await page.evaluate(async ({ cacheName, urls }) => {
      const cache = await caches.open(cacheName);
      return Promise.all(urls.map(async url => (await cache.match(url)).text()));
    }, { cacheName: activeCache.name, urls: cachedUrls });
    assert.ok(cached.every(body => body.includes('online-current-editor')), 'successful network responses must replace stale exact entries for offline use');
  });

  await step('Optional SDK chunks and WOFF2 fonts enter the offline cache only after their first actual request', async () => {
    const marker = requests.length;
    for (const url of requestedLazyUrls) {
      const response = await fetchResource(url);
      assert.equal(response.status, 200);
      assert.ok(requests.slice(marker).some(item => item.path === url), 'first lazy asset request must reach the network: ' + url);
      requestedLazyBodies.set(url, response.body);
    }
    const cached = await cacheInfo();
    for (const url of requestedLazyUrls) assert.ok(cached.urls.includes(origin + url), 'requested lazy asset was not cached with its complete URL: ' + url);
    assert.ok(!cached.urls.some(url => new URL(url).pathname === lazyAssets[1]), 'an unrequested locale must remain outside the cache');
  });

  await step('Offline editor fallback honors the complete URL and never substitutes another query version', async () => {
    denyAssetNetwork = true;
    await context.setOffline(true);
    for (const url of [entry + currentQuery, stylesheet + currentQuery]) {
      const response = await fetchResource(url);
      assert.equal(response.status, 200); assert.match(response.body, /online-current-editor/);
    }
    for (const url of [entry + '?v=never-cached', stylesheet + '?v=never-cached']) {
      const response = await fetchResource(url);
      assert.equal(response.status, 504, 'offline cache miss must not use a different query or the cached unversioned entry');
      assert.doesNotMatch(response.body, /old-before-portal|new-with-portal|online-current-editor|stale-exact-cache-entry/);
    }
    for (const url of requestedLazyUrls) {
      const response = await fetchResource(url);
      assert.equal(response.status, 200); assert.equal(response.body, requestedLazyBodies.get(url), 'offline SDK/font bytes must match the actually requested version');
      const uncached = await fetchResource(url.replace('?v=requested-runtime', '?v=never-cached'));
      assert.equal(uncached.status, 504, 'offline lazy assets must not ignore query strings');
      assert.notEqual(uncached.body, requestedLazyBodies.get(url));
    }
    await context.setOffline(false); denyAssetNetwork = false;
  });
  assert.deepEqual(pageErrors, [], 'no uncaught page errors');
  process.stdout.write(JSON.stringify({ passed: results.length, tests: results }, null, 2) + '\n');
} finally {
  await context?.close(); await browser?.close();
  server.closeAllConnections();
  await new Promise(accept => server.close(accept));
}
