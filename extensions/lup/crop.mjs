const MAX_PIXELS = 16 * 1024 * 1024;
const MAX_BYTES = 256 * 1024;
const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(maximum, value));
const finite = (value, fallback) => Number.isFinite(value) ? value : fallback;
const owners = new WeakMap();

function dimensions(width, height) {
  if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 || width * height > MAX_PIXELS)
    throw new Error('Billedet må højst være 16 megapixel. Vælg et mindre billede.');
}

/** A square source rectangle; x/y are normalized pan positions in [-1, 1]. */
export function cropRectangle(width, height, state = {}) {
  dimensions(width, height);
  const zoom = clamp(finite(state.zoom, 1), 1, 4);
  const size = Math.min(width, height) / zoom;
  const x = clamp(finite(state.x, 0), -1, 1), y = clamp(finite(state.y, 0), -1, 1);
  return { x: (width - size) * (x + 1) / 2, y: (height - size) * (y + 1) / 2, size, zoom };
}

/** Preserve the current source center while zooming, subject to edge bounds. */
export function zoomCrop(width, height, state, requestedZoom) {
  const before = cropRectangle(width, height, state);
  const zoom = clamp(finite(requestedZoom, before.zoom), 1, 4);
  const size = Math.min(width, height) / zoom;
  const centerX = before.x + before.size / 2, centerY = before.y + before.size / 2;
  return { zoom,
    x: width > size ? clamp((2 * centerX - width) / (width - size), -1, 1) : 0,
    y: height > size ? clamp((2 * centerY - height) / (height - size), -1, 1) : 0 };
}

/** Dragging the displayed image right moves the sampled source rectangle left. */
export function dragCrop(width, height, state, dx, dy, displaySize) {
  const rectangle = cropRectangle(width, height, state);
  if (!Number.isFinite(displaySize) || displaySize <= 0) return { zoom: rectangle.zoom, x: finite(state.x, 0), y: finite(state.y, 0) };
  return { zoom: rectangle.zoom,
    x: width > rectangle.size ? clamp(finite(state.x, 0) - 2 * finite(dx, 0) * rectangle.size / (displaySize * (width - rectangle.size)), -1, 1) : 0,
    y: height > rectangle.size ? clamp(finite(state.y, 0) - 2 * finite(dy, 0) * rectangle.size / (displaySize * (height - rectangle.size)), -1, 1) : 0 };
}

const png = canvas => new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('Billedet kunne ikke beskæres. Prøv igen.')), 'image/png'));

/** Reusable editor; the full source is retained until dispose, while wholeBlob may be a smaller GIF/PNG. */
export async function createCropEditor(container, sourceBlob, onChange = () => {}, { wholeBlob = sourceBlob, previewId, encodeGif } = {}) {
  if (!(sourceBlob instanceof Blob) || !sourceBlob.size || sourceBlob.size > 8 * 1024 * 1024)
    throw new Error('Vælg et billede med indhold på højst 8 MB.');
  const owner = {};
  owners.set(container, owner);
  const bitmap = await createImageBitmap(sourceBlob);
  if (owners.get(container) !== owner) { bitmap.close(); throw new DOMException('En nyere billedkladde er valgt.', 'AbortError'); }
  try { dimensions(bitmap.width, bitmap.height); } catch (error) { bitmap.close(); throw error; }
  let state = { zoom: 1, x: 0, y: 0 }, enabled = false, disabled = false, disposed = false, pointer = null;
  const wholeUrl = URL.createObjectURL(wholeBlob);
  const animatedGif = wholeBlob.type === 'image/gif' && typeof encodeGif === 'function';
  const styleUrl = new URL('./crop.css', import.meta.url).href;
  if (![...document.querySelectorAll('link[data-lup-crop-style]')].some(link => link.href === styleUrl)) {
    const link = document.createElement('link'); link.rel = 'stylesheet'; link.href = styleUrl; link.dataset.lupCropStyle = '';
    document.head.append(link);
  }
  container.classList.add('lup-crop');
  container.innerHTML = `<div class="lup-crop-modes" role="group" aria-label="Billedformat">
    <button type="button" data-whole class="secondary" aria-pressed="true">Brug hele billedet</button>
    <button type="button" data-square class="secondary" aria-pressed="false">Beskær til kvadrat</button>
  </div>
  <div class="lup-crop-frame">
    <img data-whole-image alt="Billedet, der skal tilføjes som emoji" />
    <canvas data-canvas width="512" height="512" hidden tabindex="0" aria-label="Beskåret billede. Brug pilene for at flytte udsnittet, eller skyderne nedenfor."></canvas>
  </div>
  <div data-controls hidden>
    <p class="muted lup-crop-hint">Træk billedet i rammen, eller brug skyderne til at flytte udsnittet.</p>
    <label class="lup-crop-control">Zoom <span data-zoom-value aria-hidden="true">100 %</span><input data-zoom type="range" min="1" max="4" step="0.01" value="1" /></label>
    <label class="lup-crop-control">Vandret udsnit<input data-x type="range" min="-100" max="100" step="1" value="0" /></label>
    <label class="lup-crop-control">Lodret udsnit<input data-y type="range" min="-100" max="100" step="1" value="0" /></label>
    <button type="button" data-reset class="secondary">Nulstil udsnit</button>
    <p class="muted lup-crop-hint">${animatedGif ? 'GIF-animationen bevares ved gem. Beskæringen viser ét billede fra animationen.' : 'Udsnittet gemmes som et PNG-stillbillede med højst 256 × 256 pixels. GIF- og WebP-animation bevares ikke ved beskæring.'}</p>
  </div>`;
  const select = selector => container.querySelector(selector);
  const image = select('[data-whole-image]'), canvas = select('[data-canvas]'), controls = select('[data-controls]');
  const wholeButton = select('[data-whole]'), squareButton = select('[data-square]');
  const zoomInput = select('[data-zoom]'), xInput = select('[data-x]'), yInput = select('[data-y]');
  if (previewId) image.id = previewId;
  image.src = wholeUrl;
  const listeners = [];
  function listen(target, event, handler, options) { target.addEventListener(event, handler, options); listeners.push(() => target.removeEventListener(event, handler, options)); }
  function draw() {
    if (disposed) return;
    wholeButton.setAttribute('aria-pressed', String(!enabled)); squareButton.setAttribute('aria-pressed', String(enabled));
    image.hidden = enabled; canvas.hidden = !enabled; controls.hidden = !enabled;
    zoomInput.value = String(state.zoom); xInput.value = String(Math.round(state.x * 100)); yInput.value = String(Math.round(state.y * 100));
    select('[data-zoom-value]').textContent = `${Math.round(state.zoom * 100)} %`;
    zoomInput.setAttribute('aria-valuetext', `${Math.round(state.zoom * 100)} %`);
    const rectangle = cropRectangle(bitmap.width, bitmap.height, state);
    xInput.disabled = disabled || bitmap.width <= rectangle.size;
    yInput.disabled = disabled || bitmap.height <= rectangle.size;
    if (enabled) {
      const context = canvas.getContext('2d');
      context.clearRect(0, 0, canvas.width, canvas.height);
      context.imageSmoothingEnabled = true; context.imageSmoothingQuality = 'high';
      context.drawImage(bitmap, rectangle.x, rectangle.y, rectangle.size, rectangle.size, 0, 0, canvas.width, canvas.height);
    }
    onChange({ enabled, ...state });
  }
  function mode(value) { if (disposed || disabled) return; enabled = value; pointer = null; draw(); }
  listen(wholeButton, 'click', () => mode(false)); listen(squareButton, 'click', () => mode(true));
  listen(select('[data-reset]'), 'click', () => { if (disabled || disposed) return; state = { zoom: 1, x: 0, y: 0 }; draw(); });
  listen(zoomInput, 'input', () => { if (disabled || disposed) return; state = zoomCrop(bitmap.width, bitmap.height, state, Number(zoomInput.value)); draw(); });
  for (const [input, axis] of [[xInput, 'x'], [yInput, 'y']]) listen(input, 'input', () => {
    if (disabled || disposed) return; state = { ...state, [axis]: Number(input.value) / 100 }; draw();
  });
  listen(canvas, 'pointerdown', event => {
    if (disabled || disposed || !enabled || event.button !== 0 || pointer !== null) return;
    pointer = { id: event.pointerId, x: event.clientX, y: event.clientY };
    canvas.setPointerCapture(event.pointerId); event.preventDefault();
  });
  listen(canvas, 'pointermove', event => {
    if (disabled || disposed || !pointer || pointer.id !== event.pointerId) return;
    const next = dragCrop(bitmap.width, bitmap.height, state, event.clientX - pointer.x, event.clientY - pointer.y, canvas.getBoundingClientRect().width);
    pointer.x = event.clientX; pointer.y = event.clientY; state = next; draw(); event.preventDefault();
  });
  const releasePointer = event => { if (pointer?.id === event.pointerId) pointer = null; };
  listen(canvas, 'pointerup', releasePointer); listen(canvas, 'pointercancel', releasePointer); listen(canvas, 'lostpointercapture', releasePointer);
  listen(canvas, 'keydown', event => {
    if (disabled || disposed || !enabled) return;
    const amount = event.shiftKey ? 0.15 : 0.04;
    const axes = { ArrowLeft: ['x', -amount], ArrowRight: ['x', amount], ArrowUp: ['y', -amount], ArrowDown: ['y', amount] };
    if (axes[event.key]) {
      const [axis, delta] = axes[event.key]; state = { ...state, [axis]: clamp(state[axis] + delta, -1, 1) };
    } else if (['+', '=', '-', '_'].includes(event.key)) state = zoomCrop(bitmap.width, bitmap.height, state, state.zoom + (['-', '_'].includes(event.key) ? -0.1 : 0.1));
    else if (event.key === 'Home') state = { zoom: 1, x: 0, y: 0 };
    else return;
    draw(); event.preventDefault();
  });
  draw();
  const destroy = () => {
    if (disposed) return; disposed = true; pointer = null;
    for (const remove of listeners) remove();
    bitmap.close(); URL.revokeObjectURL(wholeUrl);
    if (owners.get(container) === owner) { owners.delete(container); container.replaceChildren(); }
  };
  async function exportBlob(preview = false) {
    if (disposed) throw new Error('Billedkladden er lukket. Vælg billedet igen.');
    if (!enabled) return wholeBlob;
    const rectangle = cropRectangle(bitmap.width, bitmap.height, state);
    if (animatedGif && !preview) {
      const result = await encodeGif(sourceBlob, rectangle);
      if (disposed) throw new Error('Billedkladden er lukket. Vælg billedet igen.');
      return result;
    }
    for (const edge of [256, 192, 128]) {
      const output = document.createElement('canvas'); output.width = edge; output.height = edge;
      const context = output.getContext('2d'); context.imageSmoothingEnabled = true; context.imageSmoothingQuality = 'high';
      context.drawImage(bitmap, rectangle.x, rectangle.y, rectangle.size, rectangle.size, 0, 0, edge, edge);
      const blob = await png(output);
      if (disposed) throw new Error('Billedkladden er lukket. Vælg billedet igen.');
      if (blob.size <= MAX_BYTES) return blob;
    }
    throw new Error('Udsnittet kunne ikke gøres mindre end 256 KB. Vælg et andet billede.');
  }
  return {
    getState: () => ({ enabled, ...state }),
    setMode(value) { if (!['whole', 'square'].includes(value)) throw new Error('Ukendt billedformat.'); mode(value === 'square'); },
    setDisabled(value) {
      disabled = Boolean(value); pointer = null;
      for (const control of container.querySelectorAll('button,input')) control.disabled = disabled;
      canvas.tabIndex = disabled ? -1 : 0; draw();
    },
    getBlob: () => exportBlob(false),
    getPreviewBlob: () => exportBlob(true),
    dispose: destroy,
    destroy
  };
}
