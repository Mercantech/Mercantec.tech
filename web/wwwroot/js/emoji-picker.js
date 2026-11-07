const sessions = new WeakMap();

export function destroy(anchor, restoreFocus = false) {
    const session = sessions.get(anchor);
    if (session) {
        session.controller.abort();
        session.observer.disconnect();
        sessions.delete(anchor);
    }
    if (restoreFocus && anchor instanceof HTMLElement && anchor.isConnected)
        anchor.focus({ preventScroll: true });
}

export function mount(anchor, panel, callback, focusSearch = false) {
    if (!(anchor instanceof HTMLElement) || !(panel instanceof HTMLElement) ||
        !anchor.isConnected || !panel.isConnected) return false;
    let session = sessions.get(anchor);
    if (session?.panel !== panel) {
        destroy(anchor);
        session = createSession(anchor, panel, callback);
        sessions.set(anchor, session);
    }
    session.fit();
    const buttons = [...panel.querySelectorAll('.emoji-picker-option')];
    const active = buttons.includes(document.activeElement) ? document.activeElement : buttons[0];
    for (const button of buttons) button.tabIndex = button === active ? 0 : -1;
    if (focusSearch) {
        const search = panel.querySelector('.emoji-picker-upload-name:not(:disabled), .emoji-picker-search:not(:disabled)');
        if (search) {
            search.focus({ preventScroll: true });
            return true;
        }
    }
    return false;
}

function createSession(anchor, panel, callback) {
    const controller = new AbortController();
    const { signal } = controller;
    let closing = false;
    function close(restoreFocus) {
        if (closing || panel.dataset.uploadBusy === 'true') return;
        closing = true;
        callback.invokeMethodAsync('Dismiss', restoreFocus).catch(() => {}).finally(() => { closing = false; });
    }
    function fit() {
        if (!anchor.isConnected || !panel.isConnected) {
            destroy(anchor);
            return;
        }
        const viewport = window.visualViewport;
        const top = (viewport?.offsetTop ?? 0) + 8;
        const bottom = top + (viewport?.height ?? window.innerHeight) - 16;
        const left = (viewport?.offsetLeft ?? 0) + 8;
        const right = left + (viewport?.width ?? document.documentElement.clientWidth) - 16;
        const button = anchor.getBoundingClientRect();
        const above = button.top - top - 6;
        const below = bottom - button.bottom - 6;
        const desired = Math.min(390, bottom - top);
        const flip = below < desired && above > below;
        panel.style.top = flip ? 'auto' : 'calc(100% + 6px)';
        panel.style.bottom = flip ? 'calc(100% + 6px)' : 'auto';
        const available = flip ? above : below;
        panel.style.maxHeight = Math.max(0, available < 260 ? desired : Math.min(desired, available)) + 'px';
        panel.style.transform = '';
        const bounds = panel.getBoundingClientRect();
        const shiftX = bounds.left < left ? left - bounds.left
            : bounds.right > right ? right - bounds.right : 0;
        const shiftY = bounds.top < top ? top - bounds.top
            : bounds.bottom > bottom ? bottom - bounds.bottom : 0;
        panel.style.transform = `translate(${shiftX}px, ${shiftY}px)`;
    }
    function focusOption(button) {
        if (!button) return;
        for (const option of panel.querySelectorAll('.emoji-picker-option'))
            option.tabIndex = option === button ? 0 : -1;
        button.focus({ preventScroll: true });
        button.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    }
    panel.addEventListener('keydown', event => {
        if (event.isComposing) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            if (panel.dataset.uploadBusy === 'true') return;
            if (panel.dataset.uploadDraft === 'true') callback.invokeMethodAsync('CancelUploadDraft').catch(() => {});
            else close(true);
            return;
        }
        if (event.target.matches('.emoji-picker-upload input') && event.key === 'Enter') {
            event.preventDefault();
            event.stopPropagation();
            callback.invokeMethodAsync('SaveUpload').catch(() => {});
            return;
        }
        const options = [...panel.querySelectorAll('.emoji-picker-option')];
        if (event.target.matches('.emoji-picker-search')) {
            if (event.key === 'ArrowDown' || event.key === 'Enter') {
                event.preventDefault();
                event.stopPropagation();
                if (event.key === 'Enter') options[0]?.click();
                else focusOption(options[0]);
            }
            return;
        }
        const current = options.indexOf(event.target);
        if (current < 0) return;
        const rowWidth = options.filter(option => Math.abs(option.offsetTop - options[0].offsetTop) < 2).length || 1;
        let next;
        if (event.key === 'ArrowRight') next = Math.min(options.length - 1, current + 1);
        else if (event.key === 'ArrowLeft') next = Math.max(0, current - 1);
        else if (event.key === 'ArrowDown') next = Math.min(options.length - 1, current + rowWidth);
        else if (event.key === 'ArrowUp') next = current < rowWidth ? -1 : current - rowWidth;
        else if (event.key === 'Home') next = 0;
        else if (event.key === 'End') next = options.length - 1;
        if (next === undefined) return;
        event.preventDefault();
        event.stopPropagation();
        if (next < 0) panel.querySelector('.emoji-picker-search')?.focus({ preventScroll: true });
        else focusOption(options[next]);
    }, { signal });
    document.addEventListener('pointerdown', event => {
        if (event.target instanceof Node && !panel.contains(event.target) && !anchor.contains(event.target))
            close(false);
    }, { signal, capture: true });
    document.addEventListener('focusin', event => {
        if (event.target instanceof Node && !panel.contains(event.target) && !anchor.contains(event.target))
            close(false);
    }, { signal });
    window.addEventListener('resize', fit, { signal, passive: true });
    window.addEventListener('scroll', event => {
        if (!(event.target instanceof Node) || !panel.contains(event.target)) fit();
    }, { signal, passive: true, capture: true });
    window.visualViewport?.addEventListener('resize', fit, { signal, passive: true });
    window.visualViewport?.addEventListener('scroll', fit, { signal, passive: true });
    const observer = new ResizeObserver(fit);
    observer.observe(anchor);
    return { controller, panel, observer, fit };
}
