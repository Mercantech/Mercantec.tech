import test from 'node:test';
import assert from 'node:assert/strict';
import { Schema } from 'prosemirror-model';
import { EditorState } from 'prosemirror-state';
import { canonicalAiJson, aiReceiptKey, aiHasReceipt, findAiBlockNodes, aiSelectionSliceMatches, buildAiReviewTransaction } from '../src/ai-review-core.mjs';

const schema = new Schema({
  nodes: {
    doc: { content: 'blockGroup' }, blockGroup: { content: 'blockContainer+' },
    blockContainer: { attrs: { id: { default: null } }, content: '(paragraph | code | media) blockGroup?' },
    paragraph: { content: 'inline*', group: 'block' }, code: { content: 'text*', group: 'block', code: true },
    media: { group: 'block', atom: true, attrs: { reference: {} } }, text: { group: 'inline' },
  }, marks: { bold: {}, italic: {} },
});
const block = (id, text, marks) => schema.node('blockContainer', { id }, schema.node('paragraph', null, text ? schema.text(text, marks) : undefined));
const state = (...blocks) => EditorState.create({ schema, doc: schema.node('doc', null, schema.node('blockGroup', null, blocks)) });
const change = (from, to, nodes, extra = {}) => ({ from, to, nodes, selection: false, ...extra });

test('canonical target equality ignores key order and keeps nested marks and references', () => {
  assert.equal(canonicalAiJson({ b: ['æ', { a: 1 }], a: 2 }), canonicalAiJson({ a: 2, b: ['æ', { a: 1 }] }));
  assert.notEqual(canonicalAiJson({ text: 'a', styles: { bold: true } }), canonicalAiJson({ text: 'a', styles: {} }));
  assert.equal(aiReceiptKey({ id: 'stable', version: 3 }), 'stable');
  assert.equal(canonicalAiJson([undefined, { omitted: undefined }]), canonicalAiJson([null, {}]));
});

test('a reviewed operation cannot be replayed as a later version, including legacy receipt keys', () => {
  const receipts = new Map([['stable', { version: 1 }], ['legacy:2', { version: 2 }]]);
  assert.equal(aiHasReceipt(receipts, { id: 'stable', version: 3 }), true);
  assert.equal(aiHasReceipt(receipts, { id: 'legacy', version: 4 }), true);
  assert.equal(aiHasReceipt(receipts, { id: 'other', version: 1 }), false);
});

test('block lookup tracks stable IDs after unrelated insertions', () => {
  const original = state(block('a', 'Før'), block('b', 'Mål'), block('c', 'Efter'));
  const moved = original.apply(original.tr.insert(1, block('new', 'Tilføjet')));
  const target = findAiBlockNodes(moved.doc, ['b'])[0];
  assert.equal(target.node.textContent, 'Mål');
  assert.ok(target.from > findAiBlockNodes(original.doc, ['b'])[0].from);
});

test('selection fingerprint verifies only selected marked text', () => {
  const original = state(block('a', 'FØR mål EFTER', [schema.marks.bold.create()]), block('b', 'Andet'));
  const from = 7, to = 10;
  const captured = original.doc.slice(from, to).toJSON();
  const unrelated = original.apply(original.tr.insertText('!', original.doc.content.size - 3));
  assert.equal(aiSelectionSliceMatches(unrelated.doc, from, to, captured), true);
  const changed = original.apply(original.tr.insertText('ny', from, to));
  assert.equal(aiSelectionSliceMatches(changed.doc, from, to, captured), false);
  const formatted = original.apply(original.tr.removeMark(from, to, schema.marks.bold));
  assert.equal(aiSelectionSliceMatches(formatted.doc, from, to, captured), false);
});

test('partial single-block replacement preserves outside text and marks', () => {
  const original = state(block('a', 'FØR mål EFTER', [schema.marks.bold.create()]));
  const tr = buildAiReviewTransaction(original, [change(7, 10, [block('replacement', 'NY')], { selection: true })]);
  assert.equal(tr.doc.textContent, 'FØR NY EFTER');
  const paragraph = tr.doc.firstChild.firstChild.firstChild;
  assert.equal(paragraph.firstChild.text, 'FØR ');
  assert.equal(paragraph.firstChild.marks[0].type.name, 'bold');
  assert.equal(paragraph.lastChild.text, ' EFTER');
  assert.equal(paragraph.lastChild.marks[0].type.name, 'bold');
  assert.equal(original.doc.textContent, 'FØR mål EFTER');
});

test('partial multi-block replacement preserves both boundary fragments and unrelated media', () => {
  const media = schema.node('blockContainer', { id: 'drawing' }, schema.node('media', { reference: 'drawing-123' }));
  const original = state(block('a', 'FØR gammel', [schema.marks.bold.create()]), block('b', 'midte'), block('c', 'slut EFTER', [schema.marks.italic.create()]), media);
  const nodes = findAiBlockNodes(original.doc, ['a', 'c']);
  const from = nodes[0].from + 2 + 4, to = nodes[1].from + 2 + 4;
  const tr = buildAiReviewTransaction(original, [change(from, to, [block('n1', 'NY FØRSTE'), block('n2', 'NY SIDSTE')], { selection: true })]);
  assert.equal(tr.doc.textContent, 'FØR NY FØRSTENY SIDSTE EFTER');
  const first = tr.doc.firstChild.firstChild.firstChild;
  assert.equal(first.firstChild.text, 'FØR '); assert.equal(first.firstChild.marks[0].type.name, 'bold');
  const lastTextBlock = tr.doc.firstChild.child(tr.doc.firstChild.childCount - 2).firstChild;
  assert.equal(lastTextBlock.lastChild.text, ' EFTER'); assert.equal(lastTextBlock.lastChild.marks[0].type.name, 'italic');
  assert.equal(findAiBlockNodes(tr.doc, ['drawing'])[0].node.firstChild.attrs.reference, 'drawing-123');
});

test('nested multi-block selection preserves unselected list children and marked edge text', () => {
  const nested = schema.node('blockContainer', { id: 'parent' }, [schema.node('paragraph', null, schema.text('Forælder før mål', [schema.marks.bold.create()])), schema.node('blockGroup', null, [block('child', 'Barn i målet'), block('keep-child', 'Bevar dette barn')])]);
  const original = state(nested, block('end', 'slut EFTER', [schema.marks.italic.create()]), block('outside', 'Helt udenfor'));
  const start = findAiBlockNodes(original.doc, ['child'])[0];
  const end = findAiBlockNodes(original.doc, ['end'])[0];
  const tr = buildAiReviewTransaction(original, [change(start.from + 2 + 5, end.from + 2 + 4, [block('new', 'NY')], { selection: true })]);
  assert.match(tr.doc.textContent, /^Forælder før målBarn NY EFTERHelt udenfor$/);
  assert.equal(findAiBlockNodes(tr.doc, ['parent'])[0].node.firstChild.firstChild.marks[0].type.name, 'bold');
  assert.equal(findAiBlockNodes(tr.doc, ['outside'])[0].node.textContent, 'Helt udenfor');
});

test('atomic batch validates overlap before applying any step', () => {
  const original = state(block('a', 'Mål'), block('b', 'Uændret'));
  assert.throws(() => buildAiReviewTransaction(original, [change(1, 8, []), change(5, 8, [])]), /overlapper/);
  assert.equal(original.doc.textContent, 'MålUændret');
});

test('continuation inserts sharing an anchor have stable document order', () => {
  const original = state(block('a', 'Anker'));
  const pos = findAiBlockNodes(original.doc, ['a'])[0].to;
  const tr = buildAiReviewTransaction(original, [change(pos, pos, [block('b', 'Del 1')], { order: 1 }), change(pos, pos, [block('c', 'Del 2')], { order: 2 }), change(pos, pos, [block('d', 'Del 3')], { order: 3 })]);
  assert.equal(tr.doc.textContent, 'AnkerDel 1Del 2Del 3');
});

test('large batches and long code remain intact', () => {
  const original = state(block('a', 'Anker'));
  const pos = findAiBlockNodes(original.doc, ['a'])[0].to;
  const codeText = 'SELECT æble FROM tabel;\n'.repeat(1000);
  const longCode = schema.node('blockContainer', { id: 'code' }, schema.node('code', null, schema.text(codeText)));
  const nodes = [...Array.from({ length: 45 }, (_, index) => block('b' + index, 'Afsnit ' + index)), longCode];
  const tr = buildAiReviewTransaction(original, [change(pos, pos, nodes)]);
  assert.equal(tr.doc.firstChild.childCount, 47);
  assert.equal(findAiBlockNodes(tr.doc, ['code'])[0].node.textContent, codeText);
});
