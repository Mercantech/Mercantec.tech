import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../../web/wwwroot/js/lup-emoji-import.js', import.meta.url), 'utf8');
const id = 'aabbccdd-1122-3344-5566-778899aabbcc';
let sequence = 0;

async function fixture(query = '?lupImport=' + id) {
    const listeners = new Set();
    const timers = new Map();
    const storage = new Map();
    const messages = [], calls = [];
    let timer = 0;
    const page = {
        location: { href: 'https://lup.dk/emojis' + query, origin: 'https://lup.dk' },
        history: { state: { scroll: 27 }, replaceState(state, _, url) { this.state = state; this.url = url; } },
        sessionStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
        addEventListener: (_, listener) => listeners.add(listener),
        removeEventListener: (_, listener) => listeners.delete(listener),
        setTimeout: (fn, delay) => { timers.set(++timer, { fn, delay }); return timer; },
        clearTimeout: timerId => timers.delete(timerId),
        postMessage: (data, origin) => messages.push({ data, origin }),
    };
    globalThis.window = page;
    const bridge = await import('data:text/javascript;base64,' + Buffer.from(source + `\n// ${sequence++}`).toString('base64'));
    const dotnet = { invokeMethodAsync: async (method, data) => { calls.push({ method, data }); return true; } };
    const send = async (data, overrides = {}) => {
        for (const listener of [...listeners]) await listener({ source: page, origin: page.location.origin, data, ...overrides });
    };
    return { bridge, page, dotnet, messages, calls, timers, listeners, storage, send };
}

const draft = () => ({ source: 'lup-emoji-extension', type: 'draft', id, name: 'smile.png', contentType: 'image/png', base64: 'AQID' });

test('only matching page, source, origin and import id can deliver bytes', async () => {
    const f = await fixture();
    const handle = f.bridge.start(f.dotnet, id);
    assert.ok(handle);
    assert.deepEqual(f.messages[0], { data: { source: 'lup-emoji-page', type: 'request', id }, origin: 'https://lup.dk' });
    await f.send(draft(), { source: {} });
    await f.send(draft(), { origin: 'https://evil.example' });
    await f.send({ ...draft(), id: '00000000-1122-3344-5566-778899aabbcc' });
    assert.equal(f.calls.length, 0);
    await f.send(draft());
    assert.deepEqual(f.calls, [{ method: 'ReceiveExtensionDraft', data: { name: 'smile.png', contentType: 'image/png', base64: 'AQID' } }]);
    await f.send(draft());
    assert.equal(f.calls.length, 1);
    assert.equal(f.timers.size, 0);
    assert.equal(f.messages.length, 1, 'delivery never acknowledges upload');
    f.bridge.stop(handle);
    assert.equal(f.listeners.size, 0);
});

test('invalid media and oversized or malformed base64 never reach C#', async () => {
    for (const change of [
        { contentType: 'image/svg+xml' }, { base64: Buffer.alloc(256 * 1024 + 1).toString('base64') },
        { base64: 'AQI' }, { base64: 'AQ=I' }, { name: 'x'.repeat(241) }, { name: '\u0000.png' },
    ]) {
        const f = await fixture();
        const handle = f.bridge.start(f.dotnet, id);
        await f.send({ ...draft(), ...change });
        assert.equal(f.calls.some(call => call.method === 'ReceiveExtensionDraft'), false);
        assert.equal(f.calls[0]?.method, 'ExtensionImportError');
        f.bridge.stop(handle);
    }
});

test('pending retries and timeout are removed on disposal', async () => {
    const f = await fixture();
    const handle = f.bridge.start(f.dotnet, id);
    assert.deepEqual([...f.timers.values()].map(t => t.delay), [1000, 3000, 6000, 12000]);
    [...f.timers.values()].find(t => t.delay === 1000).fn();
    assert.equal(f.messages.length, 2);
    f.bridge.stop(handle);
    assert.equal(f.timers.size, 0);
    assert.equal(f.listeners.size, 0);
});

test('a page with a different query cannot start or acknowledge an import', async () => {
    const f = await fixture('?other=1');
    assert.equal(f.bridge.start(f.dotnet, id), 0);
    f.bridge.complete(id);
    assert.equal(f.messages.length, 0);
});

test('complete preserves other query parameters, hash and history state', async () => {
    const f = await fixture('?other=1&lupImport=' + id + '#library');
    const handle = f.bridge.start(f.dotnet, id);
    f.bridge.complete(id);
    assert.deepEqual(f.messages.at(-1).data, { source: 'lup-emoji-page', type: 'complete', id });
    assert.equal(f.page.history.url, '/emojis?other=1#library');
    assert.deepEqual(f.page.history.state, { scroll: 27 });
    assert.equal(f.timers.size, 0);
    f.bridge.stop(handle);
});

test('login continuation is short lived, local, validated and consumed once', async () => {
    const f = await fixture();
    f.bridge.prepareLogin(id);
    assert.equal(f.bridge.consumeLoginReturnPath(), '/emojis?lupImport=' + id);
    assert.equal(f.bridge.consumeLoginReturnPath(), null);
    assert.throws(() => f.bridge.prepareLogin('//evil.example'));
    for (const value of [
        { id: '//evil.example', expiresAt: Date.now() + 1000 },
        { id, expiresAt: Date.now() - 1 },
        { id, expiresAt: Date.now() + 60 * 60 * 1000 },
        { id, expiresAt: '9999999999999' },
    ]) {
        f.storage.set('lup.emojiImport.login', JSON.stringify(value));
        assert.equal(f.bridge.consumeLoginReturnPath(), null);
        assert.equal(f.storage.size, 0);
    }
    f.storage.set('lup.emojiImport.login', '{');
    assert.equal(f.bridge.consumeLoginReturnPath(), null);
});

test('a rejected user draft is never acknowledged or repeatedly overwritten', async () => {
    const f = await fixture();
    f.dotnet.invokeMethodAsync = async (method, data) => { f.calls.push({ method, data }); return false; };
    const handle = f.bridge.start(f.dotnet, id);
    await f.send(draft());
    await f.send(draft());
    assert.equal(f.calls.length, 1);
    assert.equal(f.messages.some(m => m.data.type === 'complete'), false);
    f.bridge.stop(handle);
});

test('slow optional analysis cannot trigger a second import while delivery is in flight', async () => {
    const f = await fixture();
    let resolve;
    f.dotnet.invokeMethodAsync = (method, data) => {
        f.calls.push({ method, data });
        return new Promise(done => { resolve = done; });
    };
    const handle = f.bridge.start(f.dotnet, id);
    const pending = f.send(draft());
    await f.send(draft());
    assert.equal(f.calls.length, 1);
    assert.equal(f.timers.size, 0);
    resolve(true);
    await pending;
    await f.send(draft());
    assert.equal(f.calls.length, 1);
    assert.equal(f.messages.some(m => m.data.type === 'complete'), false);
    f.bridge.stop(handle);
});
