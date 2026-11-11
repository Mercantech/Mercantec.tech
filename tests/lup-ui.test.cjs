const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/lup-ui.js'), 'utf8');

function scene(reducedMotion, saved = {}, options = {}) {
    const storage = new Map(Object.entries(saved));
    const pending = new Map();
    const events = new Map();
    const globalEvents = new Map();
    let nextFrame = 0, arcs = [], canvasCount = 0;
    const context = { setTransform() {}, clearRect() { arcs = []; }, beginPath() {}, fill() {}, arc(x, y) { arcs.push([x, y]); } };
    const canvas = { getContext: () => context, isConnected: false };
    const host = { appendChild() { canvas.isConnected = true; canvasCount++; } };
    const motion = { matches: reducedMotion, addEventListener() {} };
    const document = {
        hidden: options.hidden ?? false, documentElement: { dataset: { theme: options.theme ?? 'christmas' } },
        getElementById: id => id === 'lup-snow-host' ? host : null,
        createElement: () => canvas, addEventListener: (name, fn) => events.set(name, fn)
    };
    const window = {};
    const environment = {
        window, document, localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, val) => storage.set(key, val) },
        sessionStorage: { getItem: () => null }, innerWidth: options.width ?? 1440, innerHeight: options.height ?? 900, devicePixelRatio: options.ratio ?? 1,
        matchMedia: query => query.includes('reduced-motion') ? motion : { matches: false, addEventListener() {} },
        addEventListener(name, fn) { if (!globalEvents.has(name)) globalEvents.set(name, []); globalEvents.get(name).push(fn); },
        dispatchEvent() {}, requestAnimationFrame: fn => { pending.set(++nextFrame, fn); return nextFrame; },
        cancelAnimationFrame: id => pending.delete(id)
    };
    vm.runInNewContext(source, environment);
    window.lupTheme.mount();
    return { window, theme: window.lupTheme, document, storage, pending, canvas,
        get canvasCount() { return canvasCount; },
        frame(time) { const callbacks = [...pending.values()]; pending.clear(); callbacks.forEach(fn => fn(time)); return arcs.map(a => [...a]); },
        visibility(hidden) { document.hidden = hidden; events.get('visibilitychange')(); },
        resize(width, height) { environment.innerWidth = width; environment.innerHeight = height; globalEvents.get('resize').forEach(fn => fn()); }
    };
}

test('Christmas snow always moves despite reduced-motion and legacy pause preferences', () => {
    for (const reducedMotion of [false, true]) {
        for (const saved of [{}, { 'lup-snow-paused': 'true' }, { 'lup-snow-mode': 'off' }, { 'lup-snow-mode': 'auto', 'lup-snow-paused': 'true' }]) {
            const app = scene(reducedMotion, saved);
            assert.equal(app.pending.size, 1);
            const first = app.frame(1000);
            const second = app.frame(1040);
            assert.equal(second.length, 90);
            assert.notEqual(first[0][1], second[0][1]);
            assert.equal(app.pending.size, 1);
            const reloaded = scene(reducedMotion, Object.fromEntries(app.storage));
            assert.equal(reloaded.pending.size, 1);
        }
    }
});

test('Hidden tabs stop snow and resume it only when Christmas is visible', () => {
    const app = scene(true, { 'lup-snow-mode': 'off' }, { hidden: true });
    assert.equal(app.pending.size, 0);
    app.visibility(false);
    assert.equal(app.pending.size, 1);
    assert.equal(app.frame(1000).length, 90);
    app.visibility(true);
    assert.equal(app.pending.size, 0);
    assert.deepEqual(app.frame(1040), []);
    app.theme.set('hacker');
    app.visibility(false);
    assert.equal(app.pending.size, 0);
    app.theme.set('christmas');
    assert.equal(app.pending.size, 1);
    assert.equal(app.frame(1080).length, 90);
});

test('Leaving Christmas clears the canvas, including when the viewport changes', () => {
    const app = scene(false);
    assert.equal(app.frame(1000).length, 90);
    for (const theme of ['light', 'dark', 'newyear', 'hacker', 'system']) {
        app.theme.set(theme);
        assert.equal(app.pending.size, 0);
        assert.deepEqual(app.frame(1040), []);
        app.resize(800, 600);
        app.frame(1080); // The seasonal dock may request one geometry update.
        assert.equal(app.pending.size, 0);
        assert.deepEqual(app.frame(1120), []);
    }
    app.theme.set('christmas');
    assert.equal(app.pending.size, 1);
});

test('Remounts and resize retain one canvas and one snow loop with bounded rendering', () => {
    const app = scene(true, {}, { width: 2000, height: 1000, ratio: 4 });
    assert.equal(app.canvas.width, 4000);
    assert.equal(app.canvas.height, 2000);
    for (let i = 0; i < 5; i++) { app.theme.mount(); app.theme.apply('christmas'); }
    assert.equal(app.canvasCount, 1);
    assert.equal(app.pending.size, 1);
    assert.equal(app.frame(1000).length, 90);
    app.resize(260, 740);
    assert.equal(app.frame(1040).length, 20);
    assert.equal(app.canvas.width, 520);
    assert.equal(app.canvas.height, 1480);
    assert.equal(app.canvasCount, 1);
    assert.equal(app.pending.size, 1);
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

test('New Year selection survives reload before login and uses the dark palette', () => {
    const app = scene(false);
    app.theme.set('newyear');
    assert.equal(app.document.documentElement.dataset.theme, 'newyear');
    assert.equal(app.document.documentElement.dataset.bsTheme, 'dark');
    assert.equal(app.pending.size, 0);
    const reloaded = scene(false, Object.fromEntries(app.storage));
    assert.equal(reloaded.theme.get(), 'newyear');
    reloaded.theme.apply(reloaded.theme.get());
    assert.equal(reloaded.document.documentElement.dataset.theme, 'newyear');
    assert.equal(reloaded.document.documentElement.dataset.bsTheme, 'dark');
    assert.equal(reloaded.pending.size, 0);
});

test('Explicit System theme survives a guest reload and resolves the device palette before first paint', () => {
    const app = scene(false);
    app.theme.set('system');
    const reloaded = scene(false, Object.fromEntries(app.storage));
    assert.equal(reloaded.theme.get(), 'system');
    reloaded.theme.apply(reloaded.theme.get());
    assert.equal(reloaded.document.documentElement.dataset.theme, 'system');
    const boot = fs.readFileSync(path.join(__dirname, '../web/wwwroot/index.html'), 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];
    const root = { dataset: {} };
    vm.runInNewContext(boot, { document: { documentElement: root },
        localStorage: { getItem: () => 'system' },
        matchMedia: () => ({ matches: true }) });
    assert.equal(root.dataset.theme, 'system');
    assert.equal(root.dataset.bsTheme, 'dark');
});

test('Character gallery links open the disclosure before scrolling and focus its keyboard toggle', async () => {
    const app = scene(false, {}, { theme: 'light' });
    const id = 'character-reactions-title';
    const expectedLocation = 'https://lup.example/characters/raymond#' + id;
    app.window.location = { href: expectedLocation, hash: '#' + id };
    const gallery = { open: false };
    const steps = [];
    const summary = {
        scrollIntoView() {
            assert.equal(gallery.open, true);
            steps.push('scroll');
        },
        focus(options) {
            assert.equal(options.preventScroll, true);
            steps.push('focus');
        },
        setAttribute() { assert.fail('The native summary must stay in the tab order'); }
    };
    const heading = {
        closest(selector) {
            if (selector === 'details.character-artwork-gallery') return gallery;
            if (selector === 'summary') return summary;
            return null;
        }
    };
    app.document.getElementById = target => target === id ? heading : null;

    const current = app.window.lupCharacterUi.scrollToSection(id, expectedLocation);
    assert.equal(gallery.open, false);
    app.frame(1000);
    await current;
    assert.equal(gallery.open, true);
    assert.deepEqual(steps, ['scroll', 'focus']);

    gallery.open = false;
    const outdated = app.window.lupCharacterUi.scrollToSection(id, expectedLocation);
    app.window.location.href = 'https://lup.example/characters/lupp#' + id;
    app.frame(1020);
    await outdated;
    assert.equal(gallery.open, false);
    assert.deepEqual(steps, ['scroll', 'focus']);
});

test('The shared profile anchor helper still focuses and scrolls an ordinary section', async () => {
    const app = scene(false, {}, { theme: 'light' });
    app.window.location = { hash: '#indstillinger' };
    const steps = [];
    const section = {
        closest() { return null; },
        setAttribute(name, value) { steps.push([name, value]); },
        scrollIntoView() { steps.push('scroll'); },
        focus() { steps.push('focus'); }
    };
    app.document.getElementById = target => target === 'indstillinger' ? section : null;

    const scroll = app.window.lupProfileUi.scrollToSection('indstillinger');
    app.frame(1000);
    await scroll;
    assert.deepEqual(steps, [['tabindex', '-1'], 'scroll', 'focus']);
});
