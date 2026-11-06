import { JSDOM } from 'jsdom';
import { toEditorBlock } from '../src/ai-blocks.mjs';

const dom = new JSDOM('<!DOCTYPE html><body></body>');
const { window } = dom;
globalThis.window = window;
globalThis.document = window.document;
globalThis.Node = window.Node;
globalThis.Element = window.Element;
globalThis.HTMLElement = window.HTMLElement;
globalThis.DocumentFragment = window.DocumentFragment;
globalThis.DOMParser = window.DOMParser;
globalThis.MutationObserver = window.MutationObserver;
globalThis.getSelection = () => window.getSelection();
Object.defineProperty(globalThis, 'navigator', { value: window.navigator, configurable: true });
if (!globalThis.ResizeObserver) {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

const {
  BlockNoteEditor,
  BlockNoteSchema,
  defaultBlockSpecs,
  defaultInlineContentSpecs,
  createBlockSpec,
  createInlineContentSpec,
} = await import('@blocknote/core');

const inline = () => {
  const el = document.createElement('div');
  return { dom: el, contentDOM: el };
};
const none = () => ({ dom: document.createElement('div') });

const callout = createBlockSpec({
  type: 'callout',
  propSchema: {
    schemaVersion: { default: 1 },
    tone: { default: 'info', values: ['info', 'tip', 'warning', 'important'] },
  },
  content: 'inline',
}, { render: inline })();

const learningGoal = createBlockSpec({
  type: 'learningGoal',
  propSchema: { schemaVersion: { default: 1 } },
  content: 'inline',
}, { render: inline })();

const pageLink = createBlockSpec({
  type: 'pageLink',
  propSchema: { schemaVersion: { default: 1 }, pageId: { default: '' } },
  content: 'none',
}, { render: none })();

const mermaidDiagram = createBlockSpec({
  type: 'mermaidDiagram',
  propSchema: { schemaVersion: { default: 1 }, source: { default: '' } },
  content: 'none',
}, { render: none })();

const slideBreak = createBlockSpec({
  type: 'slideBreak',
  propSchema: { schemaVersion: { default: 1 } },
  content: 'none',
}, { render: none })();

const mention = (type, propSchema) => createInlineContentSpec({
  type,
  propSchema,
  content: 'none',
}, {
  render: () => ({ dom: document.createElement('span') }),
});

const schema = BlockNoteSchema.create({
  blockSpecs: {
    ...defaultBlockSpecs,
    callout,
    learningGoal,
    pageLink,
    mermaidDiagram,
    slideBreak,
  },
  inlineContentSpecs: {
    ...defaultInlineContentSpecs,
    pageMention: mention('pageMention', { pageId: { default: '' } }),
    studentMention: mention('studentMention', { holdId: { default: '' }, membershipId: { default: '' } }),
  },
});

const pageId = '11111111-1111-1111-1111-111111111111';
const holdId = '22222222-2222-2222-2222-222222222222';
const memberId = '33333333-3333-3333-3333-333333333333';

const blocks = [
  { type: 'heading', props: { level: 2 }, content: 'Loops' },
  { type: 'paragraph', content: 'List<string> er en generisk type.' },
  {
    type: 'paragraph',
    content: [
      { type: 'text', text: 'Se ', styles: { bold: true } },
      { type: 'pageMention', props: { pageId } },
      { type: 'text', text: ' og ', styles: {} },
      { type: 'studentMention', props: { holdId, membershipId: memberId } },
      { type: 'link', href: 'https://learn.microsoft.com/dotnet', content: 'docs' },
    ],
  },
  {
    type: 'bulletListItem',
    content: 'Ydre',
    children: [{ type: 'bulletListItem', content: 'Indre' }],
  },
  { type: 'numberedListItem', props: { start: 2 }, content: 'Andet' },
  { type: 'checkListItem', props: { checked: false }, content: 'Øv' },
  { type: 'toggleListItem', content: 'Fold' },
  { type: 'quote', content: 'Citat' },
  { type: 'learningGoal', content: 'Du kan skrive en for-løkke.' },
  { type: 'callout', props: { tone: 'tip' }, content: 'Brug debuggeren.' },
  { type: 'codeBlock', props: { language: 'csharp' }, content: 'List<string> xs = [];' },
  {
    type: 'table',
    content: {
      type: 'tableContent',
      headerRows: 1,
      rows: [
        { cells: ['Emne', 'Eksempel'] },
        { cells: ['Loop', { type: 'tableCell', content: 'for' }] },
      ],
    },
  },
  { type: 'mermaidDiagram', props: { source: 'flowchart TD\nA-->B' } },
  { type: 'pageLink', props: { pageId } },
  { type: 'divider' },
  { type: 'slideBreak' },
].map(toEditorBlock);

const editor = BlockNoteEditor.create({ schema });
editor.insertBlocks(blocks, editor.document[0], 'after');
const types = editor.document.map((block) => block.type);
const expected = ['paragraph', ...blocks.map((block) => block.type)];
const missing = expected.filter((type) => !types.includes(type));
if (missing.length) {
  console.error('missing', missing, 'got', types);
  process.exit(1);
}

const table = editor.document.find((block) => block.type === 'table');
if (table.content.type !== 'tableContent' || table.content.rows.length !== 2) {
  console.error('table', JSON.stringify(table.content));
  process.exit(1);
}
const mentionBlock = editor.document.find((block) => Array.isArray(block.content) && block.content.some((part) => part.type === 'pageMention'));
if (!mentionBlock) {
  console.error('pageMention missing', JSON.stringify(editor.document, null, 2));
  process.exit(1);
}
const nested = editor.document.find((block) => block.type === 'bulletListItem' && block.children?.length);
if (!nested) {
  console.error('children missing');
  process.exit(1);
}
const code = editor.document.find((block) => block.type === 'codeBlock');
const codeText = code.content.map((part) => part.text || '').join('');
if (!codeText.includes('List<string>')) {
  console.error('code', codeText);
  process.exit(1);
}
console.log('inserted', types.join(','));
