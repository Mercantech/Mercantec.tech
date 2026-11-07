import test from 'node:test';
import assert from 'node:assert/strict';
import { inspectGif, normalizeGifCrop } from '../gif-core.mjs';

function fixture({ width = 1, height = 1, frames = 2, delay = 10 } = {}) {
  const header = [...Buffer.from('GIF89a'), width & 255, width >> 8, height & 255, height >> 8, 128, 0, 0,
    0, 0, 0, 255, 255, 255];
  const loop = [33, 255, 11, ...Buffer.from('NETSCAPE2.0'), 3, 1, 2, 0, 0];
  const frame = [33, 249, 4, 0, delay & 255, delay >> 8, 0, 0,
    44, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 68, 1, 0];
  return Uint8Array.from([...header, ...loop, ...Array.from({ length: frames }, () => frame).flat(), 59]);
}

test('GIF metadata preserves frame count, delay and finite repetition count', () => {
  assert.deepEqual(inspectGif(fixture()), { width: 1, height: 1, frameCount: 2, delays: [100, 100], duration: 200, repeat: 2 });
  const zeroDelay = inspectGif(fixture({ delay: 0 }));
  assert.deepEqual(zeroDelay.delays, [100, 100]);
});
test('GIF parser rejects truncated or non-GIF inputs rather than flattening them', () => {
  const bytes = fixture();
  assert.throws(() => inspectGif(bytes.slice(0, -1)), /beskadiget/);
  assert.throws(() => inspectGif(new Uint8Array(20)), /beskadiget/);
  assert.throws(() => inspectGif(new Uint8Array(8 * 1024 * 1024 + 1)), /beskadiget/);
});
test('GIF frame, decoded-pixel and duration budgets are checked before decoding', () => {
  assert.throws(() => inspectGif(fixture({ frames: 301 })), /omfattende/);
  assert.throws(() => inspectGif(fixture({ width: 500, height: 500, frames: 121 })), /omfattende/);
  assert.throws(() => inspectGif(fixture({ delay: 65535 })), /30 sekunder/);
  assert.throws(() => inspectGif(fixture({ width: 16384, height: 16384 })), /dimensioner/);
});
test('GIF crop bounds accept valid fractional source coordinates and reject unsafe rectangles', () => {
  assert.deepEqual(normalizeGifCrop(null, 80, 40), { x: 0, y: 0, width: 80, height: 40 });
  assert.deepEqual(normalizeGifCrop({ x: 12.5, y: 5, size: 20 }, 80, 40), { x: 12.5, y: 5, width: 20, height: 20 });
  for (const value of [{ x: -1, y: 0, size: 40 }, { x: 0, y: 0, size: 0 }, { x: 60, y: 0, size: 40 }, { x: Infinity, y: 0, size: 40 }])
    assert.throws(() => normalizeGifCrop(value, 80, 40), /udsnittet/);
});
