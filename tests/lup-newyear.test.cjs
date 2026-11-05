const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { setMaxListeners } = require('node:events');
// Browsers do not impose Node's ten-listener warning on a shared abort signal.
class BrowserAbortController extends AbortController {
    constructor() { super(); setMaxListeners(0, this.signal); }
}
const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/lup-newyear.js'), 'utf8');

function scene({ reducedMotion = false, noTargets = false, protectedContent = null, viewportWidth = 800, canvasWidth = viewportWidth } = {}) {
    const pending = new Map();
    const observers = new Set();
    const closedEvents = [];
    let nextFrame = 0;
    const document = new EventTarget();
    document.hidden = false;
    document.documentElement = { dataset: { theme: 'newyear' } };
    const window = new EventTarget();
    const context = {
        rects: [],
        setTransform() {}, clearRect() { this.rects = []; this.rocketCount = 0; },
        beginPath() {}, moveTo(x) {
            if (this.fillStyle === "#e9f7fa") this.shipX = x;
            if (this.fillStyle === "#f6d889") this.rocketX = x;
        }, lineTo() {}, closePath() {},
        fill() { if (this.fillStyle === "#f6d889") this.rocketCount++; }, stroke() {}, strokeRect() {},
        fillRect(...rect) { this.rects.push(rect); }
    };
    function element(rect = {}) {
        const node = new EventTarget();
        const classes = new Set();
        node.dataset = {};
        node.isConnected = true;
        node.textContent = '';
        node.hidden = false;
        node.classList = { add: value => classes.add(value), remove: value => classes.delete(value), contains: value => classes.has(value) };
        node.getBoundingClientRect = () => ({ left: 390, top: 300, width: 20, height: 20, right: 410, bottom: 320, ...rect });
        node.closest = () => null;
        node.querySelector = () => null;
        node.contains = other => other === node;
        node.focus = () => { document.activeElement = node; };
        return node;
    }
    const focusBefore = element(); document.activeElement = focusBefore;
    const target = element();
    target.querySelector = selector => protectedContent && selector.split(",").includes(protectedContent) ? element() : null;
    const otherTarget = element({ left: 80, right: 110, top: 260, bottom: 280, width: 30 });
    const canvas = element({ left: 0, top: 0, width: canvasWidth, height: 600, right: canvasWidth, bottom: 600 });
    canvas.getContext = () => context;
    canvas.setPointerCapture = () => {};
    const result = element();
    const fields = Object.fromEntries(['score', 'time', 'remaining', 'status', 'result-title', 'result-copy'].map(name => ['[data-newyear-' + name + ']', element()]));
    fields['[data-newyear-result]'] = result;
    fields['[data-newyear-hud]'] = element({ bottom: 94 });
    fields['[data-newyear-controls]'] = element({ top: 520 });
    const replay = element(); replay.dataset.newyearAction = 'replay';
    fields['[data-newyear-result] [data-newyear-action="replay"]'] = replay;
    const dialog = element(); dialog.open = false;
    dialog.showModal = () => { dialog.open = true; };
    dialog.close = () => { dialog.open = false; closedEvents.push(() => dialog.dispatchEvent(new Event('close'))); };
    dialog.querySelector = selector => fields[selector] || null;
    document.getElementById = id => id === 'lup-newyear-game' ? dialog : id === 'lup-newyear-canvas' ? canvas : null;
    document.querySelectorAll = () => noTargets ? [] : [target, otherTarget];
    function emit(host, name, properties = {}) {
        const event = new Event(name, { cancelable: true });
        for (const [key, value] of Object.entries(properties)) Object.defineProperty(event, key, { value });
        host.dispatchEvent(event);
        return event;
    }
    const motion = { matches: reducedMotion };
    vm.runInNewContext(source, {
        window, document, AbortController: BrowserAbortController,
        MutationObserver: class {
            constructor(callback) { this.callback = callback; }
            observe() { observers.add(this.callback); }
            disconnect() { observers.delete(this.callback); }
        },
        matchMedia: () => motion,
        getComputedStyle: () => ({ visibility: 'visible', display: 'block', opacity: '1' }),
        innerWidth: viewportWidth, innerHeight: 600, devicePixelRatio: 1,
        requestAnimationFrame: callback => { const id = ++nextFrame; pending.set(id, callback); return id; },
        cancelAnimationFrame: id => pending.delete(id)
    });
    return {
        game: window.lupNewYear, target, otherTarget, canvas, dialog, document, context, pending, observers, focusBefore, fields,
        click(action) {
            const button = element(); button.dataset.newyearAction = action;
            button.closest = selector => selector === '[data-newyear-action]' || selector === 'button' ? button : null;
            return emit(document, 'click', { target: button });
        },
        key(key) { return emit(dialog, 'keydown', { key, target: canvas }); },
        pointerDown(action, pointerId) {
            const button = element(); button.dataset.newyearAction = action;
            button.closest = selector => selector === '[data-newyear-action]' ? button : null;
            return emit(dialog, 'pointerdown', { button: 0, pointerId, target: button });
        },
        pointerUp(pointerId) { return emit(window, 'pointerup', { pointerId }); },
        frame(now) { const callbacks = [...pending.values()]; pending.clear(); callbacks.forEach(callback => callback(now)); },
        fireAtTarget() {
            this.key(' ');
            for (let time = 1000; time <= 1800 && !target.classList.contains('newyear-destroyed'); time += 20) this.frame(time);
            emit(window, 'keyup', { key: ' ' });
        },
        visibility(hidden) { document.hidden = hidden; emit(document, 'visibilitychange'); },
        theme(value) { document.documentElement.dataset.theme = value; for (const callback of [...observers]) callback(); },
        flushClose() { closedEvents.splice(0).forEach(callback => callback()); },
        resize() { emit(window, 'resize'); }
    };
}

test('a rocket destroys a real interface target and awards points, then stop restores it and focus', () => {
    const app = scene();
    assert.equal(app.game.start(), true);
    assert.equal(app.game.state().total, 2);
    app.fireAtTarget();
    assert.equal(app.target.classList.contains('newyear-destroyed'), true);
    assert.equal(app.otherTarget.classList.contains('newyear-destroyed'), false);
    assert.equal(app.game.state().score, 100);
    assert.equal(app.game.state().remaining, 1);
    assert.equal(app.fields['[data-newyear-score]'].textContent, '100');
    app.game.stop();
    assert.equal(app.target.classList.contains('newyear-destroyed'), false);
    assert.equal(app.dialog.open, false);
    assert.equal(app.game.state().running, false);
    assert.equal(app.pending.size, 0);
    assert.equal(app.observers.size, 0);
    assert.equal(app.document.activeElement, app.focusBefore);
});

test('Escape and a theme change both restore destroyed interface nodes and cancel animation', () => {
    for (const end of ['escape', 'theme']) {
        const app = scene(); app.game.start(); app.fireAtTarget();
        assert.equal(app.target.classList.contains('newyear-destroyed'), true);
        if (end === 'escape') assert.equal(app.key('Escape').defaultPrevented, true);
        else app.theme('light');
        assert.equal(app.target.classList.contains('newyear-destroyed'), false);
        assert.equal(app.game.state().running, false);
        assert.equal(app.pending.size, 0);
    }
});

test('replay restores the targets and survives a queued close event from the previous round', () => {
    const app = scene(); app.click('start'); app.fireAtTarget();
    app.click('replay'); app.flushClose();
    assert.equal(app.game.state().running, true);
    assert.equal(app.game.state().score, 0);
    assert.equal(app.game.state().total, 2);
    assert.equal(app.target.classList.contains('newyear-destroyed'), false);
    assert.equal(app.dialog.open, true);
    assert.equal(app.pending.size, 1);
    assert.equal(app.game.start(), true);
    assert.equal(app.pending.size, 1);
    app.game.stop();
});

test('hidden tabs pause the countdown and clear held fire until the user resumes', () => {
    const app = scene(); app.game.start(); app.frame(1000); app.frame(2000);
    assert.equal(app.game.state().seconds, 59);
    app.key(' '); app.visibility(true);
    assert.equal(app.pending.size, 0);
    assert.equal(app.game.state().paused, true);
    app.visibility(false); app.frame(50000); app.frame(51000);
    assert.equal(app.game.state().seconds, 58);
    assert.equal(app.game.state().paused, false);
    app.game.stop();
});

test('reduced motion keeps shooting available while removing firework particles', () => {
    const normal = scene(); normal.game.start(); normal.fireAtTarget();
    const reduced = scene({ reducedMotion: true }); reduced.game.start(); reduced.fireAtTarget();
    assert.equal(normal.game.state().score, 100);
    assert.equal(reduced.game.state().score, 100);
    assert.ok(normal.context.rects.length > reduced.context.rects.length + 20);
    normal.game.stop(); reduced.game.stop();
});

test('the round ends after 60 seconds and replay can start a complete new round', () => {
    const app = scene(); app.game.start(); app.frame(1000); app.frame(61000);
    assert.equal(app.game.state().finished, true);
    assert.equal(app.game.state().seconds, 0);
    assert.equal(app.fields['[data-newyear-result]'].hidden, false);
    assert.equal(app.pending.size, 0);
    app.click('replay'); app.flushClose();
    assert.equal(app.game.state().finished, false);
    assert.equal(app.game.state().seconds, 60);
    assert.equal(app.fields['[data-newyear-result]'].hidden, true);
    app.game.stop();
});

test('resizing or a disconnected game canvas restores the page without leaving a running loop', () => {
    for (const end of ['resize', 'disconnect']) {
        const app = scene(); app.game.start(); app.fireAtTarget();
        if (end === 'resize') app.resize();
        else { app.canvas.isConnected = false; app.frame(2000); }
        assert.equal(app.target.classList.contains('newyear-destroyed'), false);
        assert.equal(app.game.state().running, false);
        assert.equal(app.pending.size, 0);
    }
});

test('the game does not start outside the new year theme and handles pages without targets', () => {
    const app = scene(); app.theme('light');
    assert.equal(app.game.start(), false);
    assert.equal(app.pending.size, 0);
    const empty = scene({ noTargets: true });
    assert.equal(empty.game.start(), true);
    assert.equal(empty.game.state().finished, true);
    assert.equal(empty.pending.size, 0);
    assert.equal(empty.fields['[data-newyear-result-title]'].textContent, 'Ingen mål i sigte');
    empty.game.stop();
});


test('touch steering and held fire survive release of a different finger independently', () => {
    const app = scene(); app.game.start(); app.frame(1000);
    app.pointerDown('left', 1); app.frame(1050);
    app.pointerDown('fire', 2); app.frame(1100);
    const beforeFireRelease = app.context.shipX;
    app.pointerUp(2); app.frame(1150);
    assert.ok(app.context.shipX < beforeFireRelease, 'left finger continues steering after fire finger lifts');
    app.pointerUp(1); app.frame(1200);
    const stopped = app.context.shipX; app.frame(1250);
    assert.equal(app.context.shipX, stopped, 'movement stops after its own finger lifts');
    app.pointerDown('fire', 3); app.pointerDown('right', 4); app.frame(1300);
    app.pointerUp(4);
    const afterMoveRelease = app.context.shipX;
    for (let time = 1350; time <= 2500; time += 50) app.frame(time);
    assert.equal(app.context.shipX, afterMoveRelease, 'fire finger does not keep movement active');
    assert.ok(app.context.rocketCount > 1, 'fire continues after the movement finger lifts');
    app.pointerUp(3);
    for (let time = 2550; time <= 3600; time += 50) app.frame(time);
    assert.equal(app.context.rocketCount, 0, 'fire stops after its own finger lifts');
    app.game.stop();
});

test('interface containers with embedded editors or input fields cannot become targets', () => {
    for (const protectedContent of ['iframe', 'input', 'textarea', 'select', '[contenteditable]']) {
        const app = scene({ protectedContent }); app.game.start();
        assert.equal(app.game.state().total, 1, protectedContent + ' container is excluded');
        app.fireAtTarget();
        assert.equal(app.target.classList.contains('newyear-destroyed'), false);
        assert.equal(app.game.state().score, 0);
        app.game.stop();
    }
});


test('canvas uses its CSS dimensions when a stable scrollbar gutter narrows the viewport', () => {
    const app = scene({ viewportWidth: 1440, canvasWidth: 1425 });
    app.target.getBoundingClientRect = () => ({ left: 703.5, top: 300, width: 18, height: 20, right: 721.5, bottom: 320 });
    app.game.start();
    assert.equal(app.canvas.width, 1425, 'backing pixels match the canvas CSS box rather than window.innerWidth');
    assert.equal(app.canvas.height, 600);
    assert.equal(app.context.shipX, 712.5, 'ship is centered within the rendered canvas');
    app.fireAtTarget();
    assert.equal(app.context.rocketX, 712.5, 'rendered rocket and DOM hitboxes share client-pixel coordinates');
    assert.equal(app.target.classList.contains('newyear-destroyed'), true);
    assert.equal(app.game.state().score, 100);
    app.game.stop();
});
