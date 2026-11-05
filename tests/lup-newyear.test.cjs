const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { setMaxListeners } = require('node:events');
class BrowserAbortController extends AbortController {
    constructor() { super(); setMaxListeners(0, this.signal); }
}
const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/lup-newyear.js'), 'utf8');

function scene({ reducedMotion = false, noTargets = false, protectedContent = null, viewportWidth = 800, canvasWidth = viewportWidth, loadedMascot = true } = {}) {
    const pending = new Map(), observers = new Set(), closedEvents = [];
    let nextFrame = 0, clock = 0;
    const document = new EventTarget();
    document.hidden = false;
    document.documentElement = { dataset: { theme: 'newyear' } };
    const window = new EventTarget();
    const context = new Proxy({
        rects: [], images: [],
        clearRect() { this.rects = []; this.images = []; },
        fillRect(...rect) { this.rects.push(rect); },
        drawImage(image, ...args) { this.images.push({ image, args }); }
    }, { get(target, key) { return key in target ? target[key] : () => {}; } });
    function element(rect = {}) {
        const node = new EventTarget(), classes = new Set();
        node.dataset = {}; node.style = {}; node.isConnected = true; node.textContent = ''; node.hidden = false;
        node.classList = { add: value => classes.add(value), remove: value => classes.delete(value), contains: value => classes.has(value) };
        node.getBoundingClientRect = () => ({ left: 390, top: 300, width: 20, height: 20, right: 410, bottom: 320, ...rect });
        node.closest = () => null; node.querySelector = () => null; node.contains = other => other === node;
        node.focus = () => { document.activeElement = node; };
        node.setPointerCapture = () => {}; node.releasePointerCapture = () => {};
        node.getAttribute = name => node[name] ?? null;
        return node;
    }
    const focusBefore = element(); document.activeElement = focusBefore;
    const target = element();
    target.querySelector = selector => protectedContent && selector.split(',').includes(protectedContent) ? element() : null;
    const otherTarget = element({ left: 80, right: 110, top: 260, bottom: 280, width: 30 });
    const canvas = element({ left: 0, top: 0, width: canvasWidth, height: 600, right: canvasWidth, bottom: 600 });
    canvas.getContext = () => context;
    const mascot = element(); mascot.complete = loadedMascot; mascot.naturalWidth = loadedMascot ? 1000 : 0; mascot.naturalHeight = loadedMascot ? 1000 : 0;
    mascot.dataset = { muzzleX: '0.76', muzzleY: '0.18', footY: '0.889' };
    const fields = Object.fromEntries(['score', 'level', 'hits', 'status'].map(name => ['[data-newyear-' + name + ']', element()]));
    fields['[data-newyear-hud]'] = element({ bottom: 94 });
    fields['[data-newyear-controls]'] = element({ top: 520 });
    const dialog = element(); dialog.open = false;
    dialog.showModal = () => { dialog.open = true; };
    dialog.close = () => { dialog.open = false; closedEvents.push(() => dialog.dispatchEvent(new Event('close'))); };
    dialog.querySelector = selector => fields[selector] || null;
    document.getElementById = id => ({ 'lup-newyear-game': dialog, 'lup-newyear-canvas': canvas, 'lup-newyear-player': mascot })[id] || null;
    document.querySelectorAll = () => noTargets ? [] : [target, otherTarget];
    function emit(host, name, properties = {}) {
        const event = new Event(name, { cancelable: true });
        for (const [key, value] of Object.entries(properties)) Object.defineProperty(event, key, { value });
        host.dispatchEvent(event); return event;
    }
    let seed = 12345;
    const deterministicMath = Object.create(Math);
    deterministicMath.random = () => { seed = (seed * 16807) % 2147483647; return (seed - 1) / 2147483646; };
    vm.runInNewContext(source, {
        window, document, Math: deterministicMath, AbortController: BrowserAbortController,
        MutationObserver: class {
            constructor(callback) { this.callback = callback; }
            observe() { observers.add(this.callback); }
            disconnect() { observers.delete(this.callback); }
        },
        matchMedia: () => ({ matches: reducedMotion }),
        getComputedStyle: () => ({ visibility: 'visible', display: 'block', opacity: '1' }),
        innerWidth: viewportWidth, innerHeight: 600, devicePixelRatio: 1,
        requestAnimationFrame: callback => { const id = ++nextFrame; pending.set(id, callback); return id; },
        cancelAnimationFrame: id => pending.delete(id)
    });
    const app = {
        game: window.lupNewYear, target, otherTarget, canvas, mascot, dialog, document, context, pending, observers, focusBefore, fields,
        click(action) {
            const button = element(); button.dataset.newyearAction = action;
            button.closest = selector => selector === '[data-newyear-action]' || selector === 'button' ? button : null;
            return emit(document, 'click', { target: button });
        },
        key(key) { return emit(dialog, 'keydown', { key, target: canvas }); },
        keyUp(key) { return emit(window, 'keyup', { key }); },
        aim(x) {
            emit(canvas, 'pointermove', { clientX: x, clientY: 450, target: canvas });
        },
        pointerDown(action, pointerId) {
            const button = element(); button.dataset.newyearAction = action;
            button.closest = selector => selector === '[data-newyear-action]' ? button : null;
            return emit(dialog, 'pointerdown', { button: 0, pointerId, target: button });
        },
        pointerUp(pointerId) { return emit(window, 'pointerup', { pointerId }); },
        frame(now) { clock = now; const callbacks = [...pending.values()]; pending.clear(); callbacks.forEach(callback => callback(now)); },
        advance(milliseconds, step = 20) {
            if (!clock) this.frame(1000);
            const end = clock + milliseconds;
            while (clock < end) this.frame(Math.min(end, clock + step));
        },
        fireAtTarget(node = target) {
            const rect = node.getBoundingClientRect(); this.aim(rect.left + rect.width / 2);
            this.key(' ');
            let attempts = 0;
            while (!node.classList.contains('newyear-destroyed') && attempts++ < 100) this.advance(20);
            this.keyUp(' ');
        },
        visibility(hidden) { document.hidden = hidden; emit(document, 'visibilitychange'); },
        theme(value) { document.documentElement.dataset.theme = value; for (const callback of [...observers]) callback(); },
        flushClose() { closedEvents.splice(0).forEach(callback => callback()); },
        resize() { emit(window, 'resize'); }
    };
    return app;
}

test('fireworks destroy actual interface content, award points and restore it on exit', () => {
    const app = scene(); assert.equal(app.game.start(), true);
    assert.equal(app.game.state().total, 2);
    app.fireAtTarget();
    assert.equal(app.target.classList.contains('newyear-destroyed'), true);
    assert.equal(app.otherTarget.classList.contains('newyear-destroyed'), false);
    assert.equal(app.game.state().score, 100);
    assert.equal(app.game.state().hits, 1);
    assert.equal(app.game.state().remaining, 1);
    assert.equal(app.fields['[data-newyear-score]'].textContent, '100');
    app.game.stop();
    assert.equal(app.target.classList.contains('newyear-destroyed'), false);
    assert.equal(app.dialog.open, false);
    assert.equal(app.game.state().running, false);
    assert.equal(app.pending.size, 0); assert.equal(app.observers.size, 0);
    assert.equal(app.document.activeElement, app.focusBefore);
});

test('clearing the interface or starting on an empty page never ends the endless game', () => {
    const app = scene(); app.game.start(); app.fireAtTarget(); app.fireAtTarget(app.otherTarget);
    assert.equal(app.game.state().remaining, 0);
    assert.equal(app.game.state().running, true);
    assert.equal(app.pending.size, 1);
    app.advance(3000);
    assert.ok(app.game.state().spawned > 3);
    app.game.stop();
    const empty = scene({ noTargets: true }); empty.game.start(); empty.advance(61000);
    assert.equal(empty.game.state().running, true);
    assert.ok(empty.game.state().fallingCount > 0);
    assert.ok(empty.game.state().spawned > 30);
    assert.equal('seconds' in empty.game.state(), false, 'there is no countdown');
    assert.equal(empty.pending.size, 1); empty.game.stop();
});

test('the endless game adds faster and more frequent targets as play continues', () => {
    const app = scene({ noTargets: true }); app.game.start();
    const initial = app.game.state(); app.advance(48000);
    const later = app.game.state();
    assert.ok(later.level > initial.level);
    assert.ok(later.fallSpeed > initial.fallSpeed);
    assert.ok(later.spawnInterval < initial.spawnInterval);
    assert.equal(app.fields['[data-newyear-level]'].textContent, String(later.level));
    const spawned = later.spawned; app.advance(10000);
    assert.ok(app.game.state().spawned - spawned >= 8);
    app.game.stop();
});

test('six distinct falling target kinds appear, and a durable gift requires two physical hits', () => {
    const app = scene({ noTargets: true }); app.game.start();
    const kinds = new Set(app.game.state().falling.map(target => target.type));
    for (let i = 0; i < 5; i++) { app.advance(1400); app.game.state().falling.forEach(target => kinds.add(target.type)); }
    assert.deepEqual([...kinds].sort(), ['balloon', 'disco', 'gift', 'hat', 'star', 'window']);
    app.game.stop();
    const giftApp = scene({ noTargets: true }); giftApp.game.start();
    const gift = giftApp.game.state().falling.find(target => target.type === 'gift');
    assert.ok(gift); assert.equal(gift.hp, 2);
    giftApp.aim(gift.x + gift.width / 2); giftApp.key(' '); giftApp.keyUp(' ');
    let damaged;
    for (let i = 0; i < 80; i++) {
        giftApp.advance(20);
        damaged = giftApp.game.state().falling.find(target => target.type === 'gift');
        if (damaged && damaged.hp === 1) break;
    }
    assert.ok(damaged && damaged.hp === 1, 'first firework damages the gift');
    assert.equal(giftApp.game.state().score, 0, 'partial damage does not award destruction points');
    giftApp.aim(damaged.x + damaged.width / 2); giftApp.key(' '); giftApp.keyUp(' ');
    for (let i = 0; i < 80 && giftApp.game.state().score === 0; i++) giftApp.advance(20);
    assert.equal(giftApp.game.state().score, 150);
    assert.equal(giftApp.game.state().hits, 1);
    giftApp.game.stop();
});

test('fireworks in another column cannot damage a falling target', () => {
    const app = scene({ noTargets: true }); app.game.start();
    app.aim(45); app.key(' '); app.keyUp(' '); app.advance(1000);
    assert.equal(app.game.state().score, 0);
    assert.equal(app.game.state().hits, 0);
    assert.equal(app.game.state().falling.find(target => target.type === 'gift').hp, 2);
    app.game.stop();
});

test('several minutes of spawning and held fire keep render resources bounded', () => {
    const app = scene({ noTargets: true }); app.game.start(); app.key(' ');
    app.advance(360000, 50);
    const state = app.game.state();
    assert.equal(state.running, true);
    assert.ok(state.spawned > 500); assert.ok(state.escaped > 0);
    assert.ok(state.fallingCount <= 28); assert.ok(state.projectileCount <= 36); assert.ok(state.particleCount <= 360);
    assert.ok(state.spawnInterval >= .19); assert.ok(state.fallSpeed <= 215); assert.ok(state.level <= 20);
    app.game.stop(); assert.equal(app.pending.size, 0);
});

test('hidden tabs freeze falling objects and difficulty and clear held controls', () => {
    const app = scene({ noTargets: true }); app.game.start(); app.advance(1000);
    app.key(' '); app.visibility(true);
    const before = app.game.state();
    assert.equal(app.pending.size, 0); assert.equal(before.paused, true);
    app.advance(120000);
    assert.equal(app.game.state().spawned, before.spawned);
    assert.equal(app.game.state().level, before.level);
    app.visibility(false); app.advance(20);
    assert.equal(app.game.state().level, before.level);
    app.advance(1200);
    assert.equal(app.game.state().projectileCount, 0, 'held fire is cleared when leaving the tab');
    assert.equal(app.game.state().paused, false); app.game.stop();
});

test('Escape and theme changes restore destroyed interface nodes and cancel animation', () => {
    for (const end of ['escape', 'theme']) {
        const app = scene(); app.game.start(); app.fireAtTarget();
        assert.equal(app.target.classList.contains('newyear-destroyed'), true);
        if (end === 'escape') assert.equal(app.key('Escape').defaultPrevented, true); else app.theme('light');
        assert.equal(app.target.classList.contains('newyear-destroyed'), false);
        assert.equal(app.game.state().running, false); assert.equal(app.pending.size, 0); assert.equal(app.observers.size, 0);
    }
});

test('restart restores content, resets difficulty and survives a queued close event', () => {
    const app = scene(); app.click('start'); app.fireAtTarget(); app.advance(17000);
    app.click('restart'); app.flushClose();
    assert.equal(app.game.state().running, true); assert.equal(app.game.state().score, 0); assert.equal(app.game.state().level, 1);
    assert.equal(app.game.state().total, 2); assert.equal(app.target.classList.contains('newyear-destroyed'), false);
    assert.equal(app.dialog.open, true); assert.equal(app.pending.size, 1);
    app.game.start(); assert.equal(app.pending.size, 1); app.game.stop();
});

test('the supplied mascot image is rendered, and an unloaded image still allows play', () => {
    const app = scene(); app.game.start();
    assert.ok(app.context.images.some(draw => draw.image === app.mascot));
    app.game.stop();
    const loading = scene({ loadedMascot: false, noTargets: true }); loading.game.start(); loading.advance(1000);
    assert.equal(loading.game.state().running, true);
    assert.equal(loading.context.images.length, 0); loading.game.stop();
});

test('reduced motion keeps shooting available while removing firework particles', () => {
    const normal = scene(); normal.game.start(); normal.fireAtTarget();
    const reduced = scene({ reducedMotion: true }); reduced.game.start(); reduced.fireAtTarget();
    assert.equal(normal.game.state().score, 100); assert.equal(reduced.game.state().score, 100);
    assert.ok(normal.game.state().particleCount > 0); assert.equal(reduced.game.state().particleCount, 0);
    normal.game.stop(); reduced.game.stop();
});

test('resizing or a disconnected canvas restores the page without leaving a loop', () => {
    for (const end of ['resize', 'disconnect']) {
        const app = scene(); app.game.start(); app.fireAtTarget();
        if (end === 'resize') app.resize(); else { app.canvas.isConnected = false; app.advance(20); }
        assert.equal(app.target.classList.contains('newyear-destroyed'), false);
        assert.equal(app.game.state().running, false); assert.equal(app.pending.size, 0);
    }
});

test('touch steering and held fire survive releasing a different finger', () => {
    const app = scene({ noTargets: true }); app.game.start(); app.advance(20);
    app.pointerDown('left', 1); app.advance(50); app.pointerDown('fire', 2); app.advance(50);
    const beforeFireRelease = app.game.state().playerX;
    app.pointerUp(2); app.advance(50);
    assert.ok(app.game.state().playerX < beforeFireRelease);
    app.pointerUp(1); app.advance(50); const stopped = app.game.state().playerX; app.advance(50);
    assert.equal(app.game.state().playerX, stopped);
    app.pointerDown('fire', 3); app.pointerDown('right', 4); app.advance(50); app.pointerUp(4);
    const afterMoveRelease = app.game.state().playerX; app.advance(1000);
    assert.equal(app.game.state().playerX, afterMoveRelease);
    assert.ok(app.game.state().projectileCount > 1);
    app.pointerUp(3); app.advance(1200); assert.equal(app.game.state().projectileCount, 0); app.game.stop();
});

test('containers with editors and input fields cannot become interface targets', () => {
    for (const protectedContent of ['iframe', 'input', 'textarea', 'select', '[contenteditable]']) {
        const app = scene({ protectedContent }); app.game.start();
        assert.equal(app.game.state().total, 1); app.fireAtTarget();
        assert.equal(app.target.classList.contains('newyear-destroyed'), false);
        app.game.stop();
    }
});

test('CSS canvas dimensions keep pointer aiming and DOM collisions aligned with a scrollbar gutter', () => {
    const app = scene({ viewportWidth: 1440, canvasWidth: 1425 }); app.game.start();
    assert.equal(app.canvas.width, 1425); assert.equal(app.canvas.height, 600);
    assert.equal(app.game.state().playerX, 712.5);
    app.fireAtTarget(); assert.equal(app.target.classList.contains('newyear-destroyed'), true);
    assert.equal(app.game.state().score, 100); app.game.stop();
});


test('pointer aiming follows the firing column and the sprite feet rest on the floor', () => {
    const app = scene({ noTargets: true }); app.game.start();
    app.aim(275); app.advance(20);
    assert.ok(Math.abs(app.game.state().fireOriginX - 275) < .001, 'fireworks launch along the cursor column');
    const draw = app.context.images.find(image => image.image === app.mascot);
    assert.ok(draw, 'the supplied sprite is drawn');
    const [, top, , height] = draw.args;
    assert.ok(Math.abs(top + height * Number(app.mascot.dataset.footY) - 512) < .001, 'visible feet meet the floor rather than the transparent image edge');
    app.game.stop();
});
