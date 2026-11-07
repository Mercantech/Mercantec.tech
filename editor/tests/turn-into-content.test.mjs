import test from 'node:test';
import assert from 'node:assert/strict';
import { BlockNoteEditor, BlockNoteSchema, defaultBlockSpecs, createBlockSpec } from '@blocknote/core';
import { buildTurnIntoUpdate, looksLikeMermaid, unwrapFence } from '../src/turn-into-content.ts';

const options = {
  defaultCodeLanguage: 'csharp',
  supportedLanguages: {
    csharp: { aliases: ['c#', 'cs'] },
    mermaid: { aliases: ['mmd'] },
    python: { aliases: ['py'] },
    javascript: { aliases: ['js'] },
  },
};

const mermaidSource = 'flowchart TB\n  A --> B';

function textOf(block) {
  if (!block) return '';
  if (block.type === 'mermaidDiagram') return block.props?.source ?? '';
  if (typeof block.content === 'string') return block.content;
  if (Array.isArray(block.content)) {
    return block.content.map((part) => {
      if (typeof part === 'string') return part;
      if (typeof part?.text === 'string') return part.text;
      if (Array.isArray(part?.content)) return part.content.map((item) => item.text || '').join('');
      return '';
    }).join('');
  }
  return '';
}

test('unwrapFence reads a mermaid markdown fence and leaves prose alone', () => {
  const fence = '```mermaid\nflowchart TB\n  A --> B\n```';
  assert.deepEqual(unwrapFence(fence), { language: 'mermaid', body: 'flowchart TB\n  A --> B' });
  assert.equal(unwrapFence('se ```mermaid\nA --> B\n``` senere'), null);
  assert.equal(looksLikeMermaid(mermaidSource), true);
  assert.equal(looksLikeMermaid('graph of the class results'), false);
  assert.equal(looksLikeMermaid('graph TD\n  A --> B'), true);
});

test('buildTurnIntoUpdate carries raw text into code, callout, paragraph and diagram', () => {
  const paragraph = {
    type: 'paragraph',
    content: [{ type: 'text', text: mermaidSource, styles: { bold: true } }],
  };
  const asCode = buildTurnIntoUpdate(paragraph, 'codeBlock', { language: 'csharp' }, options);
  assert.equal(asCode.content, mermaidSource);
  assert.equal(asCode.props.language, 'mermaid');

  const asDiagram = buildTurnIntoUpdate(paragraph, 'mermaidDiagram', undefined, options);
  assert.equal(asDiagram.props.source, mermaidSource);
  assert.equal(asDiagram.content, undefined);

  const fenced = {
    type: 'paragraph',
    content: [{ type: 'text', text: '```mermaid\n' + mermaidSource + '\n```', styles: {} }],
  };
  assert.equal(buildTurnIntoUpdate(fenced, 'mermaidDiagram').props.source, mermaidSource);
  const fencedCode = buildTurnIntoUpdate(fenced, 'codeBlock', { language: 'csharp' }, options);
  assert.equal(fencedCode.content, mermaidSource);
  assert.equal(fencedCode.props.language, 'mermaid');

  const styled = {
    type: 'paragraph',
    content: [
      { type: 'text', text: 'Husk ', styles: {} },
      { type: 'text', text: 'dette', styles: { bold: true } },
      { type: 'link', href: 'https://mercantec.tech', content: [{ type: 'text', text: 'link', styles: {} }] },
    ],
  };
  const callout = buildTurnIntoUpdate(styled, 'callout', { tone: 'info' }, options);
  assert.equal(callout.props.tone, 'info');
  assert.deepEqual(callout.content, styled.content);

  const code = {
    type: 'codeBlock',
    props: { language: 'python' },
    content: [{ type: 'text', text: 'print(1)\nprint(2)', styles: {} }],
  };
  const asParagraph = buildTurnIntoUpdate(code, 'paragraph', undefined, options);
  assert.equal(asParagraph.content, 'print(1)\nprint(2)');
  const asCallout = buildTurnIntoUpdate(code, 'callout', { tone: 'info' }, options);
  assert.equal(asCallout.content, 'print(1)\nprint(2)');
  const stillCode = buildTurnIntoUpdate(code, 'codeBlock', { language: 'csharp' }, options);
  assert.equal(stillCode.content, 'print(1)\nprint(2)');
  assert.equal(stillCode.props.language, 'python');

  const diagram = { type: 'mermaidDiagram', props: { source: mermaidSource } };
  assert.equal(buildTurnIntoUpdate(diagram, 'paragraph').content, mermaidSource);
  const diagramCode = buildTurnIntoUpdate(diagram, 'codeBlock', { language: 'csharp' }, options);
  assert.equal(diagramCode.content, mermaidSource);
  assert.equal(diagramCode.props.language, 'mermaid');
  assert.equal(buildTurnIntoUpdate(diagram, 'mermaidDiagram').props.source, mermaidSource);

  const plain = { type: 'paragraph', content: [{ type: 'text', text: 'Almindelig tekst', styles: {} }] };
  assert.equal(buildTurnIntoUpdate(plain, 'codeBlock', { language: 'csharp' }, options).content, 'Almindelig tekst');
  assert.equal(buildTurnIntoUpdate(plain, 'codeBlock', { language: 'csharp' }, options).props.language, 'csharp');
  assert.equal(buildTurnIntoUpdate(plain, 'learningGoal').content[0].text, 'Almindelig tekst');

  const heading = { type: 'heading', props: { level: 2 }, content: [{ type: 'text', text: 'Titel', styles: {} }] };
  assert.equal(buildTurnIntoUpdate(heading, 'paragraph').content[0].text, 'Titel');
  assert.equal(buildTurnIntoUpdate(heading, 'codeBlock', { language: 'csharp' }, options).content, 'Titel');

  const linked = {
    type: 'paragraph',
    content: [{ type: 'link', href: 'https://mercantec.tech', content: [{ type: 'text', text: 'Mercantec', styles: {} }] }],
  };
  assert.equal(buildTurnIntoUpdate(linked, 'codeBlock', { language: 'csharp' }, options).content, 'Mercantec (https://mercantec.tech)');

  const table = {
    type: 'table',
    content: {
      type: 'tableContent',
      rows: [{ cells: [{ type: 'tableCell', content: [{ type: 'text', text: 'A', styles: {} }] }, { content: [{ type: 'text', text: 'B', styles: {} }] }] }],
    },
  };
  assert.equal(buildTurnIntoUpdate(table, 'paragraph').content, 'A\tB');

  const image = { type: 'image', props: { url: 'https://cdn.example/a.png', caption: 'Figur', name: '' } };
  assert.equal(buildTurnIntoUpdate(image, 'paragraph').content, 'Figur\nhttps://cdn.example/a.png');

  const bareFence = { type: 'paragraph', content: '```\nprint(1)\n```' };
  assert.equal(buildTurnIntoUpdate(bareFence, 'codeBlock', { language: 'csharp' }, options).content, 'print(1)');
  const wrappedCode = {
    type: 'codeBlock',
    props: { language: 'text' },
    content: [{ type: 'text', text: '```mermaid\n' + mermaidSource + '\n```', styles: {} }],
  };
  assert.equal(buildTurnIntoUpdate(wrappedCode, 'mermaidDiagram').props.source, mermaidSource);
  assert.equal(buildTurnIntoUpdate(wrappedCode, 'codeBlock', { language: 'csharp' }, options).content, wrappedCode.content[0].text);

  const warning = { type: 'callout', props: { tone: 'warning', emojiId: 'saved-emoji-id', emojiName: 'vatson-vinker' }, content: [{ type: 'text', text: 'Pas på', styles: {} }] };
  assert.deepEqual(buildTurnIntoUpdate(warning, 'callout', { tone: 'info', emojiId: '', emojiName: '' }).props, warning.props);
  assert.equal(buildTurnIntoUpdate({ type: 'paragraph', content: '' }, 'mermaidDiagram').props.source, '');
});

test('editor update keeps newlines and mermaid source that a type-only update drops', () => {
  const mermaid = createBlockSpec({
    type: 'mermaidDiagram',
    propSchema: { source: { default: 'DEFAULT_SAMPLE' } },
    content: 'none',
  }, {
    render: () => {
      const dom = document.createElement('div');
      return { dom };
    },
  });
  const callout = createBlockSpec({
    type: 'callout',
    propSchema: { tone: { default: 'info' }, emojiId: { default: '' }, emojiName: { default: '' } },
    content: 'inline',
  }, {
    render: () => {
      const dom = document.createElement('div');
      return { dom, contentDOM: dom };
    },
  });
  const schema = BlockNoteSchema.create({
    blockSpecs: { ...defaultBlockSpecs, mermaidDiagram: mermaid(), callout: callout() },
  });
  const editor = BlockNoteEditor.create({ schema });

  editor.replaceBlocks(editor.document, [{ type: 'paragraph', content: mermaidSource }]);
  const paragraph = editor.document[0];
  editor.updateBlock(paragraph.id, { type: 'codeBlock', props: { language: 'csharp' } });
  assert.equal(textOf(editor.document[0]).includes('\n'), false);

  editor.replaceBlocks(editor.document, [{ type: 'paragraph', content: mermaidSource }]);
  const again = editor.document[0];
  editor.updateBlock(again.id, buildTurnIntoUpdate(again, 'codeBlock', { language: 'csharp' }, options));
  assert.equal(textOf(editor.document[0]), mermaidSource);
  assert.equal(editor.document[0].props.language, 'mermaid');

  editor.replaceBlocks(editor.document, [{ type: 'paragraph', content: mermaidSource }]);
  const forDiagram = editor.document[0];
  editor.updateBlock(forDiagram.id, { type: 'mermaidDiagram' });
  assert.equal(editor.document[0].props.source, 'DEFAULT_SAMPLE');

  editor.replaceBlocks(editor.document, [{ type: 'paragraph', content: mermaidSource }]);
  const forDiagramAgain = editor.document[0];
  editor.updateBlock(forDiagramAgain.id, buildTurnIntoUpdate(forDiagramAgain, 'mermaidDiagram'));
  assert.equal(editor.document[0].props.source, mermaidSource);

  editor.replaceBlocks(editor.document, [{ type: 'mermaidDiagram', props: { source: mermaidSource } }]);
  const diagram = editor.document[0];
  editor.updateBlock(diagram.id, { type: 'paragraph' });
  assert.equal(textOf(editor.document[0]), '');
  editor.replaceBlocks(editor.document, [{ type: 'mermaidDiagram', props: { source: mermaidSource } }]);
  const diagramAgain = editor.document[0];
  editor.updateBlock(diagramAgain.id, buildTurnIntoUpdate(diagramAgain, 'paragraph'));
  assert.equal(textOf(editor.document[0]), mermaidSource);

  editor.replaceBlocks(editor.document, [{
    type: 'paragraph',
    content: [{ type: 'text', text: 'fed tekst', styles: { bold: true } }],
  }]);
  const bold = editor.document[0];
  editor.updateBlock(bold.id, buildTurnIntoUpdate(bold, 'callout', { tone: 'info' }, options));
  assert.equal(editor.document[0].type, 'callout');
  assert.equal(editor.document[0].props.tone, 'info');
  assert.equal(textOf(editor.document[0]), 'fed tekst');
  assert.equal(editor.document[0].content[0].styles.bold, true);

  editor.updateBlock(bold.id, { props: { tone: 'warning', emojiId: 'saved-emoji-id', emojiName: 'vatson-vinker' } });
  const configuredCallout = editor.document[0];
  editor.updateBlock(configuredCallout.id, buildTurnIntoUpdate(configuredCallout, 'callout', { tone: 'info' }));
  assert.equal(editor.document[0].props.emojiId, 'saved-emoji-id');
  assert.equal(editor.document[0].props.emojiName, 'vatson-vinker');
  assert.equal(editor.document[0].props.tone, 'warning');
  assert.equal(editor.document[0].content[0].styles.bold, true);

  editor.replaceBlocks(editor.document, [
    { type: 'paragraph', content: 'Første' },
    { type: 'paragraph', content: 'Anden linje\nmed brud' },
  ]);
  const blocks = [...editor.document];
  editor.transact(() => {
    for (const block of blocks) {
      editor.updateBlock(block.id, buildTurnIntoUpdate(block, 'codeBlock', { language: 'csharp' }, options));
    }
  });
  assert.deepEqual(editor.document.map(textOf), ['Første', 'Anden linje\nmed brud']);

  editor.replaceBlocks(editor.document, [{
    type: 'paragraph',
    content: 'Intro',
    children: [{ type: 'paragraph', content: 'Underpunkt' }],
  }]);
  const parent = editor.document[0];
  editor.updateBlock(parent.id, buildTurnIntoUpdate(parent, 'codeBlock', { language: 'csharp' }, options));
  assert.equal(textOf(editor.document[0]), 'Intro');
  assert.equal(textOf(editor.document[0].children[0]), 'Underpunkt');

  editor.replaceBlocks(editor.document, [{ type: 'codeBlock', props: { language: 'python' }, content: 'print(1)\nprint(2)' }]);
  const python = editor.document[0];
  editor.updateBlock(python.id, buildTurnIntoUpdate(python, 'paragraph', undefined, options));
  assert.equal(textOf(editor.document[0]), 'print(1)\nprint(2)');
  assert.equal(editor.document[0].type, 'paragraph');
});
