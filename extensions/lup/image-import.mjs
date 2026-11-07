import { MAX_DOWNLOAD_BYTES, sourceUrl } from './core.mjs';
import { ImportFailure } from './import-errors.mjs';

export function sniffImageType(value) {
  const bytes = value instanceof Uint8Array ? value : new Uint8Array(value);
  const ascii = (start, length) => String.fromCharCode(...bytes.subarray(start, start + length));
  if (bytes.length >= 8 && bytes[0] === 137 && ascii(1, 3) === 'PNG' && bytes[4] === 13 && bytes[5] === 10 && bytes[6] === 26 && bytes[7] === 10) return 'image/png';
  if (bytes.length >= 3 && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255) return 'image/jpeg';
  if (['GIF87a', 'GIF89a'].includes(ascii(0, 6))) return 'image/gif';
  if (ascii(0, 4) === 'RIFF' && ascii(8, 4) === 'WEBP') return 'image/webp';
  if (ascii(4, 4) === 'ftyp' && /avif|avis/.test(ascii(8, 56))) return 'image/avif';
  if (ascii(0, 2) === 'BM') return 'image/bmp';
  return null;
}

export async function fetchImportImage(value, { timeoutMs = 20_000, fetcher = fetch } = {}) {
  const url = sourceUrl(value);
  if (!url) throw new ImportFailure('LUP-IMP-008');
  const context = { host: url.hostname, method: 'direct' }, controller = new AbortController();
  let timer, reader, stage = 'headers';
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => {
      reject(new ImportFailure('LUP-IMP-109', context));
      controller.abort(); void reader?.cancel().catch(() => {});
    }, timeoutMs);
  });
  async function download() {
    const response = await fetcher(url.href, { credentials: 'omit', referrerPolicy: 'no-referrer', signal: controller.signal, cache: 'no-store' });
    context.status = response.status;
    if (!response.ok) {
      const code = ({ 401: '103', 403: '104', 404: '105', 410: '105', 429: '106' })[response.status] || '107';
      throw new ImportFailure(`LUP-IMP-${code}`, context);
    }
    const declared = Number(response.headers.get('content-length'));
    if (declared > MAX_DOWNLOAD_BYTES) throw new ImportFailure('LUP-IMP-110', { ...context, bytes: declared });
    if ((response.headers.get('content-type') || '').split(';')[0].trim().toLowerCase() === 'image/svg+xml') throw new ImportFailure('LUP-IMP-113', context);
    reader = response.body?.getReader(); stage = 'body';
    if (!reader) throw new ImportFailure('LUP-IMP-112', context);
    let size = 0; const chunks = [];
    while (true) {
      const { value, done } = await reader.read(); if (done) break;
      size += value.byteLength;
      if (size > MAX_DOWNLOAD_BYTES) { await reader.cancel(); throw new ImportFailure('LUP-IMP-110', { ...context, bytes: size }); }
      chunks.push(value);
    }
    if (!size) throw new ImportFailure('LUP-IMP-112', context);
    const encoding = response.headers.get('content-encoding');
    if (declared > 0 && (!encoding || encoding === 'identity') && size !== declared) throw new ImportFailure('LUP-IMP-116', context);
    const blob = new Blob(chunks), head = new Uint8Array(await blob.slice(0, 512).arrayBuffer());
    const type = sniffImageType(head);
    if (!type) {
      const text = new TextDecoder().decode(head);
      throw new ImportFailure(/<svg\b/i.test(text) ? 'LUP-IMP-113' : 'LUP-IMP-111', context);
    }
    return new Blob(chunks, { type });
  }
  try { return await Promise.race([download(), timeout]); }
  catch (error) {
    if (error instanceof ImportFailure) throw error;
    throw new ImportFailure(stage === 'body' ? 'LUP-IMP-116' : 'LUP-IMP-108', context);
  } finally { clearTimeout(timer); try { reader?.releaseLock(); } catch { } }
}
