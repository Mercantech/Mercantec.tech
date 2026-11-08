const sessions = new WeakMap();

export function mount(dialog, callback) {
    if (!(dialog instanceof HTMLDialogElement) || !dialog.isConnected) return;
    destroy(dialog, false);
    const controller = new AbortController();
    const previous = document.activeElement;
    const dismiss = () => { callback.invokeMethodAsync('Dismiss').catch(() => {}); };
    dialog.addEventListener('close', dismiss, { signal: controller.signal });
    dialog.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const bounds = dialog.getBoundingClientRect();
        if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    }, { signal: controller.signal });
    sessions.set(dialog, { controller, previous });
    if (!dialog.open) dialog.showModal();
}

export function destroy(dialog, restoreFocus = true) {
    const session = sessions.get(dialog);
    session?.controller.abort();
    sessions.delete(dialog);
    if (dialog?.open) dialog.close();
    if (restoreFocus && session?.previous?.isConnected) session.previous.focus({ preventScroll: true });
}
