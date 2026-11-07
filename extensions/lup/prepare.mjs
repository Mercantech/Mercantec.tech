import { MAX_BYTES, MAX_DOWNLOAD_BYTES, MAX_PIXELS, MIME_TYPES, sourceUrl, originPattern, sanitizeName, validId } from './core.mjs';
import { createCropEditor } from './crop.mjs';

const status = document.querySelector('#status');
const name = document.querySelector('#name');
const altText = document.querySelector('#alt-text');
const connection = document.querySelector('#connection');
const continueButton = document.querySelector('#continue');
const permissionButton = document.querySelector('#permission');
const fileInput = document.querySelector('#file');
const backgroundButton = document.querySelector('#remove-background');
const stopBackgroundButton = document.querySelector('#stop-background');
const originalButton = document.querySelector('#restore-original');
const id = new URL(location.href).searchParams.get('job');
let source = null, prepared = null, cropEditor = null, busy = false, editedName = false, generation = 0, connected = false, saved = false, editedAlt = false;
let originalSource = null, originalPrepared = null, originalFileName = '', originalNote = '';
let backgroundEdited = false, cropActive = false, backgroundJob = null, backgroundSerial = 0, backgroundModule = null;
name.addEventListener('input', () => { editedName = true; });
name.addEventListener('input', () => { if (!editedAlt) altText.value = name.value.replaceAll('_', ' '); });
altText.addEventListener('input', () => { editedAlt = true; });

function message(text, error = false) { status.textContent = text; status.classList.toggle('error', error); }
function refreshImageActions() {
  backgroundButton.disabled = busy || saved || !prepared || backgroundEdited;
  stopBackgroundButton.hidden = backgroundJob === null;
  stopBackgroundButton.disabled = saved;
  originalButton.hidden = !originalSource || !(backgroundEdited || cropActive);
  originalButton.disabled = busy || saved;
}
function setBusy(value) { busy = value; continueButton.disabled = value || !prepared || !connected || saved; fileInput.disabled = value || saved; permissionButton.disabled = value || saved; cropEditor?.setDisabled(value || saved); refreshImageActions(); }
async function updateConnection() {
  if (!validId(id) || saved) return;
  try {
    const state = await chrome.runtime.sendMessage({ type: 'prepare:status', id });
    connected = state?.connected === true;
    connection.textContent = connected ? `Forbundet${state.email ? ' som ' + state.email : ''}.`
      : state?.message || 'Ikke forbundet. Åbn indstillingerne og tilføj din uploadnøgle.';
  } catch { connected = false; connection.textContent = 'Forbindelsen kunne ikke kontrolleres. Prøv igen senere.'; }
  setBusy(busy);
}
window.addEventListener('focus', () => { if (!busy) void updateConnection(); });
chrome.storage.onChanged.addListener((changes, area) => { if (area === 'local' && !saved) void updateConnection(); });

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
  cropEditor?.destroy(); cropEditor = null;
  document.querySelector('#preview-wrap').hidden = true;
  document.querySelector('#details').textContent = '';
  setBusy(true); message('Klargør billedet…');
  try {
    const result = await adaptImage(blob);
    if (current !== generation) return;
    originalSource = blob; originalPrepared = result.blob; originalFileName = fileName; originalNote = result.note;
    backgroundEdited = false;
    await installImage(blob, result, fileName, current);
    message('Billedet er klar. Vælg udsnit og zoom, og gem emojien herfra.');
  } catch (error) { message(error.message || 'Billedet kunne ikke læses.', true); }
  finally { if (current === generation) setBusy(false); }
}

async function installImage(blob, result, fileName, current) {
  const previous = cropEditor;
  const editor = await createCropEditor(document.querySelector('#crop-editor'), blob, state => {
    if (current !== generation) return;
    cropActive = state.enabled;
    document.querySelector('#details').textContent = state.enabled
      ? `Kvadratisk udsnit · ${Math.round(state.zoom * 100)} % zoom · gemmes som PNG-stillbillede.`
      : `${Math.ceil(result.blob.size / 1024)} KB · ${result.note}`;
    refreshImageActions();
  }, { wholeBlob: result.blob, previewId: 'preview' });
  if (current !== generation) { editor.destroy(); return false; }
  previous?.destroy();
  cropEditor = editor;
  prepared = result.blob;
  cropActive = false;
  document.querySelector('#preview-wrap').hidden = false;
  if (!editedName) name.value = sanitizeName(fileName);
  if (!editedAlt) altText.value = name.value.replaceAll('_', ' ');
  permissionButton.hidden = true;
  refreshImageActions();
  return true;
}

async function imageDataUrl(blob) {
  let binary = '';
  for (const byte of new Uint8Array(await blob.arrayBuffer())) binary += String.fromCharCode(byte);
  return `data:${blob.type};base64,${btoa(binary)}`;
}

function processedPng(result) {
  if (result?.contentType !== 'image/png' || typeof result.base64 !== 'string'
    || !result.base64.length || result.base64.length > Math.ceil(MAX_BYTES / 3) * 4)
    throw new Error('Baggrundsfjernelsen gav ikke et gyldigt PNG-billede. Det valgte billede er bevaret.');
  const raw = atob(result.base64);
  const bytes = Uint8Array.from(raw, character => character.charCodeAt(0));
  if (!bytes.length || bytes.length > MAX_BYTES || ![137,80,78,71,13,10,26,10].every((value,index) => bytes[index] === value))
    throw new Error('Baggrundsfjernelsen gav ikke et gyldigt PNG-billede. Det valgte billede er bevaret.');
  return new Blob([bytes], { type: 'image/png' });
}

backgroundButton.addEventListener('click', async () => {
  if (busy || saved || !prepared || !cropEditor || backgroundEdited) return;
  const serial = ++backgroundSerial, current = generation;
  const job = crypto.randomUUID(); backgroundJob = job;
  setBusy(true); message('Fjerner baggrunden lokalt… Første gang kan tage lidt længere tid.');
  try {
    const image = await cropEditor.getBlob();
    const data = await imageDataUrl(image);
    if (serial !== backgroundSerial || current !== generation) return;
    const module = await (backgroundModule ??= import('./emoji-background.js'));
    if (serial !== backgroundSerial || current !== generation) return;
    const result = await module.removeBackground(data, job);
    if (serial !== backgroundSerial || current !== generation) return;
    const blob = processedPng(result);
    await installImage(blob, { blob, note: 'Baggrunden er fjernet. PNG-stillbilledet kan stadig beskæres.' }, originalFileName, current);
    if (serial !== backgroundSerial || current !== generation) return;
    backgroundEdited = true;
    message('Baggrunden er fjernet. Gennemgå resultatet, eller vælg Brug original.');
  } catch (error) {
    if (serial === backgroundSerial && current === generation) {
      if (error instanceof TypeError) backgroundModule = null;
      message(error instanceof Error && error.name !== 'AbortError'
        ? error.message : 'Baggrunden kunne ikke fjernes. Det valgte billede er bevaret.', true);
    }
  } finally {
    if (serial === backgroundSerial && current === generation) { backgroundJob = null; setBusy(false); }
  }
});

stopBackgroundButton.addEventListener('click', () => {
  if (backgroundJob === null || saved) return;
  const job = backgroundJob;
  backgroundJob = null; backgroundSerial++;
  void backgroundModule?.then(module => module.cancel(job)).catch(() => {});
  setBusy(false); message('Baggrundsfjernelsen er stoppet. Det valgte billede er bevaret.');
});

originalButton.addEventListener('click', async () => {
  if (busy || saved || !originalSource || !originalPrepared) return;
  const current = ++generation;
  setBusy(true);
  try {
    await installImage(originalSource, { blob: originalPrepared, note: originalNote }, originalFileName, current);
    backgroundEdited = false;
    message('Originalbilledet er valgt igen. Navn og beskrivelse er bevaret.');
  } catch { message('Originalbilledet kunne ikke åbnes. Vælg filen igen.', true); }
  finally { if (current === generation) setBusy(false); }
});

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
  if (!prepared || busy || saved) return;
  if (!connected) { message('Forbind LUP i indstillingerne først.', true); return; }
  const shortName = name.value.trim();
  if (!/^[a-z0-9_-]{2,32}$/.test(shortName)) { message('Brug 2–32 små bogstaver, tal, _ eller - i navnet.', true); name.focus(); return; }
  if (!altText.value.trim() || altText.value.length > 240) { message('Skriv en kort beskrivelse af billedet.', true); altText.focus(); return; }
  setBusy(true);
  try {
    const image = cropEditor ? await cropEditor.getBlob() : prepared;
    const bytes = new Uint8Array(await image.arrayBuffer());
    let binary = ''; for (const byte of bytes) binary += String.fromCharCode(byte);
    const response = await chrome.runtime.sendMessage({ type: 'prepare:upload', id, name: shortName, altText: altText.value.trim(), contentType: image.type, base64: btoa(binary) });
    if (!response?.ok) throw new Error(response?.message || 'Emojien kunne ikke gemmes.');
    saved = true;
    message(`:${response.name}: er gemt i LUPs emojibibliotek. Du kan lukke denne fane.`);
    continueButton.textContent = 'Emojien er gemt';
    name.disabled = altText.disabled = true;
    setBusy(false);
  } catch (error) { message(error.message, true); setBusy(false); }
});

window.addEventListener('unload', () => {
  backgroundSerial++;
  const job = backgroundJob; backgroundJob = null;
  if (job) void backgroundModule?.then(module => module.cancel(job)).catch(() => {});
  cropEditor?.destroy();
});

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
    await updateConnection();
    source = sourceUrl(response.srcUrl);
    if (!source) { message('Dette billede kan ikke hentes direkte. Gem billedet på computeren, og vælg filen nedenfor.', true); return; }
    await fetchSource();
  } catch (error) { message(error.message || 'Billedet kunne ikke hentes. Vælg filen nedenfor.', true); }
}
void start();
