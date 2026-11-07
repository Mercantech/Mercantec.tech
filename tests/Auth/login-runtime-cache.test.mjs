import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createContext, runInContext } from 'node:vm';

const source = await readFile(new URL('../../web/wwwroot/service-worker.published.js', import.meta.url), 'utf8');
const origin = 'https://lup.example';
const current = origin + '/js/auth-storage.js?v=login-return-v1';
const previous = origin + '/js/auth-storage.js?v=older-login';
const unversioned = origin + '/js/auth-storage.js';

function fixture({ offline = false, status = 200 } = {}) {
    const entries = new Map();
    const fetched = [];
    const matches = [];
    const absolute = value => new URL(typeof value === 'string' ? value : value.url, origin + '/').href;
    const cache = {
        async match(request, options) {
            const url = absolute(request);
            matches.push({ url, options });
            return entries.get(url)?.clone();
        },
        async put(request, response) { entries.set(absolute(request), response.clone()); },
    };
    const context = createContext({
        URL, Request, Response,
        console: { info() {}, warn() {} },
        self: {
            origin,
            location: new URL('/service-worker.js', origin),
            assetsManifest: { version: 'login-cache-test', assets: [
                { url: 'js/auth-storage.js' },
                { url: 'js/auth-storage.js?v=login-return-v1' },
                { url: 'js/other-feature.js' },
            ] },
            importScripts() {},
            addEventListener() {},
            skipWaiting() {},
            clients: { async claim() {} },
        },
        caches: { async open() { return cache; } },
        async fetch(request, options) {
            fetched.push({ url: absolute(request), cache: options?.cache ?? request.cache });
            if (offline) throw new TypeError('Network unavailable');
            return new Response('current login runtime', { status });
        },
    });
    runInContext(source, context, { filename: 'service-worker.published.js' });
    return {
        entries, fetched, matches,
        fetch: url => context.onFetch({ request: new Request(url) }),
        install: () => context.onInstall({}),
        seed: (url, content) => entries.set(url, new Response(content)),
    };
}

test('current network login runtime wins even over a cached response for the same version', async () => {
    const f = fixture();
    f.seed(current, 'stale cached login runtime');
    f.seed(unversioned, 'legacy unversioned runtime');

    const response = await f.fetch(current);

    assert.equal(await response.text(), 'current login runtime');
    assert.deepEqual(f.fetched, [{ url: current, cache: 'no-cache' }]);
    assert.equal(await f.entries.get(current).text(), 'current login runtime');
    assert.equal(f.matches.length, 0, 'a successful network response never consults an old cache entry');
});

test('network failure can reuse only the exact requested version of the login runtime', async () => {
    const f = fixture({ offline: true });
    f.seed(current, 'matching runtime version');
    f.seed(previous, 'older version');
    f.seed(unversioned, 'unversioned legacy version');

    const response = await f.fetch(current);

    assert.equal(response.status, 200);
    assert.equal(await response.text(), 'matching runtime version');
    assert.deepEqual(f.matches, [{ url: current, options: undefined }]);
    assert.equal(f.fetched.length, 1, 'fallback happens only after trying the deployed runtime');
});

test('network failure never falls back to another version or the legacy unversioned script', async () => {
    const f = fixture({ offline: true });
    f.seed(previous, 'older version');
    f.seed(unversioned, 'unversioned legacy version');

    const response = await f.fetch(current);

    assert.equal(response.status, 504);
    assert.equal(await response.text(), '');
    assert.deepEqual(f.matches, [{ url: current, options: undefined }]);
});

test('offline fallback preserves every query parameter rather than matching just the pathname or version', async () => {
    const f = fixture({ offline: true });
    f.seed(current, 'runtime without the requested build parameter');
    const request = current + '&build=new-deployment';

    const response = await f.fetch(request);

    assert.equal(response.status, 504);
    assert.deepEqual(f.matches, [{ url: request, options: undefined }]);
});

test('HTTP failure is returned instead of silently substituting a cached login runtime', async () => {
    const f = fixture({ status: 503 });
    f.seed(current, 'previously cached runtime');

    const response = await f.fetch(current);

    assert.equal(response.status, 503);
    assert.equal(await f.entries.get(current).text(), 'previously cached runtime');
    assert.equal(f.matches.length, 0);
});

test('worker installation does not prefetch either login-runtime URL but still caches ordinary assets', async () => {
    const f = fixture();

    await f.install();

    assert.deepEqual(f.fetched, [{ url: origin + '/js/other-feature.js', cache: 'no-cache' }]);
    assert.equal(f.entries.has(unversioned), false);
    assert.equal(f.entries.has(current), false);
    assert.equal(f.entries.has(origin + '/js/other-feature.js'), true);
});
