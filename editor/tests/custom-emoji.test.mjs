import assert from 'node:assert/strict';
import test from 'node:test';
import { emojiCategories, expandEmojiShortcodes, filterCustomEmojis } from '../src/custom-emoji.mjs';

const lup = { id: '11111111-1111-1111-1111-111111111111', name: 'lup' };
const wolf = { id: '22222222-2222-2222-2222-222222222222', name: 'wolf' };

test('colon query filters custom emoji names', () => {
  assert.deepEqual(filterCustomEmojis([lup, wolf], ':lu').map((item) => item.name), ['lup']);
  assert.deepEqual(filterCustomEmojis([lup, wolf], '').map((item) => item.name), ['lup', 'wolf']);
  assert.deepEqual(filterCustomEmojis([lup], 'mangler'), []);
});

test('search accepts Danish keywords, accents, shortcode punctuation and multiple terms', () => {
  const heartEyes = { id: 'heart', name: 'heart_eyes', isStandard: true, category: 'smileys', keywords: ['forelsket', 'glæde', 'hjerte'] };
  const hand = { id: 'hand', name: 'handshake', isStandard: true, category: 'gestures', keywords: ['håndtryk', 'aftale'] };
  assert.deepEqual(filterCustomEmojis([heartEyes, hand], ':HEART_EYES:'), [heartEyes]);
  assert.deepEqual(filterCustomEmojis([heartEyes, hand], 'hjerte glaede'), [heartEyes]);
  assert.deepEqual(filterCustomEmojis([heartEyes, hand], 'handtryk aftale'), [hand]);
  assert.deepEqual(filterCustomEmojis([heartEyes, hand], 'hjerte aftale'), []);
  assert.deepEqual(filterCustomEmojis([lup, wolf], ' WOLF '), [wolf]);
});

test('category and source follow metadata after rename and preserve uploaded name collisions', () => {
  const renamed = { id: 'standard', name: 'vores_raket', isStandard: true, category: 'technology', keywords: ['rocket', 'raket'] };
  const uploaded = { id: 'uploaded', name: 'smile', isStandard: false, category: 'custom', keywords: [] };
  assert.deepEqual(filterCustomEmojis([renamed, uploaded, lup], '', 'custom'), [uploaded, lup]);
  assert.deepEqual(filterCustomEmojis([renamed, uploaded], 'raket', 'technology'), [renamed]);
  assert.deepEqual(filterCustomEmojis([renamed, uploaded], 'raket', 'custom'), []);
  assert.deepEqual(filterCustomEmojis([renamed, uploaded], '', 'smileys'), []);
  assert.deepEqual(emojiCategories.map(item => item.id), ['all', 'custom', 'smileys', 'gestures', 'symbols', 'learning', 'technology', 'everyday']);
});

test('own emojis use their semantic category and ALT text in search while staying in Egne', () => {
  const uploaded = { id: 'uploaded', name: 'lilla_makker', isStandard: false, category: 'learning', keywords: ['hjælp'], altText: 'Lilla maskot læser i en bog' };
  assert.deepEqual(filterCustomEmojis([uploaded], 'maskot bog', 'learning'), [uploaded]);
  assert.deepEqual(filterCustomEmojis([uploaded], 'hjaelp', 'custom'), [uploaded]);
  assert.deepEqual(filterCustomEmojis([uploaded], '', 'technology'), []);
});

test('a finished :name: becomes an emoji node and keeps surrounding text', () => {
  const input = [{ type: 'text', text: 'Se :Lup: i dag', styles: { bold: true } }];
  const result = expandEmojiShortcodes(input, [lup, wolf]);
  assert.equal(result.changed, true);
  assert.deepEqual(result.content, [
    { type: 'text', text: 'Se ', styles: { bold: true } },
    { type: 'customEmoji', props: { emojiId: lup.id, name: 'lup' } },
    { type: 'text', text: ' i dag', styles: { bold: true } },
  ]);
});

test('unknown names, inline code and untouched content stay as they are', () => {
  const code = [{ type: 'text', text: ':lup:', styles: { code: true } }];
  assert.equal(expandEmojiShortcodes(code, [lup]).changed, false);
  assert.equal(expandEmojiShortcodes(code, [lup]).content, code);
  const unknown = [{ type: 'text', text: 'brug :mangler: her', styles: {} }];
  assert.equal(expandEmojiShortcodes(unknown, [lup]).changed, false);
  const same = [{ type: 'text', text: 'ingen kolon', styles: {} }];
  assert.equal(expandEmojiShortcodes(same, [lup]).content, same);
});
