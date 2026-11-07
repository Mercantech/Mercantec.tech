import { MAX_BYTES, MAX_DOWNLOAD_BYTES, MAX_PIXELS, MIME_TYPES, sourceUrl, originPattern, sanitizeName, validId } from './core.mjs';

const status = document.querySelector('#status');
const name = document.querySelector('#name');
const continueButton = document.querySelector('#continue');
const permissionButton = document.querySelector('#permission');
const fileInput = document.querySelector('#file');
const id = new URL(location.href).searchParams.get('job');
let source = null, prepared = null, previewUrl = null, busy = false, editedName = false, generation = 0;
name.addEventListener('input', () => { editedName = true; });

function message(text, error = false) { status.textContent = text; status.classList.toggle('error', error); }
function setBusy(value) { busy = value; continueButton.disabled = value || !prepared; fileInput.disabled = value; permissionButton.disabled = value; }

async function boundedImageFetch(url) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 20000);
  try {
    const response = await fetch(url, { credentials: 'omit', referrerPolicy: 'no-referrer', signal: controller.signal });
    if (!response.ok) throw new Error(`Billedserveren svarede med ${response.status}. Gem billedet og vælg filen nedenfor.`);
    const declared = Number(response.headers.get('content-length'));
    if (declared > MAX_DOWNLOAD_BYTES) throw new Error('Billedet er større end 8 MB. Vælg et mindre billede.');
    const type = (response.headers.get('content-type') ?? '').split(';')[0].trim().toLowerCase();
    if (type && !type.startsWith('image/') && type !== 'application/octet-stream') throw new Error('Adressen gav ikke et billede. Gem billedet og vælg filen nedenfor.');
    const reader = response.body?.getReader();
    if (!reader) throw new Error('Billedet kunne ikke hentes. Vælg filen nedenfor.');
    let size = 0;
    const chunks = [];
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > MAX_DOWNLOAD_BYTES) { await reader.cancel(); throw new Error('Billedet er større end 8 MB. Vælg et mindre billede.'); }
      chunks.push(value);
    }
    return new Blob(chunks, { type });
  } finally { clearTimeout(timer); }
}

function png(canvas) { return new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('Billedet kunne ikke konverteres.')), 'image/png')); }

async function adaptImage(blob) {
  if (!blob.size || blob.size > MAX_DOWNLOAD_BYTES) throw new Error('Vælg et billede med indhold på højst 8 MB.');
  if (blob.type === 'image/svg+xml') throw new Error('SVG understøttes ikke her. Gem billedet som PNG og vælg filen.');
  let bitmap;
  try { bitmap = await createImageBitmap(blob); } catch { throw new Error('Billedet kunne ikke læses. Vælg PNG, JPEG, GIF, WebP eller AVIF.'); }
  try {
    if (!bitmap.width || !bitmap.height || bitmap.width * bitmap.height > MAX_PIXELS) throw new Error('Billedet må højst være 16 megapixel. Vælg et mindre billede.');
    const signature = new Uint8Array(await blob.slice(0, 16).arrayBuffer());
    const text = String.fromCharCode(...signature);
    const matchesType = blob.type === 'image/png' ? signature[0] === 137 && text.slice(1, 4) === 'PNG'
      : blob.type === 'image/jpeg' ? signature[0] === 255 && signature[1] === 216 && signature[2] === 255
        : blob.type === 'image/gif' ? ['GIF87a', 'GIF89a'].includes(text.slice(0, 6))
          : blob.type === 'image/webp' && text.startsWith('RIFF') && text.slice(8, 12) === 'WEBP';
    if (matchesType && blob.type === 'image/gif' && blob.size <= MAX_BYTES) return { blob, note: 'GIF-filen bevares med sin animation.' };
    if (matchesType && MIME_TYPES.has(blob.type) && blob.size <= MAX_BYTES && bitmap.width <= 256 && bitmap.height <= 256)
      return { blob, note: 'Billedet er klar uden konvertering.' };
    const canvas = document.createElement('canvas');
    for (const limit of [256, 192, 128]) {
      const scale = Math.min(1, limit / bitmap.width, limit / bitmap.height);
      canvas.width = Math.max(1, Math.round(bitmap.width * scale)); canvas.height = Math.max(1, Math.round(bitmap.height * scale));
      canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
      const result = await png(canvas);
      if (result.size <= MAX_BYTES) return { blob: result, note: `Tilpasset til ${canvas.width} × ${canvas.height} pixels og gemt som stillbillede.${blob.type === 'image/gif' ? ' GIF-animationen er ikke bevaret.' : ''}` };
    }
    throw new Error('Billedet kunne ikke gøres mindre end 256 KB. Vælg et andet billede.');
  } finally { bitmap.close(); }
}

async function prepare(blob, fileName) {
  const current = ++generation;
  prepared = null;
  document.querySelector('#preview-wrap').hidden = true;
  document.querySelector('#details').textContent = '';
  setBusy(true); message('Klargør billedet…');
  try {
    const result = await adaptImage(blob);
    if (current !== generation) return;
    prepared = result.blob;
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    previewUrl = URL.createObjectURL(prepared);
    document.querySelector('#preview').src = previewUrl;
    document.querySelector('#preview-wrap').hidden = false;
    document.querySelector('#details').textContent = `${Math.ceil(prepared.size / 1024)} KB · ${result.note}`;
    if (!editedName) name.value = sanitizeName(fileName);
    permissionButton.hidden = true;
    message('Billedet er klar. Fortsæt for at gennemgå det på LUP.dk.');
  } catch (error) { message(error.message || 'Billedet kunne ikke læses.', true); }
  finally { if (current === generation) setBusy(false); }
}

async function fetchSource() {
  setBusy(true); message('Henter billedet…');
  try {
    const pattern = originPattern(source.href);
    // activeTab can grant temporary access after the user's context-menu click.
    const granted = await chrome.permissions.contains({ origins: [pattern] });
    let blob;
    try { blob = await boundedImageFetch(source.href); }
    catch (error) {
      if (!granted && error instanceof TypeError) {
        permissionButton.hidden = false;
        permissionButton.textContent = `Giv adgang til ${source.host}`;
        throw new Error('Chrome har brug for adgang til billedets hjemmeside. Giv adgang nedenfor, eller vælg en lokal fil.');
      }
      throw error;
    }
    let filename = source.pathname.split('/').at(-1) || 'emoji';
    try { filename = decodeURIComponent(filename); } catch { /* A malformed URL filename does not invalidate decoded image pixels. */ }
    await prepare(blob, filename);
  } catch (error) { message(error.name === 'AbortError' ? 'Billedserveren svarede ikke i tide. Vælg en lokal fil.'
    : error instanceof TypeError ? 'Billedet kunne ikke hentes. Gem billedet og vælg filen nedenfor.' : error.message, true); }
  finally { setBusy(false); }
}

permissionButton.addEventListener('click', () => {
  if (!source || busy) return;
  // Request directly in the click gesture; only this origin is requested.
  void chrome.permissions.request({ origins: [originPattern(source.href)] }).then(granted => {
    if (granted) return fetchSource();
    message('Adgang blev ikke givet. Du kan gemme billedet og vælge filen nedenfor.', true);
  }).catch(() => message('Adgang kunne ikke gives. Vælg billedet som lokal fil.', true));
});

fileInput.addEventListener('change', () => { const file = fileInput.files?.[0]; if (file && !busy) void prepare(file, file.name); });

document.querySelector('#form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!prepared || busy) return;
  const shortName = name.value.trim();
  if (!/^[a-z0-9_-]{2,32}$/.test(shortName)) { message('Brug 2–32 små bogstaver, tal, _ eller - i navnet.', true); name.focus(); return; }
  setBusy(true);
  try {
    const bytes = new Uint8Array(await prepared.arrayBuffer());
    let binary = ''; for (const byte of bytes) binary += String.fromCharCode(byte);
    const response = await chrome.runtime.sendMessage({ type: 'prepare:continue', id, name: shortName, contentType: prepared.type, base64: btoa(binary) });
    if (!response?.ok) throw new Error(response?.message || 'LUP.dk kunne ikke åbnes.');
    message('LUP.dk er åbnet i en ny fane. Gennemgå felterne dér, og tryk Upload. Du kan lukke denne fane.');
    continueButton.textContent = 'Åbnet på LUP.dk';
    fileInput.disabled = true;
  } catch (error) { message(error.message, true); setBusy(false); }
});

window.addEventListener('unload', () => { if (previewUrl) URL.revokeObjectURL(previewUrl); });

async function start() {
  if (!validId(id)) { message('Billedkladden mangler. Højreklik på et billede for at begynde.', true); fileInput.disabled = true; return; }
  try {
    let response;
    for (let attempt = 0; attempt < 8; attempt++) {
      response = await chrome.runtime.sendMessage({ type: 'prepare:get', id });
      if (!response?.retry) break;
      await new Promise(resolve => setTimeout(resolve, 250));
    }
    if (!response?.ok) throw new Error(response?.message || 'Billedkladden kunne ikke læses.');
    source = sourceUrl(response.srcUrl);
    if (!source) { message('Dette billede kan ikke hentes direkte. Gem billedet på computeren, og vælg filen nedenfor.', true); return; }
    await fetchSource();
  } catch (error) { message(error.message || 'Billedet kunne ikke hentes. Vælg filen nedenfor.', true); }
}
void start();
