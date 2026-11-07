import assert from 'node:assert/strict';
import test from 'node:test';
import { EMOJI_MAX_BYTES, normalizeEmojiName, suggestEmojiName, uploadPlatformEmoji, validateEmojiFile } from '../src/emoji-upload.mjs';

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

test('explicit save sends the edited name and unchanged image bytes once', async () => {
  const bytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47]);
  const image = new File([bytes], 'Original Fil.png', { type: 'image/png' });
  let calls = 0;
  const created = await uploadPlatformEmoji(image, ':Vores_Emoji:', {
    getToken: async () => 'valid-token',
    fetchImpl: async (url, request) => {
      calls++;
      assert.equal(url, '/api/emojis');
      assert.equal(request.method, 'POST');
      assert.equal(request.headers.Authorization, 'Bearer valid-token');
      assert.equal(request.body.get('name'), 'vores_emoji');
      const file = request.body.get('file');
      assert.equal(file.name, 'Original Fil.png');
      assert.deepEqual(new Uint8Array(await file.arrayBuffer()), bytes);
      return new Response(JSON.stringify({ id: 'new-id', name: 'vores_emoji', contentUrl: '/api/emojis/new-id/content?ticket=new' }), { status: 200 });
    },
  });
  assert.equal(calls, 1);
  assert.deepEqual(created, { id: 'new-id', name: 'vores_emoji', contentUrl: '/api/emojis/new-id/content?ticket=new', isStandard: false, category: 'custom', keywords: [] });
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
