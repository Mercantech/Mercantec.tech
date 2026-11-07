import test from 'node:test';
import assert from 'node:assert/strict';
import { normalizeSearchQuery, normalizeSearchImage, searchImages } from '../image-search.mjs';

const id = '145e0b37-0577-416b-8d05-27c0b0bdde46';
const image = { id, title: 'A cat', creator: 'Photographer', license: 'cc0', foreign_landing_url: 'https://example.test/photo', url: 'https://untrusted.test/original', thumbnail: 'https://untrusted.test/thumb' };

test('search keeps Danish query text and rejects empty, overlong and control input', () => {
  assert.equal(normalizeSearchQuery('  blå   kat  '), 'blå kat');
  for (const input of ['', 'x', ' '.repeat(6), 'a'.repeat(101), 'a\nb', null, {}]) assert.equal(normalizeSearchQuery(input), null);
});
test('only marked CC0/public domain records with safe source links are accepted', () => {
  assert.equal(normalizeSearchImage(image).licenseLabel, 'CC0');
  assert.equal(normalizeSearchImage({ ...image, license: 'pdm' }).licenseLabel, 'Public domain');
  for (const changed of [{ license: 'by-sa' }, { license: 'CC0' }, { id: '../other' }, { foreign_landing_url: 'javascript:alert(1)' }, { foreign_landing_url: 'https://user:secret@example.test' }, { mature: true }])
    assert.equal(normalizeSearchImage({ ...image, ...changed }), null);
});
test('image download/proxy URLs are rebuilt, ignoring untrusted provider URLs and markup', () => {
  const result = normalizeSearchImage({ ...image, title: '<b>A cat</b>\u0000', creator: '<a href="https://evil.test">Name</a>' });
  assert.equal(result.title, 'A cat'); assert.equal(result.creator, 'Name');
  assert.equal(result.imageUrl, `https://api.openverse.org/v1/images/${id}/thumb/?full_size=true`);
  assert.equal(result.thumbnailUrl, `https://api.openverse.org/v1/images/${id}/thumb/`);
  assert.equal(result.licenseUrl, 'https://creativecommons.org/publicdomain/zero/1.0/');
});
test('provider search uses fixed public endpoint, fixed filters, bounded pagination and no credentials', async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async (value, options) => {
      const url = new URL(value);
      assert.equal(url.origin, 'https://api.openverse.org'); assert.equal(url.pathname, '/v1/images/');
      assert.equal(url.searchParams.get('q'), 'cat'); assert.equal(url.searchParams.get('license'), 'cc0,pdm');
      assert.equal(url.searchParams.get('mature'), 'false'); assert.equal(url.searchParams.get('page_size'), '20');
      assert.equal(options.credentials, 'omit'); assert.equal(options.referrerPolicy, 'no-referrer');
      assert.equal(options.redirect, 'error'); assert.equal(options.headers.Authorization, undefined);
      return Response.json({ results: [image, image, { ...image, license: 'by' }], page_count: 50 });
    };
    const result = await searchImages(' cat '); assert.equal(result.items.length, 1); assert.equal(result.nextPage, 2);
    assert.equal((await searchImages('cat', 10)).nextPage, null);
    for (const page of [0, 11, 1.5, '1']) await assert.rejects(searchImages('cat', page), /ugyldig/);
  } finally { globalThis.fetch = original; }
});
test('rate limits, invalid JSON and oversize responses produce useful static errors', async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response('private upstream message', { status: 429 });
    await assert.rejects(searchImages('cat'), /søgegrænse/);
    globalThis.fetch = async () => new Response('{');
    await assert.rejects(searchImages('cat'), /ugyldigt svar/);
    globalThis.fetch = async () => new Response('x'.repeat(1024 * 1024 + 1));
    await assert.rejects(searchImages('cat'), /for stort/);
  } finally { globalThis.fetch = original; }
});
