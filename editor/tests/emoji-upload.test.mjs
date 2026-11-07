import assert from 'node:assert/strict';
import test from 'node:test';
import { EMOJI_MAX_BYTES, analyzePlatformEmoji, mergeEmojiAnalysis, normalizeEmojiMetadata, normalizeEmojiName, suggestEmojiName, uploadPlatformEmoji, validateEmojiFile, validateEmojiMetadata } from '../src/emoji-upload.mjs';

test('filename defaults become short names and retain user-overridable names', () => {
  assert.equal(suggestEmojiName('C:\\billeder\\Min Glade ÆØÅ 😄.PNG'), 'min_glade_aeoa');
  assert.equal(suggestEmojiName('/billeder/test.tar.png'), 'test_tar');
  assert.equal(suggestEmojiName('a.png'), 'emoji');
  assert.equal(suggestEmojiName('🙂.png'), 'emoji');
  assert.equal(suggestEmojiName('.png'), 'emoji');
  assert.equal(suggestEmojiName('GLAD.png', ['glad', 'GLAD_2']), 'glad_3');
  const long = 'a'.repeat(32);
  assert.equal(suggestEmojiName(long + '.png'), long);
  assert.equal(suggestEmojiName(long + '.png', [long]), 'a'.repeat(30) + '_2');
  assert.equal(normalizeEmojiName(' :Min_Emoji: '), 'min_emoji');
  assert.equal(normalizeEmojiName(' :Vatson-Vinker: '), 'vatson-vinker');
  assert.equal(suggestEmojiName('Vatson-Vinker.png'), 'vatson-vinker');
  assert.equal(suggestEmojiName('Vatson-Vinker.png', ['VATSON-VINKER']), 'vatson-vinker_2');
  assert.equal(suggestEmojiName('Vatson-Faar en idé.png'), 'vatson-faar_en_ide');
  assert.equal(normalizeEmojiName('vatson-' + 'a'.repeat(26)), null);
  assert.equal(normalizeEmojiName('vatson/vinker'), null);
  assert.equal(normalizeEmojiName('navn med mellemrum'), null);
  assert.equal(normalizeEmojiName('x'), null);
});

test('invalid files and names perform no token lookup or upload', async () => {
  let calls = 0;
  const options = { getToken: async () => { calls++; return 'test'; }, fetchImpl: async () => { calls++; } };
  await assert.rejects(uploadPlatformEmoji(new File(['a'], 'fake.svg'), 'emoji', options), /PNG/);
  await assert.rejects(uploadPlatformEmoji(new File([], 'empty.png'), 'emoji', options), /indhold/);
  await assert.rejects(uploadPlatformEmoji(new File([new Uint8Array(EMOJI_MAX_BYTES + 1)], 'big.png'), 'emoji', options), /256 KB/);
  await assert.rejects(uploadPlatformEmoji(new File(['a'], 'valid.png'), 'bad name', options), /2–32/);
  assert.equal(calls, 0);
  assert.equal(validateEmojiFile(new File(['png'], 'smile.PNG')), null);
});

test('explicit save sends edited metadata and unchanged image bytes once', async () => {
  const bytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);
  const image = new File([bytes], 'Original Fil.png', { type: 'image/png' });
  let calls = 0;
  const created = await uploadPlatformEmoji(image, ':Vores_Emoji:', {
    getToken: async () => 'valid-token',
    metadata: { altText: ' Et glad lilla væsen ', category: 'smileys', keywords: ['glad', 'Lilla', 'GLAD'] },
    fetchImpl: async (url, request) => {
      calls++;
      assert.equal(url, '/api/emojis');
      assert.equal(request.method, 'POST');
      assert.equal(request.headers.Authorization, 'Bearer valid-token');
      assert.equal(request.body.get('name'), 'vores_emoji');
      assert.equal(request.body.get('altText'), 'Et glad lilla væsen');
      assert.equal(request.body.get('category'), 'smileys');
      assert.deepEqual(JSON.parse(request.body.get('keywords')), ['glad', 'Lilla']);
      const file = request.body.get('file');
      assert.equal(file.name, 'Original Fil.png');
      assert.deepEqual(new Uint8Array(await file.arrayBuffer()), bytes);
      return new Response(JSON.stringify({ id: 'new-id', name: 'vores_emoji', contentUrl: '/api/emojis/new-id/content?ticket=new', altText: 'Et glad lilla væsen', category: 'smileys', keywords: ['glad', 'Lilla'] }), { status: 200 });
    },
  });
  assert.equal(calls, 1);
  assert.deepEqual(created, { id: 'new-id', name: 'vores_emoji', contentUrl: '/api/emojis/new-id/content?ticket=new', isStandard: false, category: 'smileys', keywords: ['glad', 'Lilla'], altText: 'Et glad lilla væsen' });
});

test('metadata normalization preserves Danish text and rejects excessive metadata before token lookup', async () => {
  assert.deepEqual(normalizeEmojiMetadata({ altText: '  Hånd med bøger  ', category: 'learning', keywords: 'bøger, læring;BØGER\nhånd' }),
    { altText: 'Hånd med bøger', category: 'learning', keywords: ['bøger', 'læring', 'hånd'] });
  assert.equal(normalizeEmojiMetadata({ category: 'not-a-category' }).category, 'custom');
  assert.match(validateEmojiMetadata({ altText: 'a'.repeat(241) }), /240/);
  assert.match(validateEmojiMetadata({ keywords: Array.from({ length: 13 }, (_, i) => String(i)) }), /12/);
  assert.match(validateEmojiMetadata({ keywords: ['a'.repeat(33)] }), /32/);
  let calls = 0;
  await assert.rejects(uploadPlatformEmoji(new File(['png'], 'valid.png'), 'valid', {
    metadata: { altText: 'a'.repeat(241) }, getToken: async () => { calls++; }, fetchImpl: async () => { calls++; },
  }), /240/);
  assert.equal(calls, 0);
});

test('analysis sends only original GIF file to the read-only endpoint and does not save', async () => {
  const bytes = new Uint8Array([0x47, 0x49, 0x46, 0x38, 0x39, 0x61]);
  const image = new File([bytes], 'Original GIF.gif', { type: 'image/gif' });
  const urls = [];
  const result = await analyzePlatformEmoji(image, {
    getToken: async () => 'valid-token', fetchImpl: async (url, request) => {
      urls.push(url);
      assert.equal(request.method, 'POST');
      assert.equal(request.headers.Authorization, 'Bearer valid-token');
      assert.deepEqual(Array.from(request.body.keys()), ['file']);
      assert.equal(request.body.get('file').name, image.name);
      assert.deepEqual(new Uint8Array(await request.body.get('file').arrayBuffer()), bytes);
      return Response.json({ name: 'glad_maskot', altText: ' En glad lilla maskot ', category: 'smileys', keywords: ['glad', 'maskot'] });
    },
  });
  assert.deepEqual(urls, ['/api/emojis/analyze']);
  assert.deepEqual(result, { name: 'glad_maskot', altText: 'En glad lilla maskot', category: 'smileys', keywords: ['glad', 'maskot'] });
});

test('late AI suggestions preserve filename defaults and manually edited fields', () => {
  const draft = { name: 'mit_navn', altText: 'Min egen beskrivelse', category: 'learning', keywordsText: 'mine, søgeord' };
  const suggestion = { name: 'glad', altText: 'AI-beskrivelse', category: 'smileys', keywords: ['fest', 'smil'] };
  assert.deepEqual(mergeEmojiAnalysis(draft, suggestion, new Set(['name', 'altText'])),
    { name: 'mit_navn', altText: 'Min egen beskrivelse', category: 'smileys', keywordsText: 'fest, smil' });
  assert.deepEqual(mergeEmojiAnalysis(draft, suggestion, new Set(['category', 'keywordsText'])),
    { name: 'mit_navn', altText: 'AI-beskrivelse', category: 'learning', keywordsText: 'mine, søgeord' });
  assert.deepEqual(mergeEmojiAnalysis(draft, suggestion, new Set(Object.keys(draft))), draft);
});

test('analysis failure leaves manual save available and stopping analysis discards a late response', async () => {
  const image = new File(['png'], 'smile.png');
  await assert.rejects(analyzePlatformEmoji(image, {
    getToken: async () => 'test', fetchImpl: async () => new Response('', { status: 503 }),
  }), /analyseres.*manuelt/);
  const controller = new AbortController();
  await assert.rejects(analyzePlatformEmoji(image, {
    signal: controller.signal, getToken: async () => 'test', fetchImpl: async () => {
      controller.abort();
      return Response.json({ name: 'late', altText: 'Forsinket resultat', category: 'custom', keywords: [] });
    },
  }), { name: 'AbortError' });
  const created = await uploadPlatformEmoji(image, 'smile', {
    getToken: async () => 'test', metadata: { altText: 'Manuel beskrivelse' },
    fetchImpl: async () => Response.json({ id: 'manual', name: 'smile', contentUrl: '/manual', altText: 'Manuel beskrivelse' }),
  });
  assert.equal(created.altText, 'Manuel beskrivelse');
});

test('duplicate names and expired permissions surface useful errors without retries', async () => {
  const file = new File(['png'], 'smile.png');
  for (const [status, body, message] of [[409, { error: 'name_taken' }, /allerede brugt/], [401, {}, /Log ind igen/], [403, {}, /Kun undervisere/]]) {
    let calls = 0;
    await assert.rejects(uploadPlatformEmoji(file, 'smile', {
      getToken: async () => 'test', fetchImpl: async () => { calls++; return new Response(JSON.stringify(body), { status }); },
    }), message);
    assert.equal(calls, 1);
  }
});

test('abort while acquiring a token cannot start upload networking', async () => {
  const controller = new AbortController();
  let calls = 0;
  await assert.rejects(uploadPlatformEmoji(new File(['png'], 'smile.png'), 'smile', {
    signal: controller.signal,
    getToken: async () => { controller.abort(); return 'test'; },
    fetchImpl: async () => { calls++; },
  }), { name: 'AbortError' });
  assert.equal(calls, 0);
});

test('invalid image or abort during token lookup never starts analysis networking', async () => {
  let calls = 0;
  await assert.rejects(analyzePlatformEmoji(new File(['a'], 'fake.svg'), {
    getToken: async () => { calls++; }, fetchImpl: async () => { calls++; },
  }), /PNG/);
  const controller = new AbortController();
  await assert.rejects(analyzePlatformEmoji(new File(['png'], 'smile.png'), {
    signal: controller.signal, getToken: async () => { controller.abort(); return 'test'; }, fetchImpl: async () => { calls++; },
  }), { name: 'AbortError' });
  assert.equal(calls, 0);
});
