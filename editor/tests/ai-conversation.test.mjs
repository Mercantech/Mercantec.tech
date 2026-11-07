import test from 'node:test';
import assert from 'node:assert/strict';
import {
  aiBlocksPreview, assertAiTargetCurrent, assistantHistoryText, boundedAiHistory,
  canReplaceAiSelection, captureAiTarget,
} from '../src/ai-conversation.ts';

const paragraph = (id, text) => ({ id, type: 'paragraph', props: {}, content: [{ type: 'text', text }], children: [] });
function editorFixture() {
  const a = paragraph('a', 'Bevar min tekst');
  const b = paragraph('b', 'Anden blok');
  const editor = {
    document: [a, b],
    cursor: a,
    selected: '',
    prosemirrorState: { selection: { from: 5, to: 5 } },
    getBlock(id) { return this.document.find(block => block.id === id); },
    getTextCursorPosition() { return { block: this.cursor }; },
    getSelectedText() { return this.selected; },
    getSelection() { return this.selected ? { blocks: [a] } : undefined; },
  };
  return editor;
}

test('capture keeps the original block or partial text after focus and caret move', () => {
  const editor = editorFixture();
  editor.selected = ' min ';
  editor.prosemirrorState.selection = { from: 10, to: 13 };
  const selection = captureAiTarget(editor);
  editor.cursor = editor.document[1];
  editor.selected = '';
  editor.prosemirrorState.selection = { from: 40, to: 40 };
  assert.equal(selection.id, 'a');
  assert.equal(selection.text, 'min');
  assert.equal(selection.selection.from, 10);
  assert.equal(selection.selection.to, 13);
  assert.equal(selection.selection.leading, ' ');
  assert.equal(selection.selection.trailing, ' ');
  const explicit = captureAiTarget(editor, 'a');
  assert.equal(explicit.text, 'Bevar min tekst');
  assert.equal(explicit.selection, undefined);
  assert.equal(captureAiTarget(editor, 'deleted'), null);
});

test('proposals refuse changed or removed blocks and partial replacements refuse shifted document positions', () => {
  const editor = editorFixture();
  const target = captureAiTarget(editor);
  editor.document[1].content[0].text = 'Redigering et andet sted';
  assert.doesNotThrow(() => assertAiTargetCurrent(editor, target, 'rewrite'));
  editor.selected = 'min';
  editor.prosemirrorState.selection = { from: 10, to: 13 };
  const selected = captureAiTarget(editor);
  editor.document[1].content[0].text += ' mere';
  assert.throws(() => assertAiTargetCurrent(editor, selected, 'rewrite'), /ændret/);
  editor.document[0].content[0].text += ' ændret';
  assert.throws(() => assertAiTargetCurrent(editor, target, 'generate'), /ændret/);
  editor.document.shift();
  assert.throws(() => assertAiTargetCurrent(editor, target, 'rewrite'), /slettet/);
});

test('history sends only the recent bounded messages without changing local conversation', () => {
  const messages = Array.from({ length: 8 }, (_, index) => ({ role: index % 2 ? 'assistant' : 'user', text: String(index) + 'a'.repeat(4500) }));
  const request = boundedAiHistory(messages);
  assert.equal(request.length, 6);
  assert.equal(request[0].text[0], '2');
  assert.equal(request[5].text.length, 4000);
  assert.equal(messages.length, 8);
  assert.equal(messages[7].text.length, 4501);
});

test('assistant preview and follow-up context retain source, table cells and nested content', () => {
  const blocks = [
    { type: 'mermaidDiagram', props: { source: 'flowchart TD\n A-->B' } },
    { type: 'table', content: { rows: [{ cells: ['Navn', 'Tal'] }, { cells: ['A', '1'] }] } },
    { type: 'paragraph', content: 'Forælder', children: [{ type: 'paragraph', content: 'Barn' }] },
    { type: 'paragraph', content: [{ type: 'text', text: 'Se ' }, { type: 'customEmoji', props: { name: 'lilla_makker', emojiId: 'valid-id' } }, { type: 'text', text: ' her' }] },
  ];
  const text = aiBlocksPreview(blocks);
  assert.match(text, /flowchart TD\n A-->B/);
  assert.match(text, /Navn \| Tal\nA \| 1/);
  assert.match(text, /Forælder\nBarn/);
  assert.match(text, /Se :lilla_makker: her/);
  assert.match(assistantHistoryText('Her er forslaget.', blocks), /Her er forslaget\.[\s\S]*flowchart/);
  assert.match(assistantHistoryText(null, blocks), /:lilla_makker:/);
});

test('partial replacement accepts one inline formulation and preserves complex proposals for insertion', () => {
  assert.equal(canReplaceAiSelection([{ type: 'paragraph', content: 'Ny formulering' }]), true);
  assert.equal(canReplaceAiSelection([{ type: 'paragraph', content: ['En'] }, { type: 'paragraph', content: ['To'] }]), false);
  assert.equal(canReplaceAiSelection([{ type: 'table', content: {} }]), false);
  assert.equal(canReplaceAiSelection([{ type: 'paragraph', content: 'Over', children: [{ type: 'paragraph' }] }]), false);
});
