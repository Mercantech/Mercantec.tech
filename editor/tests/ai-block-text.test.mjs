import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  anchoredApplyMode,
  blockPlainText,
  blockTextPreview,
  selectionTextForAi,
} from '../src/ai-block-text.ts';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

test('blockPlainText reads inline content, links, tables and mermaid source', () => {
  assert.equal(blockPlainText({
    type: 'paragraph',
    content: [{ type: 'text', text: 'Hej ' }, { type: 'text', text: 'verden' }],
  }), 'Hej verden');
  assert.equal(blockPlainText({
    type: 'paragraph',
    content: [{ type: 'link', content: [{ type: 'text', text: 'Mercantec' }] }],
  }), 'Mercantec');
  assert.equal(blockPlainText({
    type: 'table',
    content: { type: 'tableContent', rows: [{ cells: ['A', 'B'] }, { cells: ['C', 'D'] }] },
  }), 'A | B\nC | D');
  assert.equal(blockPlainText({
    type: 'mermaidDiagram',
    props: { source: 'flowchart TD\n  A-->B' },
  }), 'flowchart TD\n  A-->B');
  assert.equal(blockPlainText({ type: 'paragraph', content: [] }), '');
  assert.equal(blockPlainText(null), '');
});

test('drag-handle launch sends the block text for every action', () => {
  assert.equal(selectionTextForAi('generate', 'loops i C#', ''), 'loops i C#');
  assert.equal(selectionTextForAi('rewrite', 'loops i C#', 'andet'), 'loops i C#');
  assert.equal(selectionTextForAi('mermaid', 'loops i C#', ''), 'loops i C#');
  assert.equal(selectionTextForAi('rewrite', '   ', 'cursor'), undefined);
  assert.equal(selectionTextForAi('rewrite', null, '  markeret  '), 'markeret');
  assert.equal(selectionTextForAi('generate', null, 'cursor tekst'), undefined);
  assert.equal(selectionTextForAi('mermaid', null, 'cursor tekst'), undefined);
});

test('rewrite replaces the block; generate keeps existing text and fills an empty paragraph', () => {
  const text = { content: [{ type: 'text', text: 'Behold mig' }] };
  const empty = { content: [] };
  const diagram = { content: undefined };
  const table = { content: { rows: [{ cells: ['A'] }] } };
  assert.equal(anchoredApplyMode('rewrite', text), 'replace');
  assert.equal(anchoredApplyMode('generate', text), 'insert-after');
  assert.equal(anchoredApplyMode('mermaid', text), 'insert-after');
  assert.equal(anchoredApplyMode('generate', empty), 'fill-empty');
  assert.equal(anchoredApplyMode('mermaid', diagram), 'insert-after');
  assert.equal(anchoredApplyMode('generate', table), 'insert-after');
  assert.equal(blockTextPreview('en '.repeat(80), 20).endsWith('…'), true);
});

test('drag handle menu opens the existing AI helper on the block', () => {
  const turnInto = readFileSync(join(root, 'src/turn-into.tsx'), 'utf8');
  const helper = readFileSync(join(root, 'src/ai-helper.tsx'), 'utf8');
  const editor = readFileSync(join(root, 'src/editor.tsx'), 'utf8');
  const aiAt = turnInto.indexOf('<AiDragHandleItem');
  assert.ok(aiAt > turnInto.indexOf('Omdan til'));
  assert.ok(aiAt < turnInto.indexOf('>Slet<'));
  assert.match(helper, /function AiDragHandleItem/);
  assert.match(helper, />\s*AI\s*</);
  assert.match(helper, /\/api\/ai\/editor/);
  assert.match(helper, /Generér/);
  assert.match(helper, /Omskriv/);
  assert.match(helper, /Diagram/);
  assert.match(helper, /selectionTextForAi/);
  assert.match(helper, /anchoredApplyMode/);
  assert.match(editor, /OpenAiOnBlockProvider/);
  assert.match(editor, /blockPlainText/);
  assert.match(editor, /anchorText=\{aiAnchor\?\.text \?\? null\}/);
});
