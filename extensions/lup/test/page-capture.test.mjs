import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { captureSelectedImage } from '../page-capture.mjs';

const png = Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0]);
const gif = Uint8Array.from(Buffer.from('GIF89a\u0002\u0000\u0002\u0000fixture', 'binary'));
const address = 'https://page.example.test/selected.png';
function image(changes = {}) {
  const attributes = {};
  return { currentSrc: address, src: address, alt: 'Valgt motiv', complete: true, naturalWidth: 2, naturalHeight: 2,
    getAttribute: name => attributes[name] ?? null, closest: () => null, ...changes };
}
function setup({ images = [], fetch: network = async () => new Response(png, { headers: { 'content-type': 'image/png' } }),
  dimensions = [2, 2], canvas = null } = {}) {
  const calls = [], closes = [];
  const document = { baseURI: 'https://page.example.test/photos/current', images,
    createElement: name => { assert.equal(name, 'canvas'); if (!canvas) throw Error('Canvas must not be read'); return canvas; } };
  const context = vm.createContext({ document, location: { origin: 'https://page.example.test' }, URL, Blob,
    Uint8Array, AbortController, Date, Error, DOMException, btoa, setTimeout, clearTimeout, matchMedia: () => ({ matches: true }),
    fetch: async (...args) => { calls.push(args); return network(...args); },
    createImageBitmap: async () => ({ width: dimensions[0], height: dimensions[1], close: () => closes.push(true) }) });
  // The actual injected function is reconstructed without access to module scope.
  const run = vm.runInContext(`(${captureSelectedImage.toString()})`, context);
  return { run, calls, closes };
}
const code = result => result.issue?.code;

test('serialized capture reads exact same-origin image with ordinary referrer and same-origin cookies', async () => {
  const fixture = setup({ images: [image()] });
  const result = await fixture.run({ srcUrl: address });
  assert.equal(result.ok, true); assert.equal(result.method, 'page-fetch'); assert.equal(result.contentType, 'image/png');
  assert.equal(result.base64, Buffer.from(png).toString('base64')); assert.equal(result.altText, 'Valgt motiv');
  assert.equal(fixture.calls[0][0], address); assert.equal(fixture.calls[0][1].credentials, 'same-origin');
  assert.equal(fixture.calls[0][1].referrerPolicy, undefined);
  assert.equal(fixture.calls[0][1].headers, undefined); assert.equal(fixture.closes.length, 1);
});
test('data and correct-frame blob images retain original binary bytes and animation format', async () => {
  for (const srcUrl of ['data:image/gif;base64,' + Buffer.from(gif).toString('base64'), 'blob:https://page.example.test/selected-id']) {
    const fixture = setup({ fetch: async () => new Response(gif, { headers: { 'content-type': 'image/gif' } }) });
    const result = await fixture.run({ srcUrl });
    assert.equal(result.ok, true); assert.equal(result.method, 'embedded'); assert.equal(result.contentType, 'image/gif');
    assert.equal(result.base64, Buffer.from(gif).toString('base64')); assert.equal(result.animationLost, false);
    assert.equal(result.candidates.length, 0);
  }
});
test('responsive and picture candidates stay scoped to the one selected image', async () => {
  const selected = image({ src: 'https://cdn.example.test/fallback.jpg',
    getAttribute: name => name === 'srcset' ? 'https://cdn.example.test/photo?size=100,200 1x, https://cdn.example.test/high.jpg 2x' : null,
    closest: () => ({ querySelectorAll: () => [{ getAttribute: name => name === 'srcset' ? 'https://cdn.example.test/picture.jpg 2x' : null }] }) });
  const unrelated = image({ currentSrc: 'https://unrelated.test/private.jpg', src: 'https://unrelated.test/private.jpg' });
  const fixture = setup({ images: [selected, unrelated], fetch: async () => { throw new TypeError('CORS'); } });
  const result = await fixture.run({ srcUrl: address });
  assert.equal(result.ok, false); assert.equal(result.canvasAvailable, true);
  assert.deepEqual(Array.from(result.candidates), [address, 'https://cdn.example.test/fallback.jpg',
    'https://cdn.example.test/photo?size=100,200', 'https://cdn.example.test/high.jpg', 'https://cdn.example.test/picture.jpg']);
  assert(!fixture.calls.some(([url]) => url.includes('unrelated')));
});
test('unsafe schemes, foreign blob origins and embedded SVG are rejected before any fetch', async () => {
  for (const srcUrl of ['javascript:alert(1)', 'file:///private.png', 'chrome://settings/', 'data:text/html,<h1>secret</h1>',
    'blob:https://other.example.test/private', 'https://name:secret@page.example.test/photo.png', 'data:image/svg+xml,<svg/>']) {
    const fixture = setup(); const result = await fixture.run({ srcUrl });
    assert.equal(result.ok, false); assert.equal(fixture.calls.length, 0);
    assert.equal(code(result), srcUrl.startsWith('data:image/svg') ? 'LUP-IMP-113' : 'LUP-IMP-002');
  }
});
test('HTTP status, HTML login responses and SVG have distinct safe diagnostic codes', async () => {
  for (const [status, expected] of [[401, 103], [403, 104], [404, 105], [410, 105], [429, 106], [500, 107]]) {
    const fixture = setup({ fetch: async () => new Response('server secret', { status }) });
    const result = await fixture.run({ srcUrl: address });
    assert.equal(code(result), 'LUP-IMP-' + expected); assert.equal(result.issue.context.status, status);
    assert(!JSON.stringify(result).includes('server secret'));
  }
  for (const [type, body, expected] of [['text/html', '<form>login</form>', 111], ['image/svg+xml', '<svg/>', 113], ['text/plain', '<?xml version="1.0"?><svg/>', 113]]) {
    const fixture = setup({ fetch: async () => new Response(body, { headers: { 'content-type': type } }) });
    assert.equal(code(await fixture.run({ srcUrl: address })), 'LUP-IMP-' + expected);
  }
});
test('a CORS-blocked responsive alternative does not hide the original HTTP diagnostic', async () => {
  const selected = image({ src: 'https://cdn.example.test/fallback.jpg' });
  const fixture = setup({ images: [selected], fetch: async url => {
    if (url === address) return new Response('login needed', { status: 401 });
    throw new TypeError('Blocked by CORS');
  } });
  const result = await fixture.run({ srcUrl: address });
  assert.equal(code(result), 'LUP-IMP-103'); assert.equal(result.issue.context.status, 401);
});
test('declared and streamed byte bounds and decoded pixel bounds are enforced', async () => {
  const declared = setup({ fetch: async () => new Response(png, { headers: { 'content-length': String(8 * 1024 * 1024 + 1) } }) });
  assert.equal(code(await declared.run({ srcUrl: address })), 'LUP-IMP-110');
  const streamed = setup({ fetch: async () => new Response(new Uint8Array(8 * 1024 * 1024 + 1)) });
  assert.equal(code(await streamed.run({ srcUrl: address })), 'LUP-IMP-110');
  const pixels = setup({ dimensions: [4097, 4097] });
  assert.equal(code(await pixels.run({ srcUrl: address })), 'LUP-IMP-114'); assert.equal(pixels.closes.length, 1);
});
test('canvas copying is opt-in and explicitly marks a PNG still image', async () => {
  let draws = 0;
  const canvas = { width: 0, height: 0, getContext: () => ({ drawImage: () => { draws++; } }), toBlob: done => done(new Blob([png], { type: 'image/png' })) };
  const fixture = setup({ images: [image()], canvas, fetch: async () => { throw new TypeError('Blocked'); } });
  const first = await fixture.run({ srcUrl: address });
  assert.equal(first.ok, false); assert.equal(first.canvasAvailable, true); assert.equal(draws, 0);
  const copied = await fixture.run({ srcUrl: address, capturePixels: true });
  assert.equal(copied.ok, true); assert.equal(copied.method, 'canvas'); assert.equal(copied.contentType, 'image/png');
  assert.equal(copied.animationLost, true); assert.equal(draws, 1);
});
test('ambiguous duplicate images are never copied through canvas', async () => {
  const fixture = setup({ images: [image(), image()], fetch: async () => { throw new TypeError('Blocked'); } });
  const result = await fixture.run({ srcUrl: address, capturePixels: true });
  assert.equal(result.ok, false); assert.equal(result.canvasAvailable, false); assert.equal(code(result), 'LUP-IMP-108');
});
test('tainted canvas remains blocked and cannot expose pixels', async () => {
  const canvas = { getContext: () => ({ drawImage: () => {} }), toBlob: () => { throw new DOMException('tainted', 'SecurityError'); } };
  const fixture = setup({ images: [image()], canvas, fetch: async () => { throw new TypeError('Blocked'); } });
  const result = await fixture.run({ srcUrl: address, capturePixels: true });
  assert.equal(result.ok, false); assert.equal(code(result), 'LUP-IMP-119'); assert.equal(result.base64, undefined);
});
