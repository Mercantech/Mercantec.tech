const workspaces = new WeakMap();
const preferenceKey = 'lup.notion.sidebar.collapsed';
const breakpoint = '(max-width: 760px)';

function readDesktopOpen() {
    try { return window.localStorage.getItem(preferenceKey) !== 'true'; }
    catch { return true; }
}

function invoke(state, method, ...args) {
    if (!state.active) return Promise.resolve();
    try { return Promise.resolve(state.callbacks?.invokeMethodAsync(method, ...args)).catch(() => {}); }
    catch { return Promise.resolve(); }
}

function focus(element) {
    if (element?.isConnected && typeof element.focus === 'function') element.focus({ preventScroll: true });
}

function hiddenByClosedDetails(element) {
    for (let parent = element.parentElement; parent; parent = parent.parentElement) {
        if (parent.localName !== 'details' || parent.open) continue;
        const summary = [...parent.children].find(child => child.localName === 'summary');
        if (!summary?.contains(element)) return true;
    }
    return false;
}

function focusable(panel) {
    return [...panel.querySelectorAll('a[href],button,input,select,textarea,summary,[tabindex]')]
        .filter(element => !element.disabled && element.tabIndex >= 0 && !element.closest('[hidden]')
            && !hiddenByClosedDetails(element) && element.getClientRects().length > 0
            && (typeof element.checkVisibility !== 'function' || element.checkVisibility({ visibilityProperty: true })));
}

function releaseModal(state) {
    const modal = state.modal;
    if (!modal) return;
    state.modal = null;
    modal.background.inert = modal.inertProperty;
    if (modal.hadInert) modal.background.setAttribute('inert', modal.inertAttribute);
    else modal.background.removeAttribute('inert');
    if (modal.overflow) modal.body.style.setProperty('overflow', modal.overflow, modal.overflowPriority);
    else modal.body.style.removeProperty('overflow');
}

function applyModal(state) {
    if (!state.mobile || !state.open) { releaseModal(state); return; }
    const background = state.root.querySelector('.notion-document');
    if (!background || !document.body) return;
    if (state.modal?.background === background) return;
    releaseModal(state);
    const body = document.body;
    state.modal = {
        background, body,
        hadInert: background.hasAttribute('inert'),
        inertAttribute: background.getAttribute('inert'),
        inertProperty: background.inert,
        overflow: body.style.getPropertyValue('overflow'),
        overflowPriority: body.style.getPropertyPriority('overflow'),
    };
    background.inert = true;
    background.setAttribute('inert', '');
    body.style.setProperty('overflow', 'hidden');
}

export function mount(root, callbacks) {
    destroy(root);
    if (!root?.querySelector) return null;
    const media = window.matchMedia(breakpoint);
    const state = {
        root, callbacks, media, active: true,
        mobile: media.matches, desktopOpen: readDesktopOpen(),
        open: false, applied: false, modal: null, closing: false,
    };
    const close = () => {
        if (state.closing || !state.open) return;
        state.closing = true;
        void invoke(state, 'CloseNotionSidebar').finally(() => { state.closing = false; });
    };
    const keydown = event => {
        if (!state.active || event.defaultPrevented || event.isComposing) return;
        // Blazor owns both clearing and closing from this field. Its handler may
        // already have cleared .value before this document listener runs.
        if (event.key === 'Escape' && event.target?.id === 'notion-page-search') return;
        if (event.key === 'Escape') {
            const more = root.querySelector('.notion-document-more[open]');
            const dialog = event.target?.closest?.('[role="dialog"],dialog[open]');
            if (more && (!dialog || more.contains(dialog))) {
                event.preventDefault();
                more.open = false;
                focus(more.querySelector('summary'));
                return;
            }
        }
        if (!state.mobile || !state.open) return;
        const panel = root.querySelector('#notion-sidebar');
        if (!panel) return;
        if (event.key === 'Escape') {
            if (!panel.contains(event.target)) return;
            // Nested dialogs, including the emoji picker, own their Escape key.
            const dialog = event.target?.closest?.('[role="dialog"],dialog[open]');
            if (dialog && dialog !== panel) return;
            event.preventDefault();
            close();
            return;
        }
        if (event.key !== 'Tab') return;
        const items = focusable(panel);
        if (!items.length) { event.preventDefault(); focus(panel); return; }
        const first = items[0], last = items.at(-1);
        if (!panel.contains(document.activeElement) || event.shiftKey && document.activeElement === first) {
            event.preventDefault(); focus(event.shiftKey ? last : first);
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault(); focus(first);
        }
    };
    const click = event => {
        if (!state.active) return;
        const more = root.querySelector('.notion-document-more[open]');
        if (!more) return;
        const menu = event.target?.closest?.('.notion-document-menu');
        const action = event.target?.closest?.('button,a');
        if (!more.contains(event.target) || menu && action && more.contains(action)) more.open = false;
    };
    const changed = () => {
        if (!state.active || media.matches === state.mobile) return;
        state.mobile = media.matches;
        // A responsive transition should not move focus out of the document.
        // .NET applies the new layout and calls setOpen after rendering it.
        state.open = false;
        releaseModal(state);
        void invoke(state, 'OnNotionSidebarViewport', state.mobile, state.desktopOpen);
    };
    state.keydown = keydown;
    state.click = click;
    state.changed = changed;
    if (media.addEventListener) media.addEventListener('change', changed);
    else media.addListener(changed);
    document.addEventListener('keydown', keydown);
    document.addEventListener('click', click);
    workspaces.set(root, state);
    return { mobile: state.mobile, desktopOpen: state.desktopOpen };
}

export function setOpen(root, open) {
    const state = workspaces.get(root);
    if (!state?.active) return;
    open = Boolean(open);
    const changed = state.open !== open;
    const wasApplied = state.applied;
    state.open = open;
    state.applied = true;
    if (!state.mobile) {
        state.desktopOpen = open;
        try { window.localStorage.setItem(preferenceKey, String(!open)); } catch { }
    }
    applyModal(state);
    if (state.mobile && changed && open) {
        const panel = root.querySelector('#notion-sidebar');
        focus(root.querySelector('#notion-page-search') ?? (panel && focusable(panel)[0]) ?? panel);
    } else if (changed && !open && wasApplied) {
        focus(root.querySelector('.notion-sidebar-toggle'));
    }
}

export function destroy(root) {
    const state = workspaces.get(root);
    if (!state) return;
    state.active = false;
    releaseModal(state);
    if (state.media.removeEventListener) state.media.removeEventListener('change', state.changed);
    else state.media.removeListener(state.changed);
    document.removeEventListener('keydown', state.keydown);
    document.removeEventListener('click', state.click);
    workspaces.delete(root);
}
