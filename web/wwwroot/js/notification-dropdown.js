const watchers = new WeakMap();

export function watch(root, receiver) {
  unwatch(root);
  const panel = root?.querySelector('.notification-dropdown');
  if (!panel) return;
  let active = true;
  const dismiss = focus => {
    if (active) receiver.invokeMethodAsync('DismissNotifications', focus).catch(() => {});
  };
  const outside = event => { if (!root.contains(event.target)) dismiss(false); };
  const escape = event => {
    if (event.key === 'Escape') { event.preventDefault(); dismiss(true); }
  };
  const focusOut = event => { if (event.relatedTarget && !root.contains(event.relatedTarget)) dismiss(false); };
  const position = () => {
    panel.style.right = '0px';
    const bounds = panel.getBoundingClientRect();
    if (bounds.left < 12) panel.style.right = (bounds.left - 12) + 'px';
    else if (bounds.right > window.innerWidth - 12) panel.style.right = (bounds.right - window.innerWidth + 12) + 'px';
    panel.style.maxHeight = Math.max(0, Math.min(560, window.innerHeight - bounds.top - 12)) + 'px';
  };
  document.addEventListener('pointerdown', outside, true);
  document.addEventListener('keydown', escape);
  root.addEventListener('focusout', focusOut);
  window.addEventListener('resize', position);
  position();
  watchers.set(root, () => {
    active = false;
    document.removeEventListener('pointerdown', outside, true);
    document.removeEventListener('keydown', escape);
    root.removeEventListener('focusout', focusOut);
    window.removeEventListener('resize', position);
  });
}
export function unwatch(root) {
  watchers.get(root)?.();
  watchers.delete(root);
}
