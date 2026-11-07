const sessions = new WeakMap();

export function destroy(anchor) {
    sessions.get(anchor)?.abort();
    sessions.delete(anchor);
}

export function mount(anchor, panel) {
    if (!(anchor instanceof Element) || !(panel instanceof HTMLElement) ||
        !anchor.isConnected || !panel.isConnected) return;
    destroy(anchor);
    const controller = new AbortController();
    const { signal } = controller;
    sessions.set(anchor, controller);

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
        const flip = below < 220 && above > below;
        panel.style.top = flip ? "auto" : "calc(100% + 6px)";
        panel.style.bottom = flip ? "calc(100% + 6px)" : "auto";
        panel.style.maxHeight = Math.max(40, Math.min(220, flip ? above : below)) + "px";
        panel.style.transform = "";
        const bounds = panel.getBoundingClientRect();
        const shift = bounds.left < left ? left - bounds.left
            : bounds.right > right ? right - bounds.right : 0;
        panel.style.transform = `translateX(${shift}px)`;
    }

    fit();
    window.addEventListener("resize", fit, { signal, passive: true });
    window.addEventListener("scroll", event => {
        if (!(event.target instanceof Node) || !panel.contains(event.target)) fit();
    }, { signal, passive: true, capture: true });
    viewportListener("resize");
    viewportListener("scroll");

    function viewportListener(event) {
        window.visualViewport?.addEventListener(event, fit, { signal, passive: true });
    }
}
