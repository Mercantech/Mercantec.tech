import assert from 'node:assert/strict';
import test from 'node:test';
import { expandEmojiShortcodes, filterCustomEmojis } from '../src/custom-emoji.mjs';

const lup = { id: '11111111-1111-1111-1111-111111111111', name: 'lup' };
const wolf = { id: '22222222-2222-2222-2222-222222222222', name: 'wolf' };

test('colon query filters custom emoji names', () => {
  assert.deepEqual(filterCustomEmojis([lup, wolf], ':lu').map((item) => item.name), ['lup']);
  assert.deepEqual(filterCustomEmojis([lup, wolf], '').map((item) => item.name), ['lup', 'wolf']);
  assert.deepEqual(filterCustomEmojis([lup], 'mangler'), []);
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
