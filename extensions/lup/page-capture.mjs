/**
 * Runs in the isolated world of the frame where the user clicked an image.
 * Keep every helper inside this function: scripting.executeScript serializes it.
 * No extension state, credentials, page storage or unrelated image bytes are read.
 */
export async function captureSelectedImage({ srcUrl, capturePixels = false } = {}) {
  const MAX_BYTES = 8 * 1024 * 1024;
  const MAX_PIXELS = 16 * 1024 * 1024;
  const MAX_DATA_URL = Math.ceil(MAX_BYTES / 3) * 4 + 256;
  const deadline = Date.now() + 6000;
  const candidates = [];
  const code = number => `LUP-IMP-${String(number).padStart(3, '0')}`;
  const problem = (number, status) => Object.assign(new Error('Image capture failed'), { captureCode: code(number), status });
  const issue = error => ({ code: error?.captureCode || code(error?.name === 'AbortError' || error?.name === 'TimeoutError' ? 109 : 108),
    ...(Number.isInteger(error?.status) ? { httpStatus: error.status, context: { status: error.status } } : {}) });
  function imageUrl(value) {
    if (typeof value !== 'string' || !value || value.length > MAX_DATA_URL) return null;
    try {
      const url = new URL(value, document.baseURI);
      if (['http:', 'https:'].includes(url.protocol))
        return url.href.length <= 8192 && !url.username && !url.password ? url : null;
      if (url.protocol === 'blob:') return url.href.length <= 8192 && url.origin === location.origin ? url : null;
      if (url.protocol === 'data:' && /^data:image\/(?:png|jpeg|gif|webp|avif)(?:;[^,]*)?,/iu.test(url.href)) return url;
    } catch { }
    return null;
  }
  function addCandidate(value) {
    if (candidates.length >= 6) return;
    const url = imageUrl(value);
    if (url && ['http:', 'https:'].includes(url.protocol) && !candidates.includes(url.href)) candidates.push(url.href);
  }
  function srcsetUrls(value) {
    // The browser-resolved currentSrc is preferred. Parse alternatives only on
    // the selected image/picture, retaining commas inside URL query strings.
    if (typeof value !== 'string' || value.length > 16_384) return;
    let position = 0, count = 0;
    while (position < value.length && count++ < 12 && candidates.length < 6) {
      while (position < value.length && /[\s,]/u.test(value[position])) position++;
      const start = position;
      while (position < value.length && !/\s/u.test(value[position])) position++;
      let url = value.slice(start, position);
      if (!url) break;
      if (url.endsWith(',')) { addCandidate(url.replace(/,+$/u, '')); continue; }
      let parentheses = 0;
      while (position < value.length) {
        const character = value[position++];
        if (character === '(') parentheses++;
        if (character === ')') parentheses = Math.max(0, parentheses - 1);
        if (character === ',' && !parentheses) break;
      }
      addCandidate(url);
    }
  }
  function format(bytes) {
    if (bytes.length >= 8 && [137, 80, 78, 71, 13, 10, 26, 10].every((byte, index) => bytes[index] === byte)) return 'image/png';
    if (bytes.length >= 3 && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255) return 'image/jpeg';
    const ascii = (start, end) => String.fromCharCode(...bytes.subarray(start, end));
    if (bytes.length >= 6 && ['GIF87a', 'GIF89a'].includes(ascii(0, 6))) return 'image/gif';
    if (bytes.length >= 12 && ascii(0, 4) === 'RIFF' && ascii(8, 12) === 'WEBP') return 'image/webp';
    if (bytes.length >= 16 && ascii(4, 8) === 'ftyp' && /avif|avis/u.test(ascii(8, Math.min(bytes.length, 64)))) return 'image/avif';
    return null;
  }
  async function timed(promise, milliseconds) {
    let timer;
    try { return await Promise.race([promise, new Promise((_, reject) => { timer = setTimeout(() => reject(problem(109)), Math.max(1, milliseconds)); })]); }
    finally { clearTimeout(timer); }
  }
  async function validate(bytes) {
    if (!bytes.length) throw problem(111);
    if (bytes.length > MAX_BYTES) throw problem(110);
    const contentType = format(bytes);
    if (!contentType) {
      const prefix = String.fromCharCode(...bytes.subarray(0, 512)).trimStart();
      throw problem(/^(?:<\?xml[^>]*>\s*)?<svg(?:\s|\/?>)/iu.test(prefix) ? 113 : 111);
    }
    const pending = createImageBitmap(new Blob([bytes], { type: contentType }));
    let bitmap;
    try {
      bitmap = await timed(pending, Math.min(1500, deadline - Date.now()));
      if (!bitmap.width || !bitmap.height) throw problem(112);
      if (bitmap.width * bitmap.height > MAX_PIXELS) throw problem(114);
    } catch (error) {
      // If decoding finishes after a timeout, release its backing pixels too.
      if (!bitmap) void pending.then(result => result.close()).catch(() => {});
      throw error?.captureCode ? error : problem(112);
    } finally { bitmap?.close(); }
    let binary = '';
    for (let offset = 0; offset < bytes.length; offset += 16_384)
      binary += String.fromCharCode(...bytes.subarray(offset, offset + 16_384));
    return { base64: btoa(binary), contentType };
  }
  async function readSelected(url) {
    const remaining = deadline - Date.now();
    if (remaining <= 0) throw problem(109);
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), Math.min(1800, remaining));
    let reader;
    try {
      const response = await fetch(url.href, {
        // Inherit the document's referrer policy, including an explicit
        // no-referrer policy. Same-origin cookies remain browser-managed.
        credentials: 'same-origin',
        redirect: 'follow', cache: 'default', signal: controller.signal,
      });
      if (!response.ok) throw problem(response.status === 401 ? 103 : response.status === 403 ? 104
        : [404, 410].includes(response.status) ? 105 : response.status === 429 ? 106 : 107, response.status);
      if (response.type === 'opaque') throw problem(108);
      if ((response.headers.get('content-type') || '').split(';')[0].trim().toLowerCase() === 'image/svg+xml') throw problem(113);
      if (Number(response.headers.get('content-length')) > MAX_BYTES) throw problem(110);
      reader = response.body?.getReader();
      if (!reader) throw problem(111);
      const chunks = []; let size = 0;
      while (true) {
        const { value, done } = await timed(reader.read(), Math.min(1800, deadline - Date.now()));
        if (done) break;
        size += value.byteLength;
        if (size > MAX_BYTES) throw problem(110);
        chunks.push(value);
      }
      const bytes = new Uint8Array(size); let offset = 0;
      for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
      return await validate(bytes);
    } finally {
      clearTimeout(timer);
      if (reader) { void reader.cancel().catch(() => {}); try { reader.releaseLock(); } catch { } }
    }
  }

  const selected = imageUrl(srcUrl);
  if (!selected) return { ok: false, candidates, canvasAvailable: false,
    issue: { code: code(typeof srcUrl === 'string' && /^data:image\/svg\+xml/iu.test(srcUrl) ? 113 : 2) } };
  addCandidate(selected.href);
  const matches = Array.from(document.images).slice(0, 4096).filter(image => [image.currentSrc, image.src]
    .some(value => imageUrl(value)?.href === selected.href));
  const target = matches.length === 1 ? matches[0] : null;
  const altText = target && typeof target.alt === 'string' ? target.alt.replace(/[\u0000-\u001f\u007f]/gu, ' ').trim().slice(0, 240) : '';
  const svg = /\.svg(?:[?#]|$)/iu.test(selected.pathname);
  const canvasAvailable = Boolean(target && !svg && target.complete && target.naturalWidth > 0 && target.naturalHeight > 0
    && target.naturalWidth * target.naturalHeight <= MAX_PIXELS);
  if (target) {
    addCandidate(target.currentSrc); addCandidate(target.src);
    srcsetUrls(target.getAttribute('srcset'));
    const picture = target.closest('picture');
    if (picture) for (const source of Array.from(picture.querySelectorAll('source[srcset]')).slice(0, 4)) {
      const media = source.getAttribute('media');
      if (!media || matchMedia(media).matches) srcsetUrls(source.getAttribute('srcset'));
    }
  }
  let lastIssue = { code: code(108) };
  const attempts = [selected.href, ...(target ? [target.currentSrc, target.src] : [])]
    .map(imageUrl).filter((value, index, all) => value && all.findIndex(other => other?.href === value.href) === index).slice(0, 3);
  for (const url of attempts) {
    try {
      const image = await readSelected(url);
      return { ok: true, ...image, method: ['blob:', 'data:'].includes(url.protocol) ? 'embedded' : 'page-fetch',
        candidates, altText, canvasAvailable, animationLost: false };
    } catch (error) {
      const current = issue(error);
      // A blocked alternative must not hide a useful status from the exact URL.
      if ([code(108), code(109)].includes(lastIssue.code)) lastIssue = current;
    }
  }
  if (capturePixels === true && canvasAvailable && Date.now() < deadline
      && [target.currentSrc, target.src].some(value => imageUrl(value)?.href === selected.href)) {
    try {
      const scale = Math.min(1, 1024 / target.naturalWidth, 1024 / target.naturalHeight);
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(target.naturalWidth * scale));
      canvas.height = Math.max(1, Math.round(target.naturalHeight * scale));
      canvas.getContext('2d').drawImage(target, 0, 0, canvas.width, canvas.height);
      const blob = await timed(new Promise(resolve => canvas.toBlob(resolve, 'image/png')), deadline - Date.now());
      if (!blob) throw problem(112);
      const image = await validate(new Uint8Array(await blob.arrayBuffer()));
      return { ok: true, ...image, method: 'canvas', candidates, altText, canvasAvailable: true, animationLost: true };
    } catch (error) { lastIssue = issue(error?.name === 'SecurityError' ? problem(119) : error); }
  }
  return { ok: false, candidates, altText, canvasAvailable, issue: lastIssue };
}
