const jobs = new Map();
const MAX_BYTES = 256 * 1024;
const MAX_INPUT = 8 * 1024 * 1024;

/** Local, bounded GIF processing. Never replaces an animation with a still image. */
export function processGif(blob, options = {}) {
  let jobId;
  try {
    if (!(blob instanceof Blob) || !blob.size || blob.size > MAX_INPUT)
      throw new Error('Vælg en GIF-fil med indhold på højst 8 MB.');
    if (!options || typeof options !== 'object') throw new Error('GIF-indstillingerne er ugyldige.');
    jobId = options.jobId ?? crypto.randomUUID();
    if (typeof jobId !== 'string' || !jobId.length || jobId.length > 128 || jobs.has(jobId))
      throw new Error('GIF-behandlingen har et ugyldigt eller allerede aktivt id.');
    if (jobs.size >= 2) throw new Error('Der behandles allerede GIF-billeder. Vent et øjeblik, og prøv igen.');
    const maxBytes = options.maxBytes ?? MAX_BYTES, maxDimension = options.maxDimension ?? 256;
    if (!Number.isInteger(maxBytes) || maxBytes < 1 || maxBytes > MAX_BYTES
      || !Number.isInteger(maxDimension) || maxDimension < 1 || maxDimension > 256)
      throw new Error('GIF-indstillingerne er ugyldige.');
  } catch (error) { return Promise.reject(error); }

  return new Promise((resolve, reject) => {
    let worker;
    try { worker = new Worker(new URL('./gif-worker.mjs', import.meta.url), { type: 'module' }); }
    catch { reject(new Error('GIF-behandlingen kunne ikke startes. Genindlæs udvidelsen, og prøv igen.')); return; }
    const finish = (error, result) => {
      if (jobs.get(jobId)?.worker !== worker) return;
      clearTimeout(jobs.get(jobId).timeout); jobs.delete(jobId); worker.terminate();
      error ? reject(error) : resolve(result);
    };
    const timeout = setTimeout(() => finish(new Error('GIF-behandlingen tog for lang tid. Vælg en kortere GIF. Originalen er bevaret.')), 30_000);
    jobs.set(jobId, { worker, timeout, finish });
    worker.addEventListener('message', event => {
      if (event.data?.jobId !== jobId) return;
      if (typeof event.data.error === 'string') { finish(new Error(event.data.error)); return; }
      const bytes = event.data.bytes;
      if (!(bytes instanceof ArrayBuffer) || !bytes.byteLength || bytes.byteLength > (options.maxBytes ?? MAX_BYTES)) {
        finish(new Error('GIF-behandlingen gav et ugyldigt svar. Originalen er bevaret.')); return;
      }
      finish(null, new Blob([bytes], { type: 'image/gif' }));
    });
    worker.addEventListener('error', event => {
      event.preventDefault(); finish(new Error('GIF-behandlingen kunne ikke gennemføres. Originalen er bevaret.'));
    });
    blob.arrayBuffer().then(bytes => {
      if (jobs.get(jobId)?.worker !== worker) return;
      worker.postMessage({ jobId, bytes, crop: options.crop ?? null, maxBytes: options.maxBytes ?? MAX_BYTES,
        maxDimension: options.maxDimension ?? 256 }, [bytes]);
    }).catch(() => finish(new Error('GIF-filen kunne ikke læses. Originalen er bevaret.')));
  });
}

export function cancelGif(jobId) {
  jobs.get(jobId)?.finish(new DOMException('GIF-behandlingen er stoppet. Originalen er bevaret.', 'AbortError'));
}

export function cancelAllGifs() { for (const id of [...jobs.keys()]) cancelGif(id); }
