import { GIFEncoder, quantize, applyPalette } from './image-tools/gifenc.mjs';

import { inspectGif, normalizeGifCrop } from './gif-core.mjs';
const MAX_BYTES = 256 * 1024;
const invalid = () => new Error('GIF-filen er beskadiget eller har et format, der ikke understøttes.');

function dimensions(rectangle, edge) {
  const scale = Math.min(1, edge / Math.max(rectangle.width, rectangle.height));
  return { width: Math.max(1, Math.round(rectangle.width * scale)), height: Math.max(1, Math.round(rectangle.height * scale)) };
}

export async function processGifBytes(buffer, { crop = null, maxBytes = MAX_BYTES, maxDimension = 256 } = {}) {
  if (!(buffer instanceof ArrayBuffer) || !Number.isInteger(maxBytes) || maxBytes < 1 || maxBytes > MAX_BYTES
      || !Number.isInteger(maxDimension) || maxDimension < 1 || maxDimension > 256) throw invalid();
  const bytes = new Uint8Array(buffer), info = inspectGif(bytes), rectangle = normalizeGifCrop(crop, info.width, info.height);
  if (typeof ImageDecoder === 'undefined' || typeof OffscreenCanvas === 'undefined' || !await ImageDecoder.isTypeSupported('image/gif'))
    throw new Error('Denne Chrome-version kan ikke behandle GIF-animationer. Opdatér Chrome, og prøv igen.');
  const decoder = new ImageDecoder({ data: bytes, type: 'image/gif', preferAnimation: true });
  const edges = [...new Set([Math.min(128, maxDimension), Math.min(96, maxDimension), Math.min(64, maxDimension)])];
  const firstSize = dimensions(rectangle, edges[0]);
  const canvas = new OffscreenCanvas(firstSize.width, firstSize.height), context = canvas.getContext('2d', { willReadFrequently: true });
  const frames = [];
  try {
    await decoder.tracks.ready; await decoder.completed;
    if (decoder.tracks.selectedTrack?.frameCount !== info.frameCount) throw invalid();
    for (let i = 0; i < info.frameCount; i++) {
      const decoded = await decoder.decode({ frameIndex: i, completeFramesOnly: true });
      const frame = decoded.image;
      try {
        if (!decoded.complete || frame.displayWidth !== info.width || frame.displayHeight !== info.height) throw invalid();
        context.clearRect(0, 0, firstSize.width, firstSize.height);
        context.drawImage(frame, rectangle.x, rectangle.y, rectangle.width, rectangle.height, 0, 0, firstSize.width, firstSize.height);
        frames.push(context.getImageData(0, 0, firstSize.width, firstSize.height).data);
      } finally { frame.close(); }
    }
  } finally { decoder.close(); }
  if (!crop && bytes.byteLength <= maxBytes && Math.max(info.width, info.height) <= maxDimension) return bytes.slice().buffer;

  const sourceCanvas = new OffscreenCanvas(firstSize.width, firstSize.height), sourceContext = sourceCanvas.getContext('2d');
  for (const edge of edges) {
    const size = dimensions(rectangle, edge), output = new OffscreenCanvas(size.width, size.height), out = output.getContext('2d', { willReadFrequently: true });
    out.imageSmoothingEnabled = true; out.imageSmoothingQuality = 'high';
    const gif = GIFEncoder(); let tooLarge = false;
    for (let i = 0; i < frames.length; i++) {
      let rgba = frames[i];
      if (size.width !== firstSize.width || size.height !== firstSize.height) {
        sourceContext.putImageData(new ImageData(rgba, firstSize.width, firstSize.height), 0, 0);
        out.clearRect(0, 0, size.width, size.height); out.drawImage(sourceCanvas, 0, 0, size.width, size.height);
        rgba = out.getImageData(0, 0, size.width, size.height).data;
      }
      const palette = quantize(rgba, edge >= 128 ? 128 : 64, { format: 'rgba4444', oneBitAlpha: true, clearAlpha: true });
      const index = applyPalette(rgba, palette, 'rgba4444'), transparentIndex = palette.findIndex(color => color[3] === 0);
      gif.writeFrame(index, size.width, size.height, { palette, delay: info.delays[i], repeat: info.repeat,
        transparent: transparentIndex >= 0, transparentIndex, dispose: 2 });
      if (gif.bytesView().byteLength > maxBytes) { tooLarge = true; break; }
    }
    if (tooLarge) continue;
    gif.finish();
    const encoded = gif.bytes();
    if (encoded.byteLength <= maxBytes) return encoded.buffer;
  }
  throw new Error('GIF-animationen kan ikke gøres mindre end 256 KB uden at miste animationen. Vælg en kortere GIF.');
}

if (typeof self !== 'undefined' && typeof self.postMessage === 'function' && typeof document === 'undefined') {
  self.addEventListener('message', async event => {
    const jobId = event.data?.jobId;
    if (typeof jobId !== 'string' || jobId.length > 128) return;
    try {
      const result = await processGifBytes(event.data.bytes, event.data);
      self.postMessage({ jobId, bytes: result }, [result]);
    } catch (error) {
      self.postMessage({ jobId, error: error instanceof Error && /^GIF|^Denne Chrome/.test(error.message)
        ? error.message : 'GIF-filen kunne ikke behandles. Originalen er bevaret. Prøv en anden GIF.' });
    }
  });
}
