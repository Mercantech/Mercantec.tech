const status = document.querySelector('#connection');
const error = document.querySelector('#access-error');
const key = document.querySelector('#access-key');
const connect = document.querySelector('#connect');
const disconnect = document.querySelector('#disconnect');
function show(state) {
  status.textContent = state?.connected ? `Forbundet${state.email ? ' som ' + state.email : ''}. Du kan gemme emojis direkte fra udvidelsen.`
    : state?.message || 'Ikke forbundet endnu. Indsæt din personlige uploadnøgle.';
  disconnect.hidden = !state?.connected;
}
document.querySelector('#access-form').addEventListener('submit', async event => {
  event.preventDefault(); error.hidden = true; connect.disabled = key.disabled = true;
  try {
    const response = await chrome.runtime.sendMessage({ type: 'settings:save', key: key.value });
    if (!response?.ok) throw new Error(response?.message || 'Forbindelsen kunne ikke gemmes.');
    key.value = ''; show(response);
  } catch (failure) { error.textContent = failure.message || 'LUP kunne ikke kontaktes.'; error.hidden = false; }
  finally { connect.disabled = key.disabled = false; }
});
disconnect.addEventListener('click', async () => {
  await chrome.runtime.sendMessage({ type: 'settings:clear' });
  key.value = ''; show({ connected: false });
});
chrome.runtime.sendMessage({ type: 'settings:status' }).then(show).catch(() => show({ connected: false, message: 'LUP kunne ikke kontaktes. Prøv igen senere.' }));
