const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/lup-ui.js'), 'utf8');

function scene(reducedMotion, saved = {}) {
    const storage = new Map(Object.entries(saved));
    const pending = new Map();
    const events = new Map();
    let nextFrame = 0, arcs = [];
    const context = { setTransform() {}, clearRect() { arcs = []; }, beginPath() {}, fill() {}, arc(x, y) { arcs.push([x, y]); } };
    const canvas = { getContext: () => context, isConnected: false };
    const host = { appendChild() { canvas.isConnected = true; } };
    const motion = { matches: reducedMotion, addEventListener() {} };
    const document = {
        hidden: false, documentElement: { dataset: { theme: 'christmas' } },
        getElementById: id => id === 'lup-snow-host' ? host : null,
        createElement: () => canvas, addEventListener: (name, fn) => events.set(name, fn)
    };
    const window = {};
    vm.runInNewContext(source, {
        window, document, localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, val) => storage.set(key, val) },
        sessionStorage: { getItem: () => null }, innerWidth: 1440, innerHeight: 900, devicePixelRatio: 1,
        matchMedia: query => query.includes('reduced-motion') ? motion : { matches: false, addEventListener() {} },
        addEventListener() {}, dispatchEvent() {}, requestAnimationFrame: fn => { pending.set(++nextFrame, fn); return nextFrame; },
        cancelAnimationFrame: id => pending.delete(id)
    });
    window.lupTheme.mount();
    return { theme: window.lupTheme, document, storage, pending,
        frame(time) { const callbacks = [...pending.values()]; pending.clear(); callbacks.forEach(fn => fn(time)); return arcs.map(a => [...a]); },
        visibility(hidden) { document.hidden = hidden; events.get('visibilitychange')(); }
    };
}

test('Start snow overrides reduced motion, moves flakes, and Pause stops the loop', () => {
    const app = scene(true);
    assert.equal(app.theme.snowPaused(), true);
    assert.equal(app.pending.size, 0);
    assert.equal(app.theme.toggleSnow(), false);
    assert.equal(app.storage.get('lup-snow-mode'), 'on');
    assert.equal(app.pending.size, 1);
    const first = app.frame(1000);
    const second = app.frame(1040);
    assert.equal(second.length, 90);
    assert.notEqual(first[0][1], second[0][1]);
    assert.equal(app.theme.toggleSnow(), true);
    assert.equal(app.pending.size, 0);
});

test('Legacy pause can be resumed and the new mode survives remounting', () => {
    const app = scene(true, { 'lup-snow-paused': 'true' });
    assert.equal(app.theme.snowState().mode, 'off');
    app.theme.toggleSnow();
    const reloaded = scene(true, Object.fromEntries(app.storage));
    assert.equal(reloaded.theme.snowPaused(), false);
    assert.equal(reloaded.pending.size, 1);
});

test('System mode respects motion preference and hidden tabs never animate', () => {
    const app = scene(true, { 'lup-snow-mode': 'on' });
    app.theme.setSnowMode('auto');
    assert.equal(app.pending.size, 0);
    app.theme.setSnowMode('on');
    app.visibility(true);
    assert.equal(app.pending.size, 0);
    app.visibility(false);
    assert.equal(app.pending.size, 1);
    app.theme.set('hacker');
    assert.equal(app.pending.size, 0);
    assert.equal(scene(false).pending.size, 1);
});

test('courseCode.copy writes via clipboard API', async () => {
    const storage = new Map();
    const events = new Map();
    const canvas = { getContext: () => ({ setTransform() {}, clearRect() {}, beginPath() {}, fill() {}, arc() {} }), isConnected: false };
    const host = { appendChild() { canvas.isConnected = true; } };
    const document = {
        hidden: false, documentElement: { dataset: { theme: 'light' } },
        getElementById: id => id === 'lup-snow-host' ? host : null,
        createElement: () => canvas, addEventListener: (name, fn) => events.set(name, fn),
        body: { appendChild() {}, }
    };
    let written = null;
    const window = {};
    vm.runInNewContext(source, {
        window, document,
        localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, val) => storage.set(key, val) },
        sessionStorage: { getItem: () => null },
        navigator: { clipboard: { writeText: async (t) => { written = t; } } },
        innerWidth: 800, innerHeight: 600, devicePixelRatio: 1,
        matchMedia: () => ({ matches: false, addEventListener() {} }),
        addEventListener() {}, dispatchEvent() {},
        requestAnimationFrame: () => 1, cancelAnimationFrame() {}
    });
    assert.equal(await window.courseCode.copy('using System;'), true);
    assert.equal(written, 'using System;');
});
