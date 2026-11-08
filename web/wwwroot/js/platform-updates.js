// The native modal supplies focus trapping, Escape and focus restoration.
export function open(dialog) {
    if (!dialog || dialog.open || !dialog.isConnected) return;
    dialog.addEventListener('click', event => {
        if (event.target !== dialog) return;
        const box = dialog.getBoundingClientRect();
        if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) dialog.close();
    }, { signal: (dialog._updatesAbort = new AbortController()).signal });
    dialog.addEventListener('close', () => dialog._updatesAbort?.abort(), { once: true });
    dialog.showModal();
}

export function close(dialog) {
    if (dialog?.open) dialog.close();
}
