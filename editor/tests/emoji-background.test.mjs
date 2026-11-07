import test from 'node:test';
import assert from 'node:assert/strict';
import { backgroundPngBytes, backgroundPngName, emojiFileDataUrl } from '../src/emoji-background.mjs';

const png = Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 0]);
const result = bytes => ({ contentType: 'image/png', base64: Buffer.from(bytes).toString('base64') });

test('only bounded PNG data can replace the user original', () => {
  assert.deepEqual(backgroundPngBytes(result(png)), png);
  assert.throws(() => backgroundPngBytes({ ...result(png), contentType: 'image/jpeg' }));
  assert.throws(() => backgroundPngBytes(result([71, 73, 70, 56, 57, 97])));
  assert.throws(() => backgroundPngBytes({ contentType: 'image/png', base64: 'not-base64!' }));
  const maximum = new Uint8Array(256 * 1024); maximum.set(png);
  assert.equal(backgroundPngBytes(result(maximum)).length, maximum.length);
  assert.throws(() => backgroundPngBytes(result(new Uint8Array(256 * 1024 + 1))));
});

test('processed filename changes extension without carrying source paths', () => {
  assert.equal(backgroundPngName('C:\\Photos\\Kat.JPG'), 'Kat.png');
  assert.equal(backgroundPngName('../../billede.gif'), 'billede.png');
  assert.equal(backgroundPngName(''), 'emoji.png');
  assert.equal(backgroundPngName('x'.repeat(300) + '.webp').length, 224);
});

test('source data URL preserves exact image bytes and uses an accepted MIME', async () => {
  const file = { name: 'test.gif', arrayBuffer: async () => png.buffer };
  assert.equal(await emojiFileDataUrl(file), `data:image/gif;base64,${Buffer.from(png).toString('base64')}`);
});
