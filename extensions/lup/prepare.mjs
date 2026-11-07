import { MAX_BYTES, MAX_DOWNLOAD_BYTES, MAX_PIXELS, MIME_TYPES, sourceUrl, originPattern, sanitizeName, validId } from './core.mjs';
import { createCropEditor } from './crop.mjs';
import { normalizeSearchImage } from './image-search.mjs';
import { normalizeGifSearchResult } from './gif-search.mjs';
import { ImportFailure, importIssue, importDiagnostic } from './import-errors.mjs';
import { fetchImportImage } from './image-import.mjs';

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
let uploading = false, previewRevision = 0, previewTimer = null, previewUrl = null;
let searchSource = null, sourceMetadata = null, searchTabId = null;
let gifModule;
let sourceInfo = {}, lastImportFailure = null, permissionUrl = null, importRunning = false, importExpired = false;
const getGifModule = () => gifModule ??= import('./gif-tools.mjs');
async function encodeGif(blob, crop = null) { return (await getGifModule()).processGif(blob, { crop }); }
name.addEventListener('input', () => { editedName = true; });
name.addEventListener('input', () => { if (!editedAlt) altText.value = name.value.replaceAll('_', ' '); updateMetadataPreview(); });
altText.addEventListener('input', () => { editedAlt = true; updateMetadataPreview(); });

function message(text, error = false, kind = 'info') {
  status.textContent = text; status.classList.toggle('error', error);
  status.dataset.kind = error ? 'error' : kind;
  status.setAttribute('role', error ? 'alert' : 'status');
  status.setAttribute('aria-live', error ? 'assertive' : 'polite');
  if (error && !prepared) {
    document.querySelector('#empty-heading').textContent = 'Vælg dit billede';
    document.querySelector('#empty-description').textContent = 'Brug Skift billede, eller træk en billedfil ind her.';
  }
}
function clearImportFailure() {
  lastImportFailure = null; permissionUrl = null;
  document.querySelector('#import-problem').hidden = true;
  document.querySelector('#diagnostic-feedback').textContent = '';
}
function refreshRecoveryActions() {
  const canCapture = Number.isInteger(sourceInfo.sourceTabId) && sourceInfo.sourceTabId > 0;
  document.querySelector('#capture-visible').hidden = !canCapture || importExpired;
  document.querySelector('#capture-visible-hint').hidden = !canCapture || importExpired;
  permissionButton.hidden = !permissionUrl;
  document.querySelector('#retry-import').hidden = lastImportFailure?.code === 'LUP-IMP-001';
  for (const id of ['retry-import', 'capture-visible', 'select-import-file']) document.querySelector('#' + id).disabled = busy || saved || importExpired;
}
function showImportFailure(error, fallback = 'LUP-IMP-112') {
  lastImportFailure = error instanceof ImportFailure ? error : new ImportFailure(error?.code || fallback, error?.context);
  const issue = importIssue(lastImportFailure);
  if (issue.code === 'LUP-IMP-001') { importExpired = true; fileInput.disabled = true; continueButton.disabled = true; }
  document.querySelector('#import-error-title').textContent = issue.title;
  document.querySelector('#import-error-code').textContent = issue.code;
  document.querySelector('#import-error-message').textContent = issue.message;
  document.querySelector('#import-error-hint').textContent = issue.hint;
  document.querySelector('#import-problem').hidden = false;
  document.querySelector('#diagnostic-feedback').textContent = '';
  message(`${issue.title} · ${issue.code}`, true);
  refreshRecoveryActions();
}
document.querySelector('#copy-import-error').addEventListener('click', async () => {
  try {
    await navigator.clipboard.writeText(importDiagnostic(lastImportFailure, chrome.runtime.getManifest().version));
    document.querySelector('#diagnostic-feedback').textContent = 'Fejlinfo er kopieret uden billedadresse eller nøgler.';
  } catch { document.querySelector('#diagnostic-feedback').textContent = 'Kunne ikke kopiere. Oplys fejlkoden ovenfor.'; }
});
document.querySelector('#select-import-file').addEventListener('click', () => { if (!busy && !saved) fileInput.click(); });
function updateMetadataPreview() {
  document.querySelector('#emoji-token').textContent = `:${name.value.trim() || 'din_emoji'}:`;
  document.querySelector('#description-count').textContent = `${altText.value.length}/240`;
}
function clearPreview() {
  previewRevision++; clearTimeout(previewTimer);
  if (previewUrl) URL.revokeObjectURL(previewUrl);
  previewUrl = null;
  const image = document.querySelector('#emoji-preview');
  image.hidden = true; image.removeAttribute('src');
  document.querySelector('#emoji-placeholder').hidden = false;
}
function requestPreview() {
  const revision = ++previewRevision; clearTimeout(previewTimer);
  previewTimer = setTimeout(async () => {
    const editor = cropEditor;
    if (!editor) return;
    try {
      const blob = await editor.getPreviewBlob();
      if (revision !== previewRevision || editor !== cropEditor) return;
      showPreviewBlob(blob);
    } catch { /* A preview never changes the file, metadata, or upload state. */ }
  }, 140);
}
function showPreviewBlob(blob) {
  const url = URL.createObjectURL(blob), previous = previewUrl;
  previewUrl = url;
  const image = document.querySelector('#emoji-preview');
  image.src = url; image.hidden = false;
  document.querySelector('#emoji-placeholder').hidden = true;
  if (previous) URL.revokeObjectURL(previous);
}
function compactCropControls() {
  const host = document.querySelector('#crop-editor'), controls = host.querySelector('[data-controls]');
  const zoom = host.querySelector('[data-zoom]').closest('label');
  const reset = host.querySelector('[data-reset]');
  const row = document.createElement('div'); row.className = 'lup-crop-zoom-row';
  row.append(zoom, reset);
  const precision = document.createElement('details'); precision.className = 'lup-crop-precision';
  const summary = document.createElement('summary'); summary.textContent = 'Finjustér placering';
  precision.append(summary, host.querySelector('[data-x]').closest('label'), host.querySelector('[data-y]').closest('label'));
  // Reparent the existing controls so their pointer, keyboard and input listeners survive.
  const hints = [...controls.querySelectorAll('.lup-crop-hint')];
  hints.forEach(hint => hint.remove());
  const hint = document.createElement('p'); hint.className = 'muted lup-crop-hint';
  hint.textContent = prepared?.type === 'image/gif' ? 'Træk for at beskære. Feltet viser ét billede fra animationen; GIF’en gemmes med bevægelse.' : 'Træk i billedet for at finde det helt rigtige udsnit.';
  controls.append(hint, row, precision);
}
function refreshImageActions() {
  backgroundButton.disabled = busy || saved || !prepared || backgroundEdited;
  stopBackgroundButton.hidden = backgroundJob === null;
  stopBackgroundButton.disabled = saved;
  originalButton.hidden = !originalSource || !(backgroundEdited || cropActive);
  originalButton.disabled = busy || saved;
  document.querySelector('#background-label').textContent = prepared?.type === 'image/gif' ? 'Fjern baggrund · stillbillede' : 'Fjern baggrund';
}
function setBusy(value) {
  busy = value; document.body.dataset.busy = String(value); document.body.dataset.saved = String(saved);
  continueButton.disabled = value || !prepared || !connected || saved || importExpired;
  continueButton.setAttribute('aria-busy', String(uploading));
  document.querySelector('#save-label').textContent = saved ? 'Emojien er gemt' : uploading ? 'Gemmer emoji…' : 'Gem emoji på LUP';
  name.disabled = altText.disabled = uploading || saved;
  fileInput.disabled = value || saved || importExpired; permissionButton.disabled = value || saved || importExpired;
  cropEditor?.setDisabled(value || saved); refreshImageActions();
  refreshRecoveryActions();
}
async function updateConnection() {
  if (!validId(id) || saved) return;
  try {
    const state = await chrome.runtime.sendMessage({ type: 'prepare:status', id });
    connected = state?.connected === true;
    connection.textContent = connected ? 'Forbundet til LUP'
      : state?.message || 'Ikke forbundet. Åbn indstillingerne og tilføj din uploadnøgle.';
    connection.title = connected ? state.email || 'Forbundet' : connection.textContent;
    document.querySelector('#upload-account').textContent = connected && state.email ? `Gemmes som ${state.email}` : 'Forbind LUP én gang med din personlige nøgle.';
  } catch { connected = false; connection.textContent = 'Forbindelsen kunne ikke kontrolleres. Prøv igen senere.'; }
  document.querySelector('#connection-wrap').dataset.state = connected ? 'connected' : 'disconnected';
  if (!connected) {
    connection.title = connection.textContent;
    document.querySelector('#upload-account').textContent = 'Forbind LUP én gang med din personlige nøgle.';
    connection.textContent = 'Ikke forbundet';
  }
  document.querySelector('#connect-prompt').hidden = connected;
  setBusy(busy);
}
window.addEventListener('focus', () => { if (!busy) void updateConnection(); });
chrome.storage.onChanged.addListener((changes, area) => { if (area === 'local' && !saved) void updateConnection(); });

const boundedImageFetch = (url, options) => fetchImportImage(url, options);

function png(canvas) { return new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('Billedet kunne ikke konverteres.')), 'image/png')); }

async function adaptImage(blob) {
  if (!blob.size) throw new ImportFailure('LUP-IMP-112');
  if (blob.size > MAX_DOWNLOAD_BYTES) throw new ImportFailure('LUP-IMP-110', { bytes: blob.size });
  if (blob.type === 'image/svg+xml') throw new ImportFailure('LUP-IMP-113');
  const header = String.fromCharCode(...new Uint8Array(await blob.slice(0, 6).arrayBuffer()));
  if (['GIF87a', 'GIF89a'].includes(header)) {
    message('Tilpasser GIF’en og bevarer animationen…', false, 'progress');
    let result;
    try { result = await encodeGif(blob); } catch { throw new ImportFailure('LUP-IMP-117'); }
    return { blob: result, note: `GIF-animationen bevares · ${Math.ceil(result.size / 1024)} KB.` };
  }
  if (blob.type === 'image/gif') throw new ImportFailure('LUP-IMP-112');
  let bitmap;
  try { bitmap = await createImageBitmap(blob); } catch { throw new ImportFailure('LUP-IMP-112'); }
  try {
    if (!bitmap.width || !bitmap.height || bitmap.width * bitmap.height > MAX_PIXELS) throw new ImportFailure('LUP-IMP-114');
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

async function prepare(blob, fileName, metadata = null, method = 'local-file') {
  const previousGeneration = generation, previousEditor = cropEditor;
  const current = ++generation;
  if (!prepared) {
    document.querySelector('#empty-preview').hidden = false;
    document.querySelector('#empty-heading').textContent = 'Dit billede er på vej';
    document.querySelector('#empty-description').textContent = 'Vi gør det klar til dit næste gode udsnit.';
  }
  setBusy(true); message('Klargør billedet…', false, 'progress');
  try {
    const result = await adaptImage(blob);
    if (current !== generation) return false;
    if (!await installImage(blob, result, fileName, current, metadata)) return false;
    searchSource = metadata;
    updateSourceCredit();
    originalSource = blob; originalPrepared = result.blob; originalFileName = fileName; originalNote = result.note;
    backgroundEdited = false;
    clearImportFailure();
    const note = document.querySelector('#import-method-note');
    note.hidden = !['canvas', 'page-fetch', 'embedded', 'blob', 'capture-cache'].includes(method);
    note.textContent = method === 'canvas' ? 'Det viste billede er hentet som et PNG-stillbillede.' : 'Billedet er hentet fra originalsiden.';
    message('Billedet er klar. Vælg udsnit og zoom, og gem emojien herfra.');
    return true;
  } catch (error) {
    if (current !== generation) return;
    if (previousEditor === cropEditor) generation = previousGeneration;
    showImportFailure(error);
    return false;
  }
  finally { if (current === generation || previousGeneration === generation) setBusy(false); }
}

async function installImage(blob, result, fileName, current, metadata = searchSource) {
  const previous = cropEditor;
  const editor = await createCropEditor(document.querySelector('#crop-editor'), blob, state => {
    if (current !== generation) return;
    cropActive = state.enabled;
    document.querySelector('#details').textContent = state.enabled
      ? `Kvadratisk udsnit · ${Math.round(state.zoom * 100)} % zoom · ${result.blob.type === 'image/gif' ? 'GIF-animationen bevares.' : 'gemmes som PNG-stillbillede.'}`
      : `${Math.ceil(result.blob.size / 1024)} KB · ${result.note}`;
    for (const slider of document.querySelectorAll('#crop-editor input[type="range"]')) {
      const progress = (Number(slider.value) - Number(slider.min)) / (Number(slider.max) - Number(slider.min));
      slider.style.setProperty('--range-progress', `${Math.max(0, Math.min(100, progress * 100))}%`);
    }
    refreshImageActions();
    requestPreview();
  }, { wholeBlob: result.blob, previewId: 'preview', encodeGif });
  if (current !== generation) { editor.destroy(); return false; }
  previous?.destroy();
  cropEditor = editor;
  prepared = result.blob;
  compactCropControls();
  cropActive = false;
  document.querySelector('#preview-wrap').hidden = false;
  document.querySelector('#empty-preview').hidden = true;
  if (!editedName) name.value = sanitizeName(fileName);
  if (!editedAlt) altText.value = metadata?.title || name.value.replaceAll('_', ' ');
  updateMetadataPreview(); requestPreview();
  permissionButton.hidden = true;
  refreshImageActions();
  return true;
}

function updateSourceCredit() {
  document.querySelector('#source-credit').hidden = !searchSource;
  if (!searchSource) return;
  const title = document.querySelector('#source-title');
  title.textContent = searchSource.title; title.href = searchSource.sourceUrl;
  document.querySelector('#source-creator').textContent = searchSource.creator ? `Af ${searchSource.creator}` : 'Se ophav og detaljer på kildesiden.';
  const license = document.querySelector('#source-license');
  license.textContent = `Markeret ${searchSource.licenseLabel}`; license.href = searchSource.licenseUrl;
}

document.querySelector('#back-to-search').addEventListener('click', async () => {
  if (Number.isInteger(searchTabId) && searchTabId > 0) {
    try { await chrome.tabs.update(searchTabId, { active: true }); return; } catch { /* The original tab may have been closed. */ }
  }
  await chrome.tabs.create({ url: chrome.runtime.getURL('search.html') });
});

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
  setBusy(true); message('Fjerner baggrunden lokalt… Første gang kan tage lidt længere tid.', false, 'progress');
  try {
    const image = await cropEditor.getPreviewBlob();
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
    message('Baggrunden er fjernet. Gennemgå resultatet, eller vælg Brug original.', false, 'success');
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

function sourceFilename() {
  let filename = sourceMetadata?.title || sourceInfo.sourceAltText || source?.pathname.split('/').at(-1) || 'emoji';
  try { filename = decodeURIComponent(filename); } catch { }
  return filename;
}
function captureBlob(response) {
  if (!response?.ok) throw new ImportFailure(response?.code || 'LUP-IMP-003', response?.context);
  if (typeof response.base64 !== 'string' || !response.base64.length || response.base64.length > Math.ceil(MAX_DOWNLOAD_BYTES / 3) * 4
      || !['image/png', 'image/jpeg', 'image/gif', 'image/webp', 'image/avif'].includes(response.contentType)) throw new ImportFailure('LUP-IMP-005');
  try {
    const raw = atob(response.base64);
    if (!raw.length || raw.length > MAX_DOWNLOAD_BYTES) throw new Error();
    return new Blob([Uint8Array.from(raw, char => char.charCodeAt(0))], { type: response.contentType });
  } catch { throw new ImportFailure('LUP-IMP-005'); }
}
async function loadCapturedSource(refresh = false, pixels = false) {
  const response = await chrome.runtime.sendMessage({ type: refresh ? 'prepare:capture' : 'prepare:source', id, capturePixels: pixels });
  const blob = captureBlob(response);
  const method = response.method || 'capture-cache';
  const ready = await prepare(blob, sourceFilename(), sourceMetadata, method);
  if (ready && !editedAlt && sourceInfo.sourceAltText && !sourceMetadata) {
    altText.value = sourceInfo.sourceAltText.slice(0, 240); updateMetadataPreview();
  }
  return ready;
}
async function framePermissionCandidate() {
  const origin = sourceUrl(sourceInfo.sourceFrameOrigin);
  if (origin && !await chrome.permissions.contains({ origins: [originPattern(origin.href)] })) return origin.href;
  return null;
}
async function fetchSource(recapture = false) {
  if (importRunning || saved) return;
  importRunning = true; permissionUrl = null;
  setBusy(true); message('Henter billedet…', false, 'progress');
  let failure = null, permissionCandidate = null;
  try {
    if (recapture && Number.isInteger(sourceInfo.sourceTabId) && sourceInfo.sourceTabId > 0) {
      message('Prøver billedet fra originalsiden igen…', false, 'progress');
      try { if (await loadCapturedSource(true, false)) return; failure = lastImportFailure; }
      catch (error) { failure = error; }
      setBusy(true);
    }
    if (sourceInfo.hasCapture) {
      try { if (await loadCapturedSource()) return; failure = lastImportFailure; }
      catch (error) { failure = error; }
      setBusy(true);
    }
    const urls = [...new Set([source?.href, ...(sourceInfo.candidates || []), sourceMetadata?.thumbnailUrl]
      .map(value => sourceUrl(value)?.href).filter(Boolean))].slice(0, 4);
    const deadline = Date.now() + 25_000;
    for (let index = 0; index < urls.length; index++) {
      const url = urls[index];
      if (Date.now() >= deadline) break;
      setBusy(true); message(index ? 'Prøver en anden billedkilde…' : 'Henter billedet…', false, 'progress');
      try {
        const blob = await boundedImageFetch(url, { timeoutMs: Math.min(12_000, deadline - Date.now()) });
        if (await prepare(blob, sourceFilename(), sourceMetadata, index ? 'alternate-source' : 'direct')) return;
        failure = lastImportFailure;
      } catch (error) {
        if (error?.code === 'LUP-IMP-108' && !await chrome.permissions.contains({ origins: [originPattern(url)] })) {
          permissionCandidate ||= url;
          error = new ImportFailure('LUP-IMP-101', { host: new URL(url).hostname });
        }
        if (!failure || ['LUP-IMP-002', 'LUP-IMP-003', 'LUP-IMP-108', 'LUP-IMP-101'].includes(failure.code)) failure = error;
      }
    }
    if (!urls.length) {
      failure ||= new ImportFailure(sourceInfo.captureIssue?.code || 'LUP-IMP-003', sourceInfo.captureIssue?.context);
      permissionCandidate ||= await framePermissionCandidate();
    }
    permissionUrl = permissionCandidate;
    if (permissionUrl) permissionButton.textContent = `Giv adgang til ${new URL(permissionUrl).hostname}`;
    showImportFailure(failure || new ImportFailure('LUP-IMP-108'));
  } catch (error) { showImportFailure(error, 'LUP-IMP-108'); }
  finally { importRunning = false; setBusy(false); }
}

document.querySelector('#retry-import').addEventListener('click', () => { if (!busy) void fetchSource(true); });
document.querySelector('#capture-visible').addEventListener('click', async () => {
  if (busy || saved) return;
  setBusy(true); message('Henter det valgte billede fra originalsiden…', false, 'progress');
  try { await loadCapturedSource(true, true); }
  catch (error) { showImportFailure(error, 'LUP-IMP-002'); }
  finally { setBusy(false); }
});
permissionButton.addEventListener('click', () => {
  if (!permissionUrl || busy || saved) return;
  // Must run directly in the click gesture, for one exact origin only.
  const target = permissionUrl;
  void chrome.permissions.request({ origins: [originPattern(target)] }).then(async granted => {
    if (!granted) { showImportFailure(new ImportFailure('LUP-IMP-102', { host: new URL(target).hostname })); return; }
    if (!source && sourceInfo.sourceTabId) {
      setBusy(true);
      try { await loadCapturedSource(true, false); }
      catch (error) { showImportFailure(error, 'LUP-IMP-002'); }
      finally { setBusy(false); }
    } else await fetchSource();
  }).catch(() => showImportFailure(new ImportFailure('LUP-IMP-115')));
});

fileInput.addEventListener('change', () => { const file = fileInput.files?.[0]; if (file && !busy && !saved) { void prepare(file, file.name); fileInput.value = ''; } });
const stage = document.querySelector('#editor-stage');
let dragDepth = 0;
stage.addEventListener('dragenter', event => {
  if (busy || saved || fileInput.disabled || !event.dataTransfer?.types.includes('Files')) return;
  event.preventDefault(); dragDepth++; stage.classList.add('drag-over');
});
stage.addEventListener('dragover', event => {
  if (event.dataTransfer?.types.includes('Files')) { event.preventDefault(); event.dataTransfer.dropEffect = busy || saved || fileInput.disabled ? 'none' : 'copy'; }
});
stage.addEventListener('dragleave', () => { if (--dragDepth <= 0) { dragDepth = 0; stage.classList.remove('drag-over'); } });
stage.addEventListener('drop', event => {
  if (!event.dataTransfer?.types.includes('Files')) return;
  event.preventDefault(); dragDepth = 0; stage.classList.remove('drag-over');
  if (busy || saved || fileInput.disabled) return;
  const file = event.dataTransfer.files[0]; if (file) void prepare(file, file.name);
});

document.querySelector('#form').addEventListener('submit', async event => {
  event.preventDefault();
  if (!prepared || busy || saved) return;
  if (!connected) { message('Forbind LUP i indstillingerne først.', true); return; }
  const shortName = name.value.trim();
  if (!/^[a-z0-9_-]{2,32}$/.test(shortName)) { message('Brug 2–32 små bogstaver, tal, _ eller - i navnet.', true); name.focus(); return; }
  if (!altText.value.trim() || altText.value.length > 240) { message('Skriv en kort beskrivelse af billedet.', true); altText.focus(); return; }
  uploading = true; setBusy(true); message('Gemmer din emoji på LUP…', false, 'progress');
  try {
    const image = cropEditor ? await cropEditor.getBlob() : prepared;
    const bytes = new Uint8Array(await image.arrayBuffer());
    let binary = ''; for (const byte of bytes) binary += String.fromCharCode(byte);
    const response = await chrome.runtime.sendMessage({ type: 'prepare:upload', id, name: shortName, altText: altText.value.trim(), contentType: image.type, base64: btoa(binary) });
    if (!response?.ok) {
      if (typeof response?.code === 'string' && response.code.startsWith('LUP-IMP-')) throw new ImportFailure(response.code);
      throw new Error(response?.message || 'Emojien kunne ikke gemmes.');
    }
    saved = true;
    uploading = false;
    message(`:${response.name}: er gemt i LUPs emojibibliotek. Du kan lukke denne fane.`, false, 'success');
    name.disabled = altText.disabled = true;
    setBusy(false);
    if (image.type === 'image/gif') { clearTimeout(previewTimer); previewRevision++; showPreviewBlob(image); }
  } catch (error) { uploading = false; error instanceof ImportFailure ? showImportFailure(error) : message(error.message, true); setBusy(false); }
});

window.addEventListener('unload', () => {
  backgroundSerial++;
  const job = backgroundJob; backgroundJob = null;
  if (job) void backgroundModule?.then(module => module.cancel(job)).catch(() => {});
  cropEditor?.destroy();
  clearPreview();
  void gifModule?.then(module => module.cancelAllGifs()).catch(() => {});
});

async function start() {
  if (!validId(id)) { showImportFailure(new ImportFailure('LUP-IMP-001')); fileInput.disabled = true; return; }
  try {
    let response;
    for (let attempt = 0; attempt < 8; attempt++) {
      response = await chrome.runtime.sendMessage({ type: 'prepare:get', id });
      if (!response?.retry) break;
      await new Promise(resolve => setTimeout(resolve, 250));
    }
    if (!response?.ok) throw new ImportFailure(response?.code || 'LUP-IMP-001');
    sourceInfo = response;
    await updateConnection();
    sourceMetadata = normalizeGifSearchResult(response.searchSource) || normalizeSearchImage(response.searchSource);
    if (Number.isInteger(response.searchTabId) && response.searchTabId > 0) {
      searchTabId = response.searchTabId;
      document.querySelector('#back-to-search').hidden = false;
    }
    source = sourceUrl(response.srcUrl);
    await fetchSource();
  } catch (error) { showImportFailure(error); }
}
void start();
