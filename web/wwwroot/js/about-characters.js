const observers = new Map();
let nextObserver = 0;

export function observe(reference, element) {
    const notify = (method, value) => reference.invokeMethodAsync(method, value).catch(() => {});
    const visibility = () => notify("SetPageVisible", !document.hidden);
    const focusIn = () => notify("SetFocusWithin", true);
    const focusOut = event => {
        if (!element.contains(event.relatedTarget)) notify("SetFocusWithin", false);
    };
    document.addEventListener("visibilitychange", visibility);
    element.addEventListener("focusin", focusIn);
    element.addEventListener("focusout", focusOut);
    const id = ++nextObserver;
    observers.set(id, () => {
        document.removeEventListener("visibilitychange", visibility);
        element.removeEventListener("focusin", focusIn);
        element.removeEventListener("focusout", focusOut);
    });
    visibility();
    notify("SetFocusWithin", element.contains(document.activeElement));
    return id;
}

export function unobserve(id) {
    observers.get(id)?.();
    observers.delete(id);
}
