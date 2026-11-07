import test from 'node:test';
import assert from 'node:assert/strict';

let number = 0;
async function environment() {
    const previous = { Worker: globalThis.Worker, setTimeout, clearTimeout };
    const workers = [], timers = new Map();
    let timer = 0;
    globalThis.setTimeout = callback => { timers.set(++timer, callback); return timer; };
    globalThis.clearTimeout = id => timers.delete(id);
    globalThis.Worker = class {
        listeners = {}; messages = []; terminated = false;
        constructor(url, options) { this.url = url; this.options = options; workers.push(this); }
        addEventListener(type, listener) { this.listeners[type] = listener; }
        postMessage(message) { this.messages.push(message); }
        terminate() { this.terminated = true; }
        reply(jobId, result = { base64: 'AQID', contentType: 'image/png' }) {
            this.listeners.message({ data: { jobId, result } });
        }
    };
    const module = await import(new URL('../../web/wwwroot/js/emoji-background.js', import.meta.url).href + '?test=' + ++number);
    return { ...module, workers, timers, restore() {
        globalThis.Worker = previous.Worker;
        globalThis.setTimeout = previous.setTimeout;
        globalThis.clearTimeout = previous.clearTimeout;
    } };
}
const source = 'data:image/png;base64,AQID';

test('browser jobs are serialized and reuse one worker after successful inference', async () => {
    const env = await environment();
    try {
        const first = env.removeBackground(source, 'one'), second = env.removeBackground(source, 'two');
        assert.equal(env.workers.length, 1);
        assert.equal(env.workers[0].messages.length, 1);
        assert.equal(env.workers[0].options.type, 'module');
        env.workers[0].reply('one');
        assert.equal((await first).contentType, 'image/png');
        assert.equal(env.workers[0].messages[1].jobId, 'two');
        env.workers[0].reply('two'); await second;
        assert.equal(env.workers.length, 1);
        assert.equal(env.timers.size, 0);
    } finally { env.restore(); }
});

test('cancelling active inference terminates its CPU worker and ignores stale replies', async () => {
    const env = await environment();
    try {
        const first = env.removeBackground(source, 'first');
        const rejected = assert.rejects(first, { name: 'AbortError' });
        const next = env.removeBackground(source, 'next');
        env.cancel('first'); await rejected;
        assert.equal(env.workers[0].terminated, true);
        assert.equal(env.workers.length, 2);
        env.workers[0].reply('next', { base64: 'stale', contentType: 'image/png' });
        env.workers[1].reply('next');
        assert.equal((await next).base64, 'AQID');
    } finally { env.restore(); }
});

test('cancelling a queued job preserves the active image operation', async () => {
    const env = await environment();
    try {
        const active = env.removeBackground(source, 'active');
        const queued = env.removeBackground(source, 'queued');
        const rejected = assert.rejects(queued, { name: 'AbortError' });
        env.cancel('queued'); await rejected;
        assert.equal(env.workers[0].terminated, false);
        env.workers[0].reply('active'); await active;
        assert.equal(env.workers[0].messages.length, 1);
    } finally { env.restore(); }
});

test('the total pipeline deadline aborts the worker and rejects safely', async () => {
    const env = await environment();
    try {
        const operation = env.removeBackground(source, 'timeout');
        const rejected = assert.rejects(operation, /tog for lang tid/);
        [...env.timers.values()][0](); await rejected;
        assert.equal(env.workers[0].terminated, true);
        assert.equal(env.timers.size, 0);
    } finally { env.restore(); }
});

test('bad sources and duplicate IDs never start additional inference', async () => {
    const env = await environment();
    try {
        await assert.rejects(env.removeBackground('https://example.test/x.png', 'url'));
        const operation = env.removeBackground(source, 'duplicate');
        await assert.rejects(env.removeBackground(source, 'duplicate'));
        assert.equal(env.workers.length, 1);
        env.workers[0].reply('duplicate'); await operation;
    } finally { env.restore(); }
});

test('the in-browser queue is bounded and rejects excess work before retaining its image', async () => {
    const env = await environment();
    try {
        const jobs = ['one', 'two', 'three', 'four'].map(id => env.removeBackground(source, id));
        const settled = Promise.allSettled(jobs);
        await assert.rejects(env.removeBackground(source, 'fifth'), /allerede flere billeder/);
        for (const id of ['one', 'two', 'three', 'four']) env.cancel(id);
        const results = await settled;
        assert(results.every(result => result.status === 'rejected'));
        assert.equal(env.timers.size, 0);
    } finally { env.restore(); }
});
