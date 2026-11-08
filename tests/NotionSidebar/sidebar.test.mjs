import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

async function fixture({ mobile = false, stored = null, storedWidth = null, rootWidth = 1280, clientWidth = rootWidth, aiWidth = 0, edgeToEdge = false, storageBlocked = false, inert = false, overflow = '' } = {}) {
    const oldWindow = globalThis.window, oldDocument = globalThis.document;
    const calls = [], storage = new Map(stored === null ? [] : [['lup.notion.sidebar.collapsed', stored]]);
    const listeners = new Map(), mediaListeners = new Set();
    const rootListeners = new Map(), rootStyles = new Map(), classes = new Set(), events = [];
    if (storedWidth !== null) storage.set('lup.notion.sidebar.width.v1', storedWidth);
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
                    if (selector === '.notion-sidebar-resize' && target.resize) return target;
                    if (selector.startsWith('[role=') && target.hasAttribute('role') && target.getAttribute('role') === 'dialog') return target;
                }
                return null;
            },
        };
    }
    const panel = node('notion-sidebar'), background = node('document'), toggle = node('toggle');
    const handle = node('resize'), slot = node('ai-slot');
    handle.resize = true; handle.parent = panel;
    let captured = null;
    handle.setPointerCapture = id => { captured = id; };
    handle.hasPointerCapture = id => captured === id;
    handle.releasePointerCapture = () => { captured = null; };
    slot.getBoundingClientRect = () => ({ width: aiWidth });
    background.getBoundingClientRect = () => ({ width: clientWidth - Number.parseFloat(rootStyles.get('--notion-sidebar-width') || '268') - aiWidth });
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
    const root = { clientWidth, dataset: { focusMode: 'false', edgeToEdge: String(edgeToEdge) }, style: { getPropertyValue: key => rootStyles.get(key) || '', setProperty: (key, value) => rootStyles.set(key, value) },
        classList: { toggle: (key, active) => active ? classes.add(key) : classes.delete(key) },
        addEventListener: (name, callback) => rootListeners.set(name, callback), removeEventListener: (name, callback) => { if (rootListeners.get(name) === callback) rootListeners.delete(name); },
        dispatchEvent: event => { events.push(event); rootListeners.get(event.type)?.(event); },
        getBoundingClientRect: () => ({ width: rootWidth, top: 160 }),
        contains: target => [panel, background, toggle].some(node => node.contains(target)), querySelector: selector => ({
        '#notion-sidebar': panel, '#notion-page-search': search,
        '.notion-document': background, '.notion-sidebar-toggle': toggle,
        '.notion-document-more[open]': more.open ? more : null,
        '.notion-sidebar-resize': handle, '.notion-ai-slot': slot,
    })[selector] ?? null };
    const media = {
        matches: mobile,
        addEventListener: (name, callback) => mediaListeners.add(callback),
        removeEventListener: (name, callback) => mediaListeners.delete(callback),
    };
    const window = {
        innerWidth: rootWidth, innerHeight: 900,
        matchMedia: query => { assert.equal(query, '(max-width: 1000px)'); return media; },
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
        module, root, panel, background, toggle, search, first, last, more, summary, menu, action, document, styles, storage, calls, mediaListeners, listeners, rootListeners, rootStyles, classes, events, handle, key, makeNode: node,
        mount: () => module.mount(root, callbacks),
        click: target => listeners.get('click')?.({ target }),
        pointer(type, x, options = {}) { const event = { type, target: handle, button: 0, pointerId: 1, clientX: x, preventDefault() {}, ...options }; (type === 'pointerdown' ? rootListeners : listeners).get(type)?.(event); },
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
        assert.equal(f.listeners.size, 5);
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

test('focus hides side columns without overwriting open preferences and reveal restores their previous state', async () => {
    const f = await fixture();
    try {
        f.mount(); f.module.setOpen(f.root, true);
        const preferences = [...f.storage];
        assert.equal(f.module.toggleFocus(f.root), true);
        assert.equal(f.root.dataset.focusMode, 'true');
        assert.equal(f.classes.has('notion-focus-mode'), true);
        assert.deepEqual([...f.storage], preferences);
        assert.deepEqual(f.calls.at(-1), ['OnNotionFocusMode', true]);
        assert.equal(f.events.at(-1).detail.focused, true);
        f.root.dispatchEvent(new CustomEvent('notion-layout-reveal', { detail: { panel: 'ai' } }));
        assert.equal(f.root.dataset.focusMode, 'false');
        assert.deepEqual([...f.storage], preferences);
        assert.deepEqual(f.calls.at(-1), ['OnNotionFocusMode', false]);
        assert.equal(f.module.toggleFocus(f.root), true);
        assert.equal(f.module.toggleFocus(f.root), false);
    } finally { f.cleanup(); }
});

test('sidebar sizing respects the adjacent AI column, supports keyboard and pointer and stores only a bounded width', async () => {
    const f = await fixture({ rootWidth: 1102, clientWidth: 1100, aiWidth: 360, storedWidth: '9999' });
    try {
        f.mount(); f.module.setOpen(f.root, true);
        assert.equal(f.rootStyles.get('--notion-sidebar-width'), '380px', 'borders must not consume the document minimum or make the columns drift');
        assert.equal(f.handle.getAttribute('aria-valuemax'), '380');
        assert.equal(f.root.dataset.centerNarrow, 'true');
        assert.equal(f.rootStyles.get('--notion-workspace-height'), '724px');
        f.key('Home', f.handle);
        assert.equal(f.rootStyles.get('--notion-sidebar-width'), '180px');
        f.key('ArrowRight', f.handle, { shiftKey: true });
        assert.equal(f.rootStyles.get('--notion-sidebar-width'), '220px');
        f.pointer('pointerdown', 220); f.pointer('pointermove', 300); f.pointer('pointerup', 300);
        assert.equal(f.rootStyles.get('--notion-sidebar-width'), '300px');
        assert.equal(f.storage.get('lup.notion.sidebar.width.v1'), '300');
        f.pointer('pointerdown', 300); f.pointer('pointermove', 9999); f.key('Escape', f.handle);
        assert.equal(f.rootStyles.get('--notion-sidebar-width'), '300px', 'Escape cancels the active drag');
        f.pointer('pointerdown', 300); f.pointer('pointermove', 9999); f.pointer('pointercancel', 9999);
        assert.equal(f.rootStyles.get('--notion-sidebar-width'), '300px', 'pointer cancellation restores the previous width');
        f.key('End', f.handle);
        assert.equal(f.storage.get('lup.notion.sidebar.width.v1'), '380');
    } finally { f.cleanup(); }
});

test('focus releases mobile modal effects and restores them without touching desktop storage', async () => {
    const f = await fixture({ mobile: true });
    try {
        f.mount(); f.module.setOpen(f.root, true);
        assert.equal(f.background.inert, true);
        f.module.setFocus(f.root, true);
        assert.equal(f.background.inert, false);
        assert.equal(f.styles.size, 0);
        f.module.setFocus(f.root, false);
        assert.equal(f.background.inert, true);
        assert.equal(f.styles.get('overflow')[0], 'hidden');
        assert.equal(f.storage.size, 0);
    } finally { f.cleanup(); }
});

test('edge-to-edge workspaces use the full remaining viewport and follow the visible keyboard area', async () => {
    const f = await fixture({ edgeToEdge: true });
    try {
        f.mount();
        assert.equal(f.rootStyles.get('--notion-workspace-height'), '740px');
        globalThis.window.visualViewport = { height: 500, offsetTop: 20, addEventListener() {}, removeEventListener() {} };
        f.module.setOpen(f.root, true);
        assert.equal(f.rootStyles.get('--notion-workspace-height'), '360px');
        assert.equal(f.root.dataset.centerNarrow, 'false');
    } finally { f.cleanup(); }
});

test('opening center tools scrolls only the document and keeps column preferences and focus intact', async () => {
    const f = await fixture({ edgeToEdge: true });
    try {
        f.mount(); f.module.setOpen(f.root, true);
        const preferences = [...f.storage], widths = [...f.rootStyles];
        f.search.focus();
        f.background.scrollTop = 480;
        f.background.scrollTo = options => { f.background.scrollTop = options.top; };
        globalThis.window.scrollTo = () => { throw new Error('The page must not scroll when center tools open.'); };
        f.module.scrollCenterTop(f.root);
        assert.equal(f.background.scrollTop, 0);
        assert.equal(f.document.activeElement, f.search);
        assert.deepEqual([...f.storage], preferences);
        assert.deepEqual([...f.rootStyles], widths);
    } finally { f.cleanup(); }
});
