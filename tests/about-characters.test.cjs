const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function scene() {
    const target = () => ({
        listeners: new Map(),
        addEventListener(name, callback) { if (!this.listeners.has(name)) this.listeners.set(name, new Set()); this.listeners.get(name).add(callback); },
        removeEventListener(name, callback) { this.listeners.get(name)?.delete(callback); },
        emit(name, event = {}) { for (const callback of this.listeners.get(name) ?? []) callback(event); }
    });
    const document = { ...target(), hidden: false };
    const element = { ...target(), contains: item => item === 'inside' };
    const calls = [];
    const reference = { invokeMethodAsync: (method, value) => { calls.push([method, value]); return Promise.resolve(); } };
    const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/about-characters.js'), 'utf8')
        .replaceAll('export function ', 'function ');
    const context = { document };
    vm.runInNewContext(source + '\nthis.about = { observe, unobserve };', context);
    return { ...context.about, document, element, reference, calls };
}

test('Spotlight learns initial and changed page visibility without reading account state', () => {
    const app = scene();
    app.observe(app.reference, app.element);
    assert.deepEqual(app.calls, [['SetPageVisible', true], ['SetFocusWithin', false]]);
    app.document.hidden = true;
    app.document.emit('visibilitychange');
    assert.deepEqual(app.calls.at(-1), ['SetPageVisible', false]);
});

test('Keyboard movement inside the spotlight keeps the profile paused until focus actually leaves', () => {
    const app = scene();
    app.observe(app.reference, app.element);
    app.element.emit('focusin');
    app.element.emit('focusout', { relatedTarget: 'inside' });
    assert.deepEqual(app.calls.at(-1), ['SetFocusWithin', true]);
    app.element.emit('focusout', { relatedTarget: 'outside' });
    assert.deepEqual(app.calls.at(-1), ['SetFocusWithin', false]);
});

test('Disposing the spotlight removes page and focus listeners, including repeated cleanup', () => {
    const app = scene();
    const id = app.observe(app.reference, app.element);
    app.unobserve(id);
    app.unobserve(id);
    const before = app.calls.length;
    app.document.emit('visibilitychange');
    app.element.emit('focusin');
    app.element.emit('focusout');
    assert.equal(app.calls.length, before);
});
