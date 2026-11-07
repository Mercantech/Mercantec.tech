const MAX_INPUT = 8 * 1024 * 1024;
const MAX_BYTES = 256 * 1024;
const MAX_FRAMES = 300;
const MAX_PIXELS = 16 * 1024 * 1024;
const MAX_FRAME_PIXELS = 30_000_000;
const MAX_DURATION = 30_000;
const invalid = () => new Error('GIF-filen er beskadiget eller har et format, der ikke understøttes.');

/** Read GIF block boundaries before asking the browser to allocate decoded frames. */
export function inspectGif(bytes) {
  if (!(bytes instanceof Uint8Array) || bytes.length < 14 || bytes.length > MAX_INPUT) throw invalid();
  const word = offset => bytes[offset] | bytes[offset + 1] << 8;
  const ascii = (offset, length) => String.fromCharCode(...bytes.subarray(offset, offset + length));
  if (!['GIF87a', 'GIF89a'].includes(ascii(0, 6))) throw invalid();
  const width = word(6), height = word(8);
  if (!width || !height || width > 16384 || height > 16384 || width * height > MAX_PIXELS)
    throw new Error('GIF-billedet har for store dimensioner. Vælg en mindre GIF.');
  let offset = 13 + (bytes[10] & 128 ? 3 * (1 << ((bytes[10] & 7) + 1)) : 0);
  let repeat = -1, delay = 100, duration = 0, ended = false;
  const delays = [];
  const blocks = callback => {
    while (true) {
      if (offset >= bytes.length) throw invalid();
      const size = bytes[offset++];
      if (!size) return;
      if (offset + size > bytes.length) throw invalid();
      callback?.(offset, size); offset += size;
    }
  };
  while (offset < bytes.length) {
    const marker = bytes[offset++];
    if (marker === 0x3b) { ended = true; break; }
    if (marker === 0x21) {
      const label = bytes[offset++];
      if (label === 0xf9) {
        if (offset + 6 > bytes.length || bytes[offset] !== 4 || bytes[offset + 5] !== 0) throw invalid();
        const rawDelay = word(offset + 2);
        // Browsers display zero/one-centisecond GIF frames for 100 ms.
        delay = rawDelay < 2 ? 100 : rawDelay * 10;
        offset += 6;
      } else if (label === 0xff) {
        const size = bytes[offset++];
        if (offset + size > bytes.length) throw invalid();
        const application = ascii(offset, size); offset += size;
        blocks((start, length) => {
          if (['NETSCAPE2.0', 'ANIMEXTS1.0'].includes(application) && length >= 3 && bytes[start] === 1)
            repeat = word(start + 1);
        });
      } else {
        blocks();
        if (label === 0x01) delay = 100;
      }
      continue;
    }
    if (marker !== 0x2c || offset + 9 > bytes.length) throw invalid();
    const x = word(offset), y = word(offset + 2), frameWidth = word(offset + 4), frameHeight = word(offset + 6), flags = bytes[offset + 8];
    if (!frameWidth || !frameHeight || x + frameWidth > width || y + frameHeight > height) throw invalid();
    offset += 9;
    if (flags & 128) offset += 3 * (1 << ((flags & 7) + 1));
    if (offset >= bytes.length || bytes[offset] < 1 || bytes[offset] > 8) throw invalid();
    offset++; blocks();
    delays.push(delay); duration += delay; delay = 100;
    if (delays.length > MAX_FRAMES || width * height * delays.length > MAX_FRAME_PIXELS)
      throw new Error('GIF-animationen er for omfattende. Vælg en GIF med færre eller mindre billeder.');
    if (duration > MAX_DURATION) throw new Error('GIF-animationen må højst vare 30 sekunder. Vælg en kortere GIF.');
  }
  if (!ended || !delays.length) throw invalid();
  return { width, height, frameCount: delays.length, delays, duration, repeat };
}

export function normalizeGifCrop(value, width, height) {
  if (value === null || value === undefined) return { x: 0, y: 0, width, height };
  if (!value || typeof value !== 'object' || !['x', 'y', 'size'].every(key => Number.isFinite(value[key]))
      || value.x < 0 || value.y < 0 || value.size <= 0 || value.x + value.size > width + 1e-5 || value.y + value.size > height + 1e-5)
    throw new Error('GIF-udsnittet er ugyldigt. Vælg udsnittet igen.');
  return { x: value.x, y: value.y, width: value.size, height: value.size };
}
