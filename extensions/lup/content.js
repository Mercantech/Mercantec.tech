(() => {
  if (window.top !== window || location.origin !== 'https://lup.dk') return;
  const ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  let inFlight = false, deliveredId = null;
  window.addEventListener('message', async event => {
    if (event.source !== window || event.origin !== location.origin || location.pathname !== '/emojis') return;
    const data = event.data;
    const query = new URLSearchParams(location.search);
    if (!data || data.source !== 'lup-emoji-page' || !['request', 'complete'].includes(data.type)
      || !ID.test(data.id ?? '')) return;
    if (data.type === 'complete' ? deliveredId !== data.id
      : query.getAll('lupImport').length !== 1 || query.get('lupImport') !== data.id) return;
    if (inFlight) return;
    inFlight = true;
    try {
      let response;
      for (let attempt = 0; attempt < 8; attempt++) {
        response = await chrome.runtime.sendMessage({ type: `emoji:${data.type}`, id: data.id });
        if (!response?.retry) break;
        await new Promise(resolve => setTimeout(resolve, 250));
      }
      if (data.type === 'complete') { if (response?.ok) deliveredId = null; return; }
      if (response?.ok) deliveredId = data.id;
      window.postMessage(response?.ok
        ? { source: 'lup-emoji-extension', type: 'draft', id: data.id, name: response.name, contentType: response.contentType, base64: response.base64 }
        : { source: 'lup-emoji-extension', type: 'error', id: data.id, message: response?.message ?? 'Importen kunne ikke læses.' }, location.origin);
    } catch {
      if (data.type === 'request') window.postMessage({ source: 'lup-emoji-extension', type: 'error', id: data.id,
        message: 'Udvidelsen blev genindlæst. Højreklik på billedet igen.' }, location.origin);
    } finally { inFlight = false; }
  });
})();
