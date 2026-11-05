import test from 'node:test';
import assert from 'node:assert/strict';
import { toEditorBlock } from '../src/ai-blocks.mjs';

test('AI-blokke bevarer tabel, mention og children og udelader content på pageLink', () => {
  const pageId = '11111111-1111-1111-1111-111111111111';
  const table = toEditorBlock({
    type: 'table',
    content: {
      type: 'tableContent',
      headerRows: 1,
      rows: [{ cells: ['A', 'B'] }],
    },
  });
  assert.equal(table.content.type, 'tableContent');
  assert.deepEqual(table.content.rows[0].cells, ['A', 'B']);

  const link = toEditorBlock({ type: 'pageLink', props: { pageId }, content: '<p>html</p>' });
  assert.equal(link.type, 'pageLink');
  assert.equal(link.props.pageId, pageId);
  assert.equal('content' in link, false);

  const list = toEditorBlock({
    type: 'bulletListItem',
    content: [
      { type: 'text', text: 'Se ', styles: {} },
      { type: 'pageMention', props: { pageId } },
    ],
    children: [{ type: 'bulletListItem', content: 'Indre' }],
  });
  assert.equal(list.content[1].type, 'pageMention');
  assert.equal(list.children[0].content, 'Indre');

  const diagram = toEditorBlock({ type: 'mermaidDiagram', props: { source: 'flowchart TD\nA-->B' } });
  assert.equal('content' in diagram, false);
  assert.match(diagram.props.source, /flowchart TD/);

  const code = toEditorBlock({ type: 'codeBlock', props: { language: 'csharp' }, content: 'List<string> xs;' });
  assert.equal(code.content, 'List<string> xs;');
});
