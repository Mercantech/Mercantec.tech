export const LARGE_UPLOAD_CONFIRM_BYTES = 100 * 1024 * 1024;

export function formatUploadSize(bytes) {
  const unit = bytes >= 1024 ** 3 ? 'GiB' : bytes >= 1024 ** 2 ? 'MiB' : 'KiB';
  const divisor = unit === 'GiB' ? 1024 ** 3 : unit === 'MiB' ? 1024 ** 2 : 1024;
  return new Intl.NumberFormat('da-DK', { maximumFractionDigits: 1 }).format(bytes / divisor) + ' ' + unit;
}

/** Notebook files use the private page-authorized single-request API. */
export async function uploadNotebookAsset(file, {
  pageId, getToken, signal, onProgress = (_sent, _total) => {}, fetchImpl = globalThis.fetch,
}) {
  if (!Number.isSafeInteger(file.size) || file.size <= 0) throw new Error('Vælg en fil med indhold.');
  if (file.size > LARGE_UPLOAD_CONFIRM_BYTES) throw new Error('Filer i notesbogen må højst være 100 MiB.');
  signal?.throwIfAborted();
  const body = new FormData();
  body.append('file', file, file.name);
  const token = await getToken();
  signal?.throwIfAborted();
  onProgress(0, file.size);
  const response = await fetchImpl('/api/notebooks/pages/' + encodeURIComponent(pageId) + '/assets', {
    method: 'POST', headers: { Authorization: 'Bearer ' + token }, body, signal,
  });
  if (!response.ok) throw uploadError(response.status);
  const asset = await response.json();
  if (typeof asset.id !== 'string' || !asset.id || typeof asset.kind !== 'string') throw new Error('Filen blev ikke korrekt registreret. Prøv igen.');
  onProgress(file.size, file.size);
  return asset;
}

function uploadError(status) {
  return new Error(status === 400 ? 'Filtypen understøttes ikke, eller filen er ugyldig.'
    : status === 401 ? 'Log ind igen for at uploade filen.'
    : status === 403 ? 'Du har ikke længere adgang til at uploade på siden.'
    : status === 404 || status === 409 ? 'Uploaden er ikke længere tilgængelig. Vælg filen igen.'
    : status === 413 ? 'Uploaden kunne ikke gennemføres med serverens uploadindstillinger. Prøv igen.'
    : 'Upload kunne ikke gennemføres. Prøv igen.');
}

const retryDelay = (milliseconds, signal) => new Promise((resolve, reject) => {
  signal?.throwIfAborted();
  const onAbort = () => { clearTimeout(timer); reject(signal.reason); };
  const timer = setTimeout(() => { signal?.removeEventListener('abort', onAbort); resolve(); }, milliseconds);
  signal?.addEventListener('abort', onAbort, { once: true });
});

/** Upload Blob slices, keeping memory and individual proxy requests bounded by the server's part size. */
export async function uploadNotionAsset(file, {
  pageId, getToken, confirmLargeUpload, signal, onProgress = (_sent, _total) => {},
  fetchImpl = globalThis.fetch, delay = retryDelay,
}) {
  if (!Number.isSafeInteger(file.size) || file.size <= 0) throw new Error('Vælg en fil med indhold.');
  signal?.throwIfAborted();
  if (file.size > LARGE_UPLOAD_CONFIRM_BYTES && !await confirmLargeUpload(file)) return null;
  signal?.throwIfAborted();

  const base = '/api/notion/pages/' + encodeURIComponent(pageId) + '/assets/uploads';
  let uploadId;
  let completed = false;
  const request = async (url, options, retry = false) => {
    let failures = 0, busyWait = 0;
    for (;;) {
      signal?.throwIfAborted();
      const token = await getToken();
      signal?.throwIfAborted();
      let response;
      try {
        response = await fetchImpl(url, { ...options, signal, headers: { ...options.headers, Authorization: 'Bearer ' + token } });
      } catch (error) {
        if (signal?.aborted) throw error;
        if (!retry || failures >= 2) throw new Error('Forbindelsen blev afbrudt under uploaden. Vælg filen igen, og prøv igen.', { cause: error });
        await delay(750 * ++failures, signal);
        continue;
      }
      if (response.ok) return response;
      if (retry && response.status === 409) {
        let code;
        try { code = (await response.json())?.error; } catch { /* Other conflicts are terminal. */ }
        if (code === 'upload_busy') {
          // A lost response can leave the original S3 request running on the server.
          const retryAfter = Number(response.headers?.get('Retry-After')) || 2;
          const wait = Math.max(500, Math.min(10000, retryAfter * 1000));
          if (busyWait + wait <= 5 * 60 * 1000) {
            busyWait += wait;
            await delay(wait, signal);
            continue;
          }
        }
      }
      if (!retry || failures >= 2 || !(response.status === 408 || response.status === 429 || response.status >= 500)) throw uploadError(response.status);
      await delay(750 * ++failures, signal);
    }
  };
  try {
    const start = await request(base, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ originalName: file.name, contentType: file.type, sizeBytes: file.size }) });
    const session = await start.json();
    uploadId = session.id;
    const chunkSize = session.chunkSizeBytes;
    if (typeof uploadId !== 'string' || !uploadId || !Number.isSafeInteger(chunkSize) || chunkSize <= 0 || chunkSize > 64 * 1024 * 1024) throw new Error('Serveren returnerede en ugyldig upload. Prøv igen.');
    onProgress(0, file.size);
    for (let offset = 0, part = 1; offset < file.size; offset += chunkSize, part++) {
      const end = Math.min(offset + chunkSize, file.size);
      await request(base + '/' + encodeURIComponent(uploadId) + '/parts/' + part, { method: 'PUT', headers: { 'Content-Type': 'application/octet-stream' }, body: file.slice(offset, end) }, true);
      onProgress(end, file.size);
    }
    const response = await request(base + '/' + encodeURIComponent(uploadId) + '/complete', { method: 'POST' }, true);
    const asset = await response.json();
    if (typeof asset.id !== 'string' || !asset.id || typeof asset.kind !== 'string') throw new Error('Filen blev ikke korrekt registreret. Prøv igen.');
    completed = true;
    return asset;
  } finally {
    if (uploadId && !completed) {
      // Cancellation also needs a request after the upload's signal has been aborted.
      try {
        const token = await getToken();
        await fetchImpl(base + '/' + encodeURIComponent(uploadId), { method: 'DELETE', headers: { Authorization: 'Bearer ' + token }, signal: AbortSignal.timeout(10000) });
      } catch { /* The server also expires abandoned upload sessions. */ }
    }
  }
}
