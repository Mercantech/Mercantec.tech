import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

async function fixture({ mobile = false, stored = null, storageBlocked = false, inert = false, overflow = '' } = {}) {
    const oldWindow = globalThis.window, oldDocument = globalThis.document;
    const calls = [], storage = new Map(stored === null ? [] : [['lup.notion.sidebar.collapsed', stored]]);
    const listeners = new Map(), mediaListeners = new Set();
    const styles = new Map(overflow ? [['overflow', [overflow, 'important']]] : []);
    const document = {
        activeElement: null,
        body: { style: {
            getPropertyValue: key => styles.get(key)?.[0] ?? '',
            getPropertyPriority: key => styles.get(key)?.[1] ?? '',
            setProperty: (key, value, priority = '') => styles.set(key, [value, priority]),
            removeProperty: key => styles.delete(key),
        } },
        addEventListener: (name, callback) => listeners.set(name, callback),
        removeEventListener: (name, callback) => { if (listeners.get(name) === callback) listeners.delete(name); },
    };
    function node(id = '') {
        const attributes = new Map();
        return {
            id, isConnected: true, tabIndex: 0, disabled: false,
            parent: null, focusCount: 0,
            get parentElement() { return this.parent; },
            focus() { document.activeElement = this; this.focusCount++; },
            getClientRects: () => [{}],
            hasAttribute: key => attributes.has(key),
            getAttribute: key => attributes.get(key) ?? null,
            setAttribute: (key, value) => attributes.set(key, value),
            removeAttribute: key => attributes.delete(key),
            contains(target) { for (; target; target = target.parent) if (target === this) return true; return false; },
            closest(selector) {
                for (let target = this; target; target = target.parent) {
                    if (selector === '[hidden]' && target.hasAttribute('hidden')) return target;
                    if (selector === 'button,a' && target.action) return target;
                    if (selector === '.notion-document-menu' && target.menu) return target;
                    if (selector.startsWith('[role=') && target.hasAttribute('role') && target.getAttribute('role') === 'dialog') return target;
                }
                return null;
            },
        };
    }
    const panel = node('notion-sidebar'), background = node('document'), toggle = node('toggle');
    panel.setAttribute('role', 'dialog');
    background.inert = inert;
    if (inert) background.setAttribute('inert', 'existing');
    const search = node('notion-page-search'), first = node('first'), last = node('last');
    search.value = '';
    for (const item of [first, search, last]) item.parent = panel;
    panel.querySelectorAll = () => [first, search, last];
    const more = node('more'), summary = node('more-summary'), menu = node('more-menu'), action = node('more-action');
    more.open = false; menu.menu = true; action.action = true;
    more.parent = background; summary.parent = more; menu.parent = more; action.parent = menu;
    more.querySelector = selector => selector === 'summary' ? summary : null;
    const root = { contains: target => [panel, background, toggle].some(node => node.contains(target)), querySelector: selector => ({
        '#notion-sidebar': panel, '#notion-page-search': search,
        '.notion-document': background, '.notion-sidebar-toggle': toggle,
        '.notion-document-more[open]': more.open ? more : null,
    })[selector] ?? null };
    const media = {
        matches: mobile,
        addEventListener: (name, callback) => mediaListeners.add(callback),
        removeEventListener: (name, callback) => mediaListeners.delete(callback),
    };
    const window = {
        matchMedia: query => { assert.equal(query, '(max-width: 760px)'); return media; },
        localStorage: {
            getItem: key => { if (storageBlocked) throw new Error('blocked'); return storage.get(key) ?? null; },
            setItem: (key, value) => { if (storageBlocked) throw new Error('blocked'); storage.set(key, value); },
        },
    };
    globalThis.window = window; globalThis.document = document;
    const source = await readFile(new URL('../../web/wwwroot/js/notion-sidebar.js', import.meta.url), 'utf8');
    const module = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64') + '#' + Math.random());
    const callbacks = { invokeMethodAsync: async (...args) => { calls.push(args); } };
    function key(key, target, options = {}) {
        const event = { key, target, shiftKey: false, defaultPrevented: false,
            preventDefault() { this.defaultPrevented = true; }, ...options };
        listeners.get('keydown')?.(event);
        return event;
    }
    return {
        module, root, panel, background, toggle, search, first, last, more, summary, menu, action, document, styles, storage, calls, mediaListeners, listeners, key, makeNode: node,
        mount: () => module.mount(root, callbacks),
        click: target => listeners.get('click')?.({ target }),
        resize(next) { media.matches = next; for (const callback of mediaListeners) callback(); },
        cleanup() { module.destroy(root); globalThis.window = oldWindow; globalThis.document = oldDocument; },
    };
}

test('desktop preference is a single boolean and does not affect the document', async () => {
    const f = await fixture({ stored: 'true' });
    try {
        assert.deepEqual(f.mount(), { mobile: false, desktopOpen: false });
        f.document.activeElement = f.background;
        f.module.setOpen(f.root, false);
        assert.equal(f.toggle.focusCount, 0, 'initial state must not steal focus');
        assert.equal(f.background.inert, false);
        assert.equal(f.styles.size, 0);
        f.module.setOpen(f.root, true);
        assert.deepEqual([...f.storage], [['lup.notion.sidebar.collapsed', 'false']]);
        assert.equal(f.document.activeElement, f.background);
        assert.deepEqual(f.calls, []);
    } finally { f.cleanup(); }
});

test('mobile opening traps focus, makes the document inert and restores original state on closing', async () => {
    const f = await fixture({ mobile: true, inert: true, overflow: 'clip' });
    try {
        f.mount();
        f.module.setOpen(f.root, false);
        assert.equal(f.toggle.focusCount, 0);
        f.module.setOpen(f.root, true);
        assert.equal(f.document.activeElement, f.search);
        assert.equal(f.background.inert, true);
        assert.deepEqual(f.styles.get('overflow'), ['hidden', '']);
        assert.equal(f.storage.size, 0, 'mobile toggles must not change desktop preference');

        f.last.focus();
        assert.equal(f.key('Tab', f.last).defaultPrevented, true);
        assert.equal(f.document.activeElement, f.first);
        assert.equal(f.key('Tab', f.first, { shiftKey: true }).defaultPrevented, true);
        assert.equal(f.document.activeElement, f.last);
        f.background.focus();
        f.key('Tab', f.background);
        assert.equal(f.document.activeElement, f.first);

        f.module.setOpen(f.root, false);
        assert.equal(f.background.inert, true, 'preserve a previously inert background');
        assert.equal(f.background.getAttribute('inert'), 'existing');
        assert.deepEqual(f.styles.get('overflow'), ['clip', 'important']);
        assert.equal(f.document.activeElement, f.toggle);
    } finally { f.cleanup(); }
});

test('Escape leaves search handling to Blazor and respects nested dialogs before requesting one close elsewhere', async () => {
    const f = await fixture({ mobile: true });
    try {
        f.mount(); f.module.setOpen(f.root, true);
        f.search.value = 'database';
        assert.equal(f.key('Escape', f.search).defaultPrevented, false);
        f.search.value = '';
        assert.equal(f.key('Escape', f.search).defaultPrevented, false, 'a Blazor handler may already have cleared the input');
        f.key('Escape', f.first, { defaultPrevented: true });
        const dialog = { parent: f.panel, hasAttribute: key => key === 'role', getAttribute: () => 'dialog' };
        const nested = { parent: dialog, closest: () => dialog };
        f.key('Escape', nested);
        assert.equal(f.calls.length, 0);
        assert.equal(f.key('Escape', f.first).defaultPrevented, true);
        f.key('Escape', f.first);
        assert.deepEqual(f.calls, [['CloseNotionSidebar']]);
        await Promise.resolve();
    } finally { f.cleanup(); }
});

test('viewport changes preserve desktop preference and release mobile effects before notifying the host', async () => {
    const f = await fixture({ mobile: false });
    try {
        f.mount(); f.module.setOpen(f.root, false);
        f.resize(true);
        assert.deepEqual(f.calls, [['OnNotionSidebarViewport', true, false]]);
        f.module.setOpen(f.root, true);
        assert.equal(f.styles.get('overflow')[0], 'hidden');
        f.resize(false);
        assert.equal(f.background.inert, false);
        assert.equal(f.styles.size, 0);
        assert.deepEqual(f.calls.at(-1), ['OnNotionSidebarViewport', false, false]);
        f.resize(false);
        assert.equal(f.calls.length, 2, 'unchanged viewport must not retrigger the host');
        assert.equal(f.toggle.focusCount, 0, 'responsive changes must not steal focus');
    } finally { f.cleanup(); }
});

test('blocked storage still permits opening and closing', async () => {
    const f = await fixture({ storageBlocked: true });
    try {
        assert.deepEqual(f.mount(), { mobile: false, desktopOpen: true });
        assert.doesNotThrow(() => f.module.setOpen(f.root, false));
        assert.doesNotThrow(() => f.module.setOpen(f.root, true));
        assert.equal(f.storage.size, 0);
    } finally { f.cleanup(); }
});

test('destroy and remount remove old handlers and restore inert and scrolling', async () => {
    const f = await fixture({ mobile: true });
    try {
        f.mount(); f.module.setOpen(f.root, true);
        f.module.destroy(f.root);
        assert.equal(f.background.inert, false);
        assert.equal(f.background.hasAttribute('inert'), false);
        assert.equal(f.styles.size, 0);
        assert.equal(f.listeners.size, 0);
        assert.equal(f.mediaListeners.size, 0);
        f.module.setOpen(f.root, true);
        assert.equal(f.styles.size, 0);
        f.mount(); f.mount();
        assert.equal(f.listeners.size, 2);
        assert.equal(f.mediaListeners.size, 1);
    } finally { f.cleanup(); }
});

test('the scoped document menu closes on outside clicks, actions and Escape without dismissing another dialog', async () => {
    const f = await fixture();
    try {
        f.mount();
        f.more.open = true;
        f.click(f.summary);
        assert.equal(f.more.open, true, 'summary retains native toggle behavior');
        f.click(f.action);
        assert.equal(f.more.open, false, 'chosen actions close the menu');
        assert.equal(f.summary.focusCount, 0, 'clicking an action must not steal its focus');
        f.more.open = true;
        f.click(f.toggle);
        assert.equal(f.more.open, false, 'an outside click closes this workspace menu');

        f.more.open = true;
        assert.equal(f.key('Escape', f.action).defaultPrevented, true);
        assert.equal(f.more.open, false);
        assert.equal(f.document.activeElement, f.summary);
        assert.equal(f.calls.length, 0, 'closing More does not close the sidebar');
        f.more.open = true;
        f.key('Escape', f.action, { defaultPrevented: true });
        assert.equal(f.more.open, true);
        f.key('Escape', f.search);
        assert.equal(f.more.open, true, 'a different dialog owns its Escape');

        f.module.destroy(f.root);
        f.click(f.toggle);
        assert.equal(f.more.open, true, 'destroy removes delegated menu handlers');
    } finally { f.cleanup(); }
});

test('mobile Tab trapping includes a closed details summary and skips its hidden links even when they report layout boxes', async () => {
    const f = await fixture({ mobile: true });
    try {
        const tools = f.makeNode('tools'), summary = f.makeNode('tools-summary'), link = f.makeNode('tools-link');
        tools.localName = 'details'; tools.open = false; tools.parent = f.panel;
        summary.localName = 'summary'; summary.parent = tools;
        link.localName = 'a'; link.parent = tools;
        tools.children = [summary, link];
        f.panel.querySelectorAll = () => [f.first, f.search, f.last, summary, link];
        assert.equal(link.getClientRects().length, 1, 'closed native details children can still expose a layout box');
        f.mount(); f.module.setOpen(f.root, true);

        summary.focus();
        assert.equal(f.key('Tab', summary).defaultPrevented, true);
        assert.equal(f.document.activeElement, f.first);
        f.key('Tab', f.first, { shiftKey: true });
        assert.equal(f.document.activeElement, summary);

        tools.open = true;
        link.focus();
        assert.equal(f.key('Tab', link).defaultPrevented, true);
        assert.equal(f.document.activeElement, f.first, 'opening details includes its now visible final action');
    } finally { f.cleanup(); }
});
