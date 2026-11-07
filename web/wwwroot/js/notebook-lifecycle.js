let callbacks = null;
const refresh = () => {
  if (document.visibilityState === 'visible') callbacks?.invokeMethodAsync('OnNotebookFocus').catch(() => {});
};
export function watch(next) {
  unwatch(); callbacks = next;
  window.addEventListener('focus', refresh);
  document.addEventListener('visibilitychange', refresh);
}
export function unwatch() {
  window.removeEventListener('focus', refresh);
  document.removeEventListener('visibilitychange', refresh);
  callbacks = null;
}
