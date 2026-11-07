import test from 'node:test';
import assert from 'node:assert/strict';
import { isGifResultId, normalizeGifSearchResult, searchGifs } from '../gif-search.mjs';
const id = 'df5f72cb-cfd2-467c-9514-34ab250557b9';
const image = { id, title: 'Smiling laughing yes', creator: 'Artist', license: 'cc0', filetype: 'gif', source: 'wikimedia',
  foreign_landing_url: 'https://commons.wikimedia.org/wiki/File:Smiling_laughing_yes_Animated_icon.gif',
  url: 'https://upload.wikimedia.org/wikipedia/commons/5/5f/Smiling_laughing_yes_Animated_icon.gif' };
test('GIF results have separate IDs and use original animated source, never the flattening proxy', () => {
  const item = normalizeGifSearchResult(image);
  assert.equal(item.id, 'gif:' + id); assert(isGifResultId(item.id)); assert.equal(item.provider, 'openverse-gif');
  assert.equal(item.imageUrl, image.url); assert.equal(item.thumbnailUrl, image.url); assert.equal(item.license, 'cc0');
  assert.deepEqual(normalizeGifSearchResult(item), item);
  const { imageUrl, ...draft } = item; assert.deepEqual(normalizeGifSearchResult(draft), item);
});
test('GIF sources reject other hosts, transforms, traversal, unsafe links and non-public-domain metadata', () => {
  for (const changed of [{ url: 'https://evil.test/x.gif' }, { url: image.url + '?width=100' }, { url: image.url.replace('/5/5f/', '/%2e%2e/%2e%2e/') },
    { url: image.url.replace('Smiling_', '%2FSmiling_') }, { url: image.url.replace('https:', 'http:') }, { url: image.url.replace('.gif', '.png') },
    { license: 'by-sa' }, { filetype: 'jpg' }, { foreign_landing_url: 'javascript:alert(1)' }]) assert.equal(normalizeGifSearchResult({ ...image, ...changed }), null);
});
test('public GIF search sets actual extension filter and permits blank discovery without credentials', async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async (value, options) => {
      const url = new URL(value); assert.equal(url.origin, 'https://api.openverse.org');
      assert.equal(url.searchParams.get('extension'), 'gif'); assert.equal(url.searchParams.get('source'), 'wikimedia');
      assert.equal(url.searchParams.get('license'), 'cc0,pdm'); assert.equal(url.searchParams.get('mature'), 'false');
      assert.equal(options.credentials, 'omit'); assert.equal(options.headers.Authorization, undefined); assert.equal(options.referrerPolicy, 'no-referrer');
      return Response.json({ results: [image, image, { ...image, license: 'by' }], page_count: 11 });
    };
    const result = await searchGifs(''); assert.equal(result.items.length, 1); assert.equal(result.nextPage, 2);
    assert.equal((await searchGifs('dance', 10)).nextPage, null);
    for (const [value, page] of [['x', 1], ['dance', 11], [null, 1], ['a\nb', 1]]) await assert.rejects(searchGifs(value, page));
  } finally { globalThis.fetch = original; }
});
