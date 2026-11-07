import assert from 'node:assert/strict';
import test from 'node:test';
import { captureEmojiQuery, resolveEmojiQuery } from '../src/emoji-query-target.mjs';

function source(text = 'Intro :as slut', cursor = 9) {
  const model = { text, cursor, blockId: 'paragraph-one', originalExists: true, shift: 0 };
  const doc = {
    get content() { return { size: model.text.length }; },
    textBetween(from, to) { return model.text.slice(from, to); },
    resolve() { return { depth: 1, node: depth => ({ attrs: depth === 1 ? { id: model.blockId } : {} }) }; },
  };
  const editor = {
    get prosemirrorState() { return { doc, selection: { from: model.cursor, to: model.cursor } }; },
    getTextCursorPosition: () => ({ block: { id: model.blockId } }),
    getBlock: id => model.originalExists && id === 'paragraph-one' ? { id } : undefined,
  };
  const sides = [];
  const track = (_, position, side) => { sides.push(side); return () => position + model.shift; };
  return { model, editor, track, sides };
}

test('captured colon query uses mapped positions and excludes surrounding text', () => {
  const { model, editor, track, sides } = source();
  const target = captureEmojiQuery(editor, 'as', track);
  assert.deepEqual(resolveEmojiQuery(editor, target), { from: 6, to: 9 });
  assert.deepEqual(sides, ['right', 'left']);
  model.text = 'Nyt ' + model.text;
  model.shift = 4;
  assert.deepEqual(resolveEmojiQuery(editor, target), { from: 10, to: 13 });
  assert.equal(model.text, 'Nyt Intro :as slut');
});

test('a changed query is never removed even when mapped range has the same length', () => {
  const { model, editor, track } = source();
  const target = captureEmojiQuery(editor, 'as', track);
  model.text = 'Intro :ny slut';
  assert.throws(() => resolveEmojiQuery(editor, target), /ændret/);
  assert.equal(model.text, 'Intro :ny slut');
});

test('a deleted block cannot redirect insertion into an adjacent identical query', () => {
  const { model, editor, track } = source();
  const target = captureEmojiQuery(editor, 'as', track);
  model.blockId = 'another-paragraph';
  model.originalExists = false;
  assert.throws(() => resolveEmojiQuery(editor, target), /ændret/);
});

test('invalid or out-of-bounds query locations fail without changing text', () => {
  const { model, editor, track } = source();
  assert.throws(() => captureEmojiQuery(editor, 'wrong', track), /ændret/);
  const target = captureEmojiQuery(editor, 'as', track);
  model.shift = 100;
  assert.throws(() => resolveEmojiQuery(editor, target), /ændret/);
  assert.equal(model.text, 'Intro :as slut');
});

test('query positions use UTF-16 lengths, including emoji search text', () => {
  const { editor, track } = source(':😀', 3);
  const target = captureEmojiQuery(editor, '😀', track);
  assert.deepEqual(resolveEmojiQuery(editor, target), { from: 0, to: 3 });
});
