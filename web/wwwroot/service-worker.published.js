// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/, /^appsettings\.Development\.json$/ ];

// Replace with your base path if you are hosting on a subfolder. Ensure there is a trailing '/'.
const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install', cacheName);
    // Activate updated SW immediately so routes like /lectio appear without hard-reset.
    self.skipWaiting();

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate', cacheName);
    await self.clients.claim();

    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    const url = new URL(event.request.url);

    // Never cache or intercept API calls — always hit the network (nginx → api).
    if (url.pathname.startsWith('/api/')) {
        return fetch(event.request);
    }

    // Navigations (HTML): always network-first so new Blazor builds win over stale cache.
    if (event.request.method === 'GET' && event.request.mode === 'navigate') {
        try {
            const network = await fetch(event.request);
            return network;
        } catch {
            const cache = await caches.open(cacheName);
            return (await cache.match('index.html')) || Response.error();
        }
    }

    let cachedResponse = null;
    if (event.request.method === 'GET') {
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(event.request);
    }

    return cachedResponse || fetch(event.request);
}
