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

function scene({ reducedMotion = false, noTargets = false, protectedContent = null, viewportWidth = 800, canvasWidth = viewportWidth, loadedMascot = true, targetCount = 2, reservePiercingTargets = false, reserveRadiusTargets = false, reserveCometTargets = false } = {}) {
    const pending = new Map(), observers = new Set(), closedEvents = [];
    let nextFrame = 0, clock = 0;
    const document = new EventTarget();
    document.hidden = false;
    document.documentElement = { dataset: { theme: 'newyear' } };
    const window = new EventTarget();
    const createContext = () => new Proxy({
        rects: [], images: [], texts: [], translations: [],
        clearRect() { this.rects = []; this.images = []; this.texts = []; this.translations = []; },
        fillRect(...rect) { this.rects.push(rect); },
        drawImage(image, ...args) { this.images.push({ image, args }); },
        fillText(text, ...args) { this.texts.push({ text, args }); },
        translate(...coordinates) { this.translations.push(coordinates); }
    }, { get(target, key) { return key in target ? target[key] : () => {}; } });
    const context = createContext(), skyContext = createContext();
    function element(rect = {}) {
        const node = new EventTarget(), classes = new Set();
        node.dataset = {}; node.style = { setProperty(name, value) { this[name] = value; } }; node.isConnected = true; node.textContent = ''; node.hidden = false;
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
    const interfaceTargets = [target, otherTarget];
    for (let i = 2; i < targetCount; i++) interfaceTargets.push(element());
    const piercingTargets = reservePiercingTargets ? [140, 540, 680].flatMap(left => [300, 260, 220].map(top => element({ left, right: left + 20, width: 20, top, bottom: top + 20 }))) : [];
    interfaceTargets.push(...piercingTargets);
    const radiusTargets = reserveRadiusTargets ? Array.from({ length: 11 }, (_, index) => element({ left: 120 + index * 55, right: 140 + index * 55, width: 20, top: 360, bottom: 380 })) : [];
    interfaceTargets.push(...radiusTargets);
    const cometTargets = reserveCometTargets ? Array.from({ length: 10 }, (_, index) => 120 + index * 60).flatMap(left => [360, 340, 320].map(top => element({ left, right: left + 20, width: 20, height: 12, top, bottom: top + 12 }))) : [];
    interfaceTargets.push(...cometTargets);
    const canvas = element({ left: 0, top: 0, width: canvasWidth, height: 600, right: canvasWidth, bottom: 600 });
    canvas.getContext = () => context;
    const sky = element({ left: 0, top: 0, width: canvasWidth, height: 600 });
    sky.hidden = true;
    sky.getContext = () => skyContext;
    const mascot = element(); mascot.complete = loadedMascot; mascot.naturalWidth = loadedMascot ? 1000 : 0; mascot.naturalHeight = loadedMascot ? 1000 : 0;
    mascot.dataset = { muzzleX: '0.76', muzzleY: '0.18', footY: '0.889' };
    const fields = Object.fromEntries(['score', 'level', 'hits', 'status'].map(name => ['[data-newyear-' + name + ']', element()]));
    fields['[data-newyear-hud]'] = element({ bottom: 94 });
    fields['[data-newyear-controls]'] = element({ top: 520 });
    fields['[data-newyear-boost-empty]'] = element();
    for (const kind of ['multishot', 'mega', 'rapid', 'comet', 'speed']) {
        const chip = element(); chip.hidden = true;
        fields['[data-newyear-boost="' + kind + '"]'] = chip;
        fields["[data-newyear-boost='" + kind + "']"] = chip;
    }
    const dialog = element(); dialog.open = false;
    dialog.showModal = () => { dialog.open = true; };
    dialog.close = () => { dialog.open = false; closedEvents.push(() => dialog.dispatchEvent(new Event('close'))); };
    dialog.querySelector = selector => fields[selector] || null;
    document.getElementById = id => ({ 'lup-newyear-game': dialog, 'lup-newyear-canvas': canvas, 'lup-newyear-player': mascot, 'lup-newyear-sky': sky })[id] || null;
    document.querySelector = () => dialog.open || document.menuOpen ? dialog : null;
    document.querySelectorAll = () => noTargets ? [] : interfaceTargets;
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
        game: window.lupNewYear, target, otherTarget, interfaceTargets, piercingTargets, radiusTargets, cometTargets, canvas, mascot, dialog, document, context, sky, skyContext, pending, observers, focusBefore, fields,
        click(action) {
            const button = element(); button.dataset.newyearAction = action;
            button.closest = selector => selector === '[data-newyear-action]' || selector === 'button' ? button : null;
            return emit(document, 'click', { target: button });
        },
        clickPage({ background = true, ...options } = {}) {
            const target = element();
            target.matches = selector => background && selector.includes('.public-home');
            return emit(document, 'click', { target, button: 0, detail: 1, clientX: 340, clientY: 250, ...options });
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


function startChargedScene() {
    const app = scene({ targetCount: 60 }); app.game.start(); app.aim(400); return app;
}
function chargeThrough(app, kind) {
    app.aim(400); app.key(' ');
    let attempts = 0;
    while (!(app.game.state().boosts[kind] > 0) && attempts++ < 800) app.advance(20);
    app.keyUp(' ');
    assert.ok(app.game.state().boosts[kind] > 0, 'the ' + kind + ' boost is collected through ordinary shooting');
    return app.game.state();
}
function clearShootingColumn(app) {
    for (const target of app.interfaceTargets) target.isConnected = false;
    app.aim(50); app.advance(1000);
}
function shootOnce(app, x = 400) {
    app.aim(x); app.key(' '); app.keyUp(' ');
    return app.game.state();
}
function clearTargetX(state, target) {
    const candidates = [target.x + target.width - 6, target.x + 6, target.x + target.width / 2];
    return candidates.find(x => !state.falling.some(other => other.id !== target.id && other.y > target.y && x >= other.x - 5 && x <= other.x + other.width + 5)) ?? candidates[0];
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

test('a blank-page click launches from below and bursts at the clicked point', () => {
    const app = scene();
    const click = app.clickPage();
    assert.equal(click.defaultPrevented, false);
    assert.equal(app.sky.hidden, false);
    app.advance(100);
    const [x, y] = app.skyContext.translations[0];
    assert.equal(x, 340);
    assert.ok(y > 250 && y < 615);
    app.advance(100);
    assert.ok(app.skyContext.translations[0][1] < y);
    let landed = false;
    for (let i = 0; i < 25 && !landed; i++) {
        app.advance(20);
        landed = app.skyContext.rects.some(([left, top, width]) => left === 339 && top === 249 && width === 2.4);
    }
    assert.ok(landed, 'sparks start at the click coordinates');
    assert.equal(app.game.state().running, false);
    assert.equal(app.game.state().score, 0);
    app.advance(2200);
    assert.equal(app.sky.hidden, true);
    assert.equal(app.pending.size, 0);
    assert.equal(app.observers.size, 0);
});

test('page fireworks ignore controls, open menus and other themes', () => {
    const app = scene();
    app.clickPage({ background: false });
    app.clickPage({ detail: 0 });
    app.clickPage({ ctrlKey: true });
    app.document.menuOpen = true;
    app.clickPage();
    app.document.menuOpen = false;
    app.theme('dark');
    app.clickPage();
    assert.equal(app.sky.hidden, true);
    assert.equal(app.pending.size, 0);
    app.theme('newyear');
    app.clickPage();
    assert.equal(app.sky.hidden, false);
    app.game.start();
    app.clickPage();
    assert.equal(app.sky.hidden, true, 'the game takes over without launching decorative rockets');
    app.game.stop();
    app.clickPage();
    app.theme('light');
    assert.equal(app.sky.hidden, true);
    assert.equal(app.pending.size, 0);
});

test('page fireworks still fly and scatter sparks when reduced motion is requested', () => {
    const app = scene({ reducedMotion: true });
    app.clickPage();
    app.advance(100);
    const [x, y] = app.skyContext.translations[0];
    assert.equal(x, 340);
    assert.ok(y > 250 && y < 615);
    app.advance(100);
    assert.ok(app.skyContext.translations[0][1] < y);
    app.advance(500);
    assert.equal(app.skyContext.translations.length, 0);
    const sparks = app.skyContext.rects.filter(([, , width]) => width === 2.4);
    assert.ok(sparks.some(([left]) => left < 339));
    assert.ok(sparks.some(([left]) => left > 339));
    app.advance(1800);
    assert.equal(app.sky.hidden, true);
    assert.equal(app.pending.size, 0);
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

test('six programming target kinds appear, and a merge conflict requires two physical hits', () => {
    const app = scene({ noTargets: true }); app.game.start();
    const kinds = new Set(app.game.state().falling.map(target => target.type));
    for (let i = 0; i < 5; i++) { app.advance(1400); app.game.state().falling.forEach(target => kinds.add(target.type)); }
    assert.deepEqual([...kinds].sort(), ['bug', 'build', 'merge', 'null', 'syntax', 'test']);
    app.game.stop();
    const conflictApp = scene({ noTargets: true }); conflictApp.game.start(); conflictApp.advance(1000);
    const conflict = conflictApp.game.state().falling.find(target => target.type === 'merge');
    assert.ok(conflict); assert.equal(conflict.hp, 2);
    conflictApp.aim(clearTargetX(conflictApp.game.state(), conflict)); conflictApp.key(' '); conflictApp.keyUp(' ');
    let damaged;
    for (let i = 0; i < 80; i++) {
        conflictApp.advance(20);
        damaged = conflictApp.game.state().falling.find(target => target.id === conflict.id);
        if (damaged && damaged.hp === 1) break;
    }
    assert.ok(damaged && damaged.hp === 1, 'the first firework damages the conflict');
    assert.equal(conflictApp.game.state().score, 0, 'partial damage does not award destruction points');
    conflictApp.aim(clearTargetX(conflictApp.game.state(), damaged)); conflictApp.key(' '); conflictApp.keyUp(' ');
    for (let i = 0; i < 80 && conflictApp.game.state().score === 0; i++) conflictApp.advance(20);
    assert.equal(conflictApp.game.state().score, 150);
    assert.equal(conflictApp.game.state().hits, 1);
    conflictApp.game.stop();
});

test('fireworks in another column cannot damage a falling target', () => {
    const app = scene({ noTargets: true }); app.game.start();
    app.aim(250); app.key(' '); app.keyUp(' '); app.advance(1000);
    assert.equal(app.game.state().score, 0);
    assert.equal(app.game.state().hits, 0);
    assert.ok(app.game.state().falling.every(target => target.hp > 0));
    app.game.stop();
});

test('several minutes of spawning and held fire keep render resources bounded', () => {
    const app = scene({ noTargets: true }); app.game.start(); app.key(' ');
    app.advance(360000, 50);
    const state = app.game.state();
    assert.equal(state.running, true);
    assert.ok(state.spawned > 500); assert.ok(state.escaped > 0);
    assert.ok(state.fallingCount <= 28); assert.ok(state.projectileCount <= 72); assert.ok(state.particleCount <= 720);
    assert.ok(state.burstCount <= 12); assert.ok(state.labelCount <= 20); assert.ok(state.pickupCount <= 8);
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

test('reduced motion keeps shooting and static explosion feedback without moving sparks', () => {
    const normal = scene(); normal.game.start(); normal.fireAtTarget();
    const reduced = scene({ reducedMotion: true }); reduced.game.start(); reduced.fireAtTarget();
    assert.equal(normal.game.state().score, 100); assert.equal(reduced.game.state().score, 100);
    assert.ok(normal.game.state().particleCount > 0); assert.equal(reduced.game.state().particleCount, 0);
    assert.ok(reduced.game.state().burstCount > 0, 'reduced motion still communicates the hit');
    const still = reduced.game.state().bursts[0], expanding = normal.game.state().bursts[0];
    reduced.advance(100); normal.advance(100);
    assert.equal(reduced.game.state().bursts[0].static, true);
    assert.equal(reduced.game.state().bursts[0].radius, still.radius);
    assert.ok(normal.game.state().bursts[0].radius > expanding.radius);
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


test('the first destruction drops a boost that activates by being shot and has no free score', () => {
    const app = scene(); app.game.start(); app.fireAtTarget();
    let state = app.game.state();
    assert.equal(state.pickupCount, 1);
    assert.equal(state.pickups[0].type, 'multishot');
    assert.equal(state.boosts.multishot, 0);
    const score = state.score, hits = state.hits;
    const pickup = state.pickups[0]; app.aim(pickup.x + pickup.width / 2); app.key(' '); app.keyUp(' ');
    for (let i = 0; i < 100 && !app.game.state().boosts.multishot; i++) app.advance(20);
    state = app.game.state();
    assert.ok(state.boosts.multishot > 0 && state.boosts.multishot <= 12);
    assert.equal(state.score, score); assert.equal(state.hits, hits);
    const chip = app.fields['[data-newyear-boost="multishot"]'];
    assert.equal(chip.hidden, false); assert.ok(chip.style['--boost-left']);
    assert.equal(app.fields['[data-newyear-boost-empty]'].hidden, true);
    app.game.stop();
});

test('a falling boost can be collected with the mascot without firing at it', () => {
    const app = scene(); app.game.start(); app.fireAtTarget();
    const pickup = app.game.state().pickups[0]; assert.ok(pickup);
    const desiredPlayerCenter = pickup.x + pickup.width / 2;
    app.aim(desiredPlayerCenter + (app.game.state().fireOriginX - app.game.state().playerX));
    for (let i = 0; i < 600 && !app.game.state().boosts.multishot; i++) app.advance(20);
    assert.ok(app.game.state().boosts.multishot > 0, 'touching the capsule activates it');
    assert.equal(app.game.state().score, 100, 'catching a boost does not score as another target');
    app.game.stop();
});

test('multi-shot fires a fan of three and mega fireworks become larger and deal more damage', () => {
    const base = scene({ noTargets: true }); base.game.start(); shootOnce(base);
    const ordinary = base.game.state().rockets[0]; assert.ok(ordinary); base.game.stop();
    const app = startChargedScene(); chargeThrough(app, 'mega'); clearShootingColumn(app);
    const before = app.game.state().projectileCount; const fired = shootOnce(app);
    const volley = fired.rockets.slice(before);
    assert.equal(volley.length, 3);
    assert.ok(volley.some(projectile => projectile.vx < 0));
    assert.ok(volley.some(projectile => projectile.vx === 0));
    assert.ok(volley.some(projectile => projectile.vx > 0));
    assert.ok(volley.every(projectile => projectile.radius > ordinary.radius));
    assert.ok(volley.every(projectile => projectile.damage >= 2));
    app.game.stop();
});

test('rapid fire increases firing cadence, comet pierces, and speed boosts real movement', () => {
    const app = startChargedScene(); chargeThrough(app, 'speed'); clearShootingColumn(app);
    const charged = app.game.state();
    assert.ok(Object.values(charged.boosts).every(duration => duration > 0), 'different boost kinds stack');
    const existing = app.game.state().projectileCount; app.key(' ');
    const freshVolley = app.game.state().rockets.slice(existing);
    assert.equal(freshVolley.length, 3);
    assert.ok(freshVolley.every(projectile => projectile.pierceLeft === 3 && projectile.kind === 'comet'));
    app.advance(170); app.keyUp(' ');
    const volley = app.game.state().rockets;
    assert.ok(volley.length >= 6, 'rapid fire launches more than one triple volley in 170ms');
    const base = scene({ noTargets: true }); base.game.start(); shootOnce(base, 400);
    const ordinary = base.game.state().rockets[0];
    assert.ok(Math.abs(volley.find(projectile => projectile.vx === 0).vy) > Math.abs(ordinary.vy));
    app.aim(400); base.aim(400); app.advance(20); base.advance(20);
    const chargedX = app.game.state().playerX, normalX = base.game.state().playerX;
    app.key('ArrowRight'); base.key('ArrowRight'); app.advance(100); base.advance(100);
    const movedFast = app.game.state().playerX - chargedX, movedNormal = base.game.state().playerX - normalX;
    assert.ok(movedFast > movedNormal * 1.3, 'speed changes actual movement, not only its HUD label');
    app.keyUp('ArrowRight'); base.keyUp('ArrowRight'); app.game.stop(); base.game.stop();
});

test('boost duration pauses with the hidden tab, expires during active play, and restart clears it', () => {
    const app = startChargedScene(); chargeThrough(app, 'mega'); clearShootingColumn(app);
    const before = app.game.state().boosts;
    app.visibility(true); app.advance(60000);
    assert.deepEqual(app.game.state().boosts, before);
    app.visibility(false); app.advance(20); app.advance(12500);
    assert.ok(Object.values(app.game.state().boosts).every(duration => duration === 0));
    assert.equal(app.fields['[data-newyear-boost="mega"]'].hidden, true);
    const control = scene({ noTargets: true }); control.game.start(); const ordinary = shootOnce(control).rockets[0];
    const normalShot = shootOnce(app).rockets;
    assert.equal(normalShot.length, 1); assert.equal(normalShot[0].damage, ordinary.damage); assert.equal(normalShot[0].radius, ordinary.radius);
    assert.equal(normalShot[0].pierceLeft, ordinary.pierceLeft); assert.equal(app.game.state().fireCooldown, control.game.state().fireCooldown);
    control.game.stop();
    for (const target of app.interfaceTargets) target.isConnected = true;
    app.click('restart'); chargeThrough(app, 'multishot'); app.click('restart');
    assert.ok(Object.values(app.game.state().boosts).every(duration => duration === 0));
    assert.equal(app.game.state().pickupCount, 0);
    app.game.stop();
    assert.equal(app.game.state().projectileCount, 0); assert.equal(app.game.state().burstCount, 0);
});

test('a repeated boost refreshes its duration without extending it beyond twelve seconds', () => {
    const app = startChargedScene(); chargeThrough(app, 'multishot'); app.advance(2500);
    const before = app.game.state().boosts.multishot; assert.ok(before < 11);
    app.aim(400); app.key(' ');
    let previous = before, refreshed = false;
    for (let i = 0; i < 700; i++) {
        app.advance(20); const remaining = app.game.state().boosts.multishot;
        if (remaining > previous + .1) { refreshed = true; assert.ok(remaining <= 12); break; }
        previous = remaining;
    }
    app.keyUp(' '); assert.ok(refreshed, 'another earned capsule refreshes the same boost');
    app.game.stop();
});

test('firework hits form a persistent radial bouquet with trails and then fade away', () => {
    const app = scene(); app.game.start(); app.fireAtTarget();
    let state = app.game.state();
    assert.ok(state.burstCount > 0); assert.ok(state.particleCount >= 20);
    const origin = state.bursts[0]; assert.ok(origin);
    app.advance(100); state = app.game.state();
    const left = state.particles.some(particle => particle.x < origin.x), right = state.particles.some(particle => particle.x > origin.x);
    const up = state.particles.some(particle => particle.y < origin.y), down = state.particles.some(particle => particle.y > origin.y);
    assert.ok(left && right && up && down, 'sparks expand in every direction');
    app.advance(400); state = app.game.state();
    assert.ok(state.burstCount > 0); assert.ok(state.particleCount > 0);
    assert.ok(state.particles.some(particle => particle.previousX !== particle.x || particle.previousY !== particle.y), 'sparks retain a trailing segment');
    for (const target of app.interfaceTargets) target.isConnected = false;
    app.advance(2500);
    assert.equal(app.game.state().particleCount, 0); assert.equal(app.game.state().burstCount, 0);
    app.game.stop();
});

test('destroyed test and build targets show their programming success messages', () => {
    for (const [kind, message] of [['test', 'PASS'], ['build', 'Build succeeded']]) {
        const app = scene({ noTargets: true }); app.game.start();
        let target;
        for (let i = 0; i < 100 && !target; i++) { app.advance(100); target = app.game.state().falling.find(item => item.type === kind); }
        assert.ok(target, kind + ' target spawns');
        app.aim(clearTargetX(app.game.state(), target)); app.key(' ');
        let success = false;
        for (let i = 0; i < 160; i++) {
            app.advance(20);
            if (app.game.state().labels.some(label => label.text === message)) { success = true; break; }
        }
        app.keyUp(' '); assert.ok(success, message + ' is displayed after physical destruction'); app.game.stop();
    }
});


test('a comet firework physically pierces three separate interface targets in one shot', () => {
    const app = scene({ targetCount: 30, reserveCometTargets: true }); app.game.start();
    chargeThrough(app, 'comet'); clearShootingColumn(app);
    let waits = 0;
    while (app.game.state().boosts.multishot > 0 && waits++ < 800) app.advance(20);
    assert.equal(app.game.state().boosts.multishot, 0, 'the older fan boost expires naturally');
    assert.ok(app.game.state().boosts.comet > 0, 'the later comet boost is still active');
    const groups = Array.from({ length: 10 }, (_, index) => app.cometTargets.slice(index * 3, index * 3 + 3));
    const selected = groups.find(group => {
        const x = group[0].getBoundingClientRect().left + 10;
        return !app.game.state().falling.some(item => item.y + item.height > 300 && x >= item.x - 8 && x <= item.x + item.width + 8);
    });
    assert.ok(selected, 'one physical shooting column is clear of unrelated falling targets');
    for (const target of selected) target.isConnected = true;
    const before = app.game.state(); assert.equal(before.remaining, 3);
    const launched = shootOnce(app, selected[0].getBoundingClientRect().left + 10);
    assert.equal(launched.projectileCount - before.projectileCount, 1);
    for (let i = 0; i < 100 && app.game.state().remaining > 0; i++) app.advance(20);
    assert.ok(selected.every(target => target.classList.contains('newyear-destroyed')));
    assert.equal(app.game.state().hits - before.hits, 3);
    assert.equal(app.game.state().score - before.score, 300);
    app.game.stop();
});


test('a larger firework physically catches the edge that an ordinary shot misses', () => {
    const app = scene({ targetCount: 30, reserveRadiusTargets: true }); app.game.start();
    chargeThrough(app, 'mega'); clearShootingColumn(app);
    let waits = 0;
    while (app.game.state().boosts.multishot > 0 && waits++ < 800) app.advance(20);
    assert.equal(app.game.state().boosts.multishot, 0);
    assert.ok(app.game.state().boosts.mega > 0);
    const selected = app.radiusTargets.find(target => {
        const x = target.getBoundingClientRect().right + 4;
        return !app.game.state().falling.some(item => item.y + item.height > 340 && x >= item.x - 8 && x <= item.x + item.width + 8);
    });
    assert.ok(selected, 'there is an unobstructed edge to aim at'); selected.isConnected = true;
    const x = selected.getBoundingClientRect().right + 4;
    const before = app.game.state().projectileCount, powered = shootOnce(app, x);
    assert.equal(powered.projectileCount - before, 1);
    assert.equal(powered.rockets.at(-1).damage, 2);
    for (let i = 0; i < 100 && !selected.classList.contains('newyear-destroyed'); i++) app.advance(20);
    assert.equal(selected.classList.contains('newyear-destroyed'), true);
    const control = scene({ reserveRadiusTargets: true }); control.game.start();
    const ordinaryTarget = control.radiusTargets.find(target => target.getBoundingClientRect().right + 4 === x);
    shootOnce(control, x); control.advance(700);
    assert.equal(ordinaryTarget.classList.contains('newyear-destroyed'), false, 'the same near-miss stays a miss without the larger radius');
    app.game.stop(); control.game.stop();
});


test('a multi-shot side ray hits a target that the central firing column misses', () => {
    const app = scene({ targetCount: 60, reservePiercingTargets: true }); app.game.start();
    chargeThrough(app, 'multishot'); clearShootingColumn(app);
    const target = app.piercingTargets[6]; target.isConnected = true;
    const x = target.getBoundingClientRect().right + 21;
    const before = app.game.state(); const fired = shootOnce(app, x);
    assert.equal(fired.projectileCount - before.projectileCount, 3);
    for (let i = 0; i < 100 && !target.classList.contains('newyear-destroyed'); i++) app.advance(20);
    assert.equal(target.classList.contains('newyear-destroyed'), true);
    const control = scene({ reservePiercingTargets: true }); control.game.start();
    const ordinaryTarget = control.piercingTargets[6]; shootOnce(control, x); control.advance(700);
    assert.equal(ordinaryTarget.classList.contains('newyear-destroyed'), false);
    app.game.stop(); control.game.stop();
});

test('the first programming targets fit separately inside a narrow mobile canvas', () => {
    const app = scene({ noTargets: true, viewportWidth: 320, canvasWidth: 305 }); app.game.start();
    const initial = app.game.state().falling.toSorted((a, b) => a.x - b.x);
    assert.equal(initial.length, 3);
    for (const target of initial) { assert.ok(target.x >= 0); assert.ok(target.x + target.width <= 305); }
    for (let i = 1; i < initial.length; i++) assert.ok(initial[i - 1].x + initial[i - 1].width <= initial[i].x);
    app.game.stop();
});
