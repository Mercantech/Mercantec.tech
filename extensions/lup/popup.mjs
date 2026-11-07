const status = document.querySelector('#connection');
const heading = document.querySelector('#connection-heading');
const error = document.querySelector('#access-error');
const key = document.querySelector('#access-key');
const connect = document.querySelector('#connect');
const disconnect = document.querySelector('#disconnect');
const settings = document.querySelector('#connection-settings');
const summary = settings.querySelector('summary');
const card = document.querySelector('.connection-card');
const ready = document.querySelector('#ready-guide');
const scope = document.querySelector('#connection-account');
let busy = false;
let lastConnected = null;
let revision = 0;

function show(state) {
  const connected = state?.connected === true;
  const next = connected ? 'connected' : 'disconnected';
  document.body.dataset.state = card.dataset.state = next;
  document.body.setAttribute('aria-busy', 'false');
  heading.textContent = connected ? 'Du er klar' : 'Forbind LUP, og kom i gang';
  status.textContent = connected
    ? `Forbundet${state.email ? ' som ' + state.email : ''}.`
    : state?.message || 'Tilføj din personlige nøgle for at gemme emojis direkte herfra.';
  disconnect.hidden = ready.hidden = scope.hidden = !connected;
  summary.textContent = connected ? 'Administrer forbindelse' : 'Forbind din udvidelse';
  // A status update must never close an open form while someone is typing.
  if (lastConnected !== connected) settings.open = !connected;
  lastConnected = connected;
}

function setBusy(value, clearing = false) {
  busy = value;
  key.disabled = connect.disabled = disconnect.disabled = value;
  document.body.setAttribute('aria-busy', String(value));
  connect.textContent = value && !clearing ? 'Forbinder…' : 'Forbind LUP';
}

function showError(message) {
  error.textContent = message;
  error.hidden = false;
  settings.open = true;
}

document.querySelector('#access-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (busy) return;
  ++revision;
  error.hidden = true;
  const selection = [key.selectionStart, key.selectionEnd];
  let failed = false;
  setBusy(true);
  try {
    const response = await chrome.runtime.sendMessage({ type: 'settings:save', key: key.value });
    if (!response?.ok) throw new Error(response?.message || 'Forbindelsen kunne ikke gemmes.');
    key.value = '';
    show(response);
    heading.focus({ preventScroll: true });
  } catch (failure) {
    failed = true;
    showError(failure.message || 'LUP kunne ikke kontaktes. Prøv igen.');
  } finally {
    setBusy(false);
    if (failed) {
      key.focus({ preventScroll: true });
      if (selection.every(Number.isInteger)) key.setSelectionRange(...selection);
    }
  }
});

disconnect.addEventListener('click', async () => {
  if (busy) return;
  ++revision;
  error.hidden = true;
  setBusy(true, true);
  let cleared = false;
  try {
    const response = await chrome.runtime.sendMessage({ type: 'settings:clear' });
    if (!response?.ok) throw new Error('Forbindelsen kunne ikke fjernes. Prøv igen.');
    key.value = '';
    show({ connected: false, message: 'Forbindelsen er fjernet fra denne Chrome-profil. Du kan forbinde igen med en nøgle.' });
    cleared = true;
  } catch (failure) { showError(failure.message || 'Forbindelsen kunne ikke fjernes. Prøv igen.'); }
  finally { setBusy(false); if (cleared) key.focus(); }
});

const initialRevision = ++revision;
chrome.runtime.sendMessage({ type: 'settings:status' })
  .then(state => { if (revision === initialRevision && !busy) show(state); })
  .catch(() => { if (revision === initialRevision && !busy) show({ connected: false, message: 'LUP kunne ikke kontaktes. Prøv igen senere.' }); });
