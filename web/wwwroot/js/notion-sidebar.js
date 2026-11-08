const workspaces = new WeakMap();
const preferenceKey = 'lup.notion.sidebar.collapsed';
const widthKey = 'lup.notion.sidebar.width.v1';
const breakpoint = '(max-width: 1000px)';

function readWidth() {
    try { const width = JSON.parse(window.localStorage.getItem(widthKey) || '268'); return typeof width === 'number' && Number.isFinite(width) ? Math.max(180, Math.min(420, width)) : 268; }
    catch { return 268; }
}

function setStyle(root, name, value) {
    if (root.style?.getPropertyValue(name) !== value) root.style?.setProperty(name, value);
}

function updateLayout(state) {
    if (!state.active) return;
    const { root } = state;
    const bounds = root.getBoundingClientRect?.() || { width: window.innerWidth || 1280, top: 0 };
    const ai = !state.focused ? root.querySelector('.notion-ai-slot')?.getBoundingClientRect?.().width || 0 : 0;
    state.maxWidth = Math.max(180, Math.min(420, (root.clientWidth || bounds.width) - ai - 360));
    state.width = Math.min(state.preferredWidth, state.maxWidth);
    setStyle(root, '--notion-sidebar-width', Math.round(state.width) + 'px');
    const handle = root.querySelector('.notion-sidebar-resize');
    handle?.setAttribute('aria-valuenow', String(Math.round(state.width)));
    handle?.setAttribute('aria-valuemax', String(Math.round(state.maxWidth)));
    handle?.setAttribute('aria-valuetext', Math.round(state.width) + ' pixels');
    const viewport = window.visualViewport;
    const height = viewport?.height || window.innerHeight || 800;
    const available = Math.max(160, height - Math.max(0, bounds.top - (viewport?.offsetTop || 0)) - 16);
    setStyle(root, '--notion-workspace-height', Math.round(available) + 'px');
    const center = root.querySelector('.notion-document')?.getBoundingClientRect?.();
    if (root.dataset && center) {
        const narrow = String(center.width < 600);
        if (root.dataset.centerNarrow !== narrow) root.dataset.centerNarrow = narrow;
    }
}

function scheduleLayout(state) {
    if (!state.active || state.frame) return;
    if (!window.requestAnimationFrame) { updateLayout(state); return; }
    state.frame = window.requestAnimationFrame(() => { state.frame = 0; updateLayout(state); });
}

function releaseResize(state, restore = false) {
    const drag = state.drag; state.drag = null;
    if (restore && drag) state.preferredWidth = drag.preferredWidth;
    if (drag?.handle.hasPointerCapture?.(drag.pointerId)) drag.handle.releasePointerCapture(drag.pointerId);
    if (state.root.dataset) state.root.dataset.sidebarResizing = 'false';
    updateLayout(state);
}

function persistWidth(state) {
    try { window.localStorage.setItem(widthKey, JSON.stringify(state.preferredWidth)); } catch { }
}

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
    if (!state.mobile || !state.open || state.focused) { releaseModal(state); return; }
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
        focused: root.dataset?.focusMode === 'true', preferredWidth: readWidth(), width: 268, maxWidth: 420, drag: null, frame: 0,
    };
    const close = () => {
        if (state.closing || !state.open) return;
        state.closing = true;
        void invoke(state, 'CloseNotionSidebar').finally(() => { state.closing = false; });
    };
    const keydown = event => {
        if (!state.active || event.defaultPrevented || event.isComposing) return;
        if (event.key === 'Escape' && state.drag) { event.preventDefault(); releaseResize(state, true); return; }
        if (event.target?.closest?.('.notion-sidebar-resize') && !state.mobile && !state.focused) {
            const step = event.shiftKey ? 40 : 20;
            const next = event.key === 'Home' ? 180 : event.key === 'End' ? state.maxWidth
                : event.key === 'ArrowRight' ? state.width + step : event.key === 'ArrowLeft' ? state.width - step : null;
            if (next !== null) { event.preventDefault(); state.preferredWidth = Math.max(180, Math.min(state.maxWidth, next)); updateLayout(state); persistWidth(state); return; }
        }
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
        if (!state.mobile || !state.open || state.focused) return;
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
        releaseResize(state);
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
    const down = event => {
        const handle = event.target?.closest?.('.notion-sidebar-resize');
        if (!handle || state.mobile || state.focused || !state.open || event.button !== 0) return;
        event.preventDefault(); focus(handle);
        state.drag = { pointerId: event.pointerId, x: event.clientX, width: state.width, preferredWidth: state.preferredWidth, handle };
        handle.setPointerCapture?.(event.pointerId);
        if (root.dataset) root.dataset.sidebarResizing = 'true';
    };
    const move = event => {
        if (!state.drag || state.drag.pointerId !== event.pointerId) return;
        state.preferredWidth = Math.max(180, Math.min(state.maxWidth, state.drag.width + event.clientX - state.drag.x));
        updateLayout(state);
    };
    const up = event => {
        if (!state.drag || state.drag.pointerId !== event.pointerId) return;
        releaseResize(state, event.type === 'pointercancel');
        if (event.type !== 'pointercancel') persistWidth(state);
    };
    const lost = event => { if (state.drag?.pointerId === event.pointerId) releaseResize(state, true); };
    const blur = () => releaseResize(state, true);
    const reveal = event => {
        setFocus(root, false);
        if (event.detail?.panel === 'ai' && state.mobile && state.open) close();
    };
    state.down = down; state.move = move; state.up = up; state.lost = lost; state.blur = blur; state.reveal = reveal;
    state.layout = () => scheduleLayout(state);
    root.addEventListener?.('pointerdown', down);
    root.addEventListener?.('notion-layout-reveal', reveal);
    root.addEventListener?.('lostpointercapture', lost);
    document.addEventListener('pointermove', move);
    document.addEventListener('pointerup', up);
    document.addEventListener('pointercancel', up);
    window.addEventListener?.('resize', state.layout);
    window.addEventListener?.('blur', blur);
    window.addEventListener?.('scroll', state.layout, { passive: true });
    window.visualViewport?.addEventListener('resize', state.layout);
    window.visualViewport?.addEventListener('scroll', state.layout);
    if (typeof ResizeObserver !== 'undefined') {
        state.observer = new ResizeObserver(state.layout);
        for (const element of [root, root.querySelector('.notion-document'), root.querySelector('.notion-ai-slot')]) if (element) state.observer.observe(element);
    }
    updateLayout(state);
    return { mobile: state.mobile, desktopOpen: state.desktopOpen };
}

export function setFocus(root, focused) {
    const state = workspaces.get(root);
    if (!state?.active) return false;
    focused = Boolean(focused);
    const changed = state.focused !== focused;
    state.focused = focused;
    root.classList?.toggle('notion-focus-mode', focused);
    if (root.dataset) root.dataset.focusMode = String(focused);
    releaseResize(state);
    applyModal(state);
    if (changed) {
        if (typeof CustomEvent === 'function') root.dispatchEvent?.(new CustomEvent('notion-layout-focus', { detail: { focused } }));
        void invoke(state, 'OnNotionFocusMode', focused);
    }
    return focused;
}

export function toggleFocus(root) { return setFocus(root, !workspaces.get(root)?.focused); }

export function setOpen(root, open) {
    const state = workspaces.get(root);
    if (!state?.active) return;
    open = Boolean(open);
    const changed = state.open !== open;
    const wasApplied = state.applied;
    if (open && changed && wasApplied) setFocus(root, false);
    state.open = open;
    state.applied = true;
    if (!open) releaseResize(state);
    if (!state.mobile) {
        state.desktopOpen = open;
        try { window.localStorage.setItem(preferenceKey, String(!open)); } catch { }
    }
    applyModal(state);
    scheduleLayout(state);
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
    releaseResize(state);
    releaseModal(state);
    if (state.media.removeEventListener) state.media.removeEventListener('change', state.changed);
    else state.media.removeListener(state.changed);
    document.removeEventListener('keydown', state.keydown);
    document.removeEventListener('click', state.click);
    root.removeEventListener?.('pointerdown', state.down);
    root.removeEventListener?.('notion-layout-reveal', state.reveal);
    root.removeEventListener?.('lostpointercapture', state.lost);
    document.removeEventListener('pointermove', state.move);
    document.removeEventListener('pointerup', state.up);
    document.removeEventListener('pointercancel', state.up);
    window.removeEventListener?.('resize', state.layout);
    window.removeEventListener?.('blur', state.blur);
    window.removeEventListener?.('scroll', state.layout);
    window.visualViewport?.removeEventListener('resize', state.layout);
    window.visualViewport?.removeEventListener('scroll', state.layout);
    if (state.frame) window.cancelAnimationFrame?.(state.frame);
    state.observer?.disconnect();
    workspaces.delete(root);
}
