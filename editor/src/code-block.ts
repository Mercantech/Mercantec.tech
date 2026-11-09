import {
  applyDataAttributes,
  createCodeBlockSpec,
  wrapInBlockStructure,
  type BlockImplementation,
  type CodeBlockOptions,
} from '@blocknote/core';
import { codeBlockOptions as bundledOptions, syntaxHighlighter } from '@blocknote/code-block';
import { createCodeLanguagePicker } from './code-language-picker';
import { curlLanguage, highlightCodeLanguage } from './code-syntax';
import { isLatexLanguage, renderLatex } from './latex-renderer';

export { syntaxHighlighter };

const bundled = { ...bundledOptions.supportedLanguages, curl: curlLanguage };

const preferredOrder = [
  'text', 'csharp', 'javascript', 'typescript', 'jsx', 'tsx', 'html', 'css', 'scss',
  'json', 'python', 'java', 'kotlin', 'sql', 'shellscript', 'curl', 'markdown', 'xml', 'yaml',
  'rust', 'c', 'cpp', 'php', 'ruby', 'swift', 'lua', 'scala', 'haskell', 'mermaid', 'latex',
] as const;

const preferredSet = new Set<string>(preferredOrder);

const orderedLanguages: CodeBlockOptions['supportedLanguages'] = {};
for (const id of preferredOrder) {
  const entry = bundled[id as keyof typeof bundled];
  if (!entry) continue;
  if (id === 'text') {
    orderedLanguages.text = { name: 'Almindelig tekst', aliases: entry.aliases };
  } else if (id === 'shellscript') {
    orderedLanguages.shellscript = { name: 'Shell', aliases: entry.aliases };
  } else if (id === 'latex') {
    orderedLanguages.latex = { name: 'LaTeX', aliases: ['latex', 'tex'] };
  } else {
    orderedLanguages[id] = entry;
  }
}
for (const [id, entry] of Object.entries(bundled)) {
  if (!preferredSet.has(id)) orderedLanguages[id] = entry;
}

/** C# first among real languages — Mercantec er en .NET-skole. */
export const notionCodeBlockOptions = {
  indentLineWithTab: true,
  defaultLanguage: 'csharp',
  supportedLanguages: orderedLanguages,
} satisfies CodeBlockOptions;

function resolveLanguage(language: string): string {
  const langs = notionCodeBlockOptions.supportedLanguages!;
  const normalized = language.trim().toLowerCase();
  if (Object.hasOwn(langs, normalized)) return normalized;
  const mapped = Object.entries(langs).find(([id, meta]) =>
    id.toLowerCase() === normalized
    || meta.aliases?.some((alias) => alias.toLowerCase() === normalized),
  )?.[0];
  return mapped ?? notionCodeBlockOptions.defaultLanguage!;
}

function plainText(block: { content?: unknown }): string {
  const content = block.content;
  if (typeof content === 'string') return content;
  if (Array.isArray(content)) {
    return content
      .map((part) => {
        if (typeof part === 'string') return part;
        if (part && typeof part === 'object' && 'text' in part && typeof (part as { text: unknown }).text === 'string') {
          return (part as { text: string }).text;
        }
        return '';
      })
      .join('');
  }
  return '';
}

const baseSpec = createCodeBlockSpec(notionCodeBlockOptions);
const codeBlockProps = {
  ...baseSpec.config.propSchema,
  showLineNumbers: { default: false },
};
type CodeRender = NonNullable<BlockImplementation<'codeBlock', typeof codeBlockProps, 'plain'>['render']>;

function createCodeBlockWithCopy(
  this: ThisParameterType<CodeRender>,
  block: Parameters<CodeRender>[0],
  editor: Parameters<CodeRender>[1],
) {
  const wrap = document.createElement('div');
  wrap.className = 'notion-code-block';

  const toolbar = document.createElement('div');
  toolbar.className = 'notion-code-toolbar';
  toolbar.contentEditable = 'false';
  const onToolbarKey = (event: KeyboardEvent) => {
    if (event.key === 'Enter' || event.key === ' ') event.stopPropagation();
  };
  toolbar.addEventListener('keydown', onToolbarKey);

  const languages = notionCodeBlockOptions.supportedLanguages!;
  const languagePicker = createCodeLanguagePicker(languages, () => editor.isEditable, language => {
    editor.updateBlock(block.id, { props: { language } });
  });

  const languageLabel = document.createElement('span');
  languageLabel.className = 'notion-code-language';
  const lineToggle = document.createElement('button');
  lineToggle.type = 'button';
  lineToggle.className = 'notion-code-line-toggle';
  lineToggle.textContent = '#';
  lineToggle.title = 'Linjetal';
  lineToggle.setAttribute('aria-label', 'Linjetal');
  const onLineToggle = () => {
    if (!editor.isEditable) return;
    editor.updateBlock(block.id, { props: { showLineNumbers: !showLineNumbers } });
  };
  lineToggle.addEventListener('click', onLineToggle);
  toolbar.append(languagePicker.dom, languageLabel, lineToggle);

  const latexToggle = document.createElement('button');
  latexToggle.type = 'button';
  latexToggle.className = 'notion-latex-edit-toggle';
  latexToggle.hidden = true;
  const onLatexToggle = () => {
    if (!editor.isEditable || !isLatexLanguage(language)) return;
    sourceOpen = scroll.hidden;
    cursorInSource = false;
    updateSourceVisibility();
    if (sourceOpen) {
      editor.setTextCursorPosition(block.id, 'end');
      editor.focus();
    } else {
      latexToggle.focus();
    }
  };
  latexToggle.addEventListener('click', onLatexToggle);
  toolbar.append(latexToggle);

  const copy = document.createElement('button');
  copy.type = 'button';
  copy.className = 'notion-code-copy';
  copy.textContent = 'Kopiér';
  copy.setAttribute('aria-label', 'Kopiér kode');
  const status = document.createElement('span');
  status.className = 'notion-visually-hidden';
  status.setAttribute('role', 'status');
  let resetTimer: ReturnType<typeof setTimeout> | undefined;
  let destroyed = false;
  const onCopy = async (event: MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    const live = editor.getBlock(block.id);
    const text = plainText(live ?? block);
    let copied = false;
    try {
      await navigator.clipboard.writeText(text);
      copied = true;
    } catch { /* Fejlen vises ved knappen. */ }
    if (destroyed) return;

    copy.textContent = copied ? 'Kopieret' : 'Fejl';
    copy.classList.toggle('is-copied', copied);
    status.textContent = isLatexLanguage(language)
      ? (copied ? 'LaTeX-kilden er kopieret.' : 'LaTeX-kilden kunne ikke kopieres.')
      : (copied ? 'Koden er kopieret.' : 'Koden kunne ikke kopieres.');
    if (resetTimer) clearTimeout(resetTimer);
    resetTimer = setTimeout(() => {
      copy.textContent = 'Kopiér';
      copy.classList.remove('is-copied');
      status.textContent = '';
    }, 1600);
  };
  copy.addEventListener('click', onCopy);
  toolbar.append(copy, status);

  const scroll = document.createElement('div');
  scroll.className = 'notion-code-scroll';
  const lineNumbers = document.createElement('span');
  lineNumbers.className = 'notion-code-line-numbers';
  lineNumbers.contentEditable = 'false';
  lineNumbers.setAttribute('aria-hidden', 'true');
  const pre = document.createElement('pre');
  const code = document.createElement('code');
  pre.appendChild(code);
  scroll.append(lineNumbers, pre);
  const formula = document.createElement('div');
  formula.className = 'notion-latex-preview';
  formula.contentEditable = 'false';
  formula.hidden = true;
  wrap.append(toolbar, formula, scroll);

  let language = String(block.props.language || notionCodeBlockOptions.defaultLanguage);
  let showLineNumbers = block.props.showLineNumbers === true;
  let renderedLineCount = 0;
  let lastFormulaSource: string | undefined;
  let sourceOpen = false;
  let cursorInSource = false;
  let stopTrackingCursor: (() => void) | undefined;
  function updateSourceVisibility() {
    const latex = isLatexLanguage(language);
    const ready = formula.dataset.latexState === 'ready';
    scroll.hidden = latex && ready && !(editor.isEditable && (sourceOpen || cursorInSource));
    formula.hidden = !latex || !lastFormulaSource?.trim();
    latexToggle.hidden = !latex || !ready || !editor.isEditable;
    latexToggle.textContent = scroll.hidden ? 'Redigér formel' : 'Vis kun formel';
    latexToggle.setAttribute('aria-expanded', String(!scroll.hidden));
    lineToggle.hidden = scroll.hidden;
  }
  function trackCursor() {
    const anchor = editor.prosemirrorView?.state.selection.$anchor;
    const next = Boolean(anchor && anchor.depth > 0 && anchor.parent.type.name === block.type &&
      anchor.node(anchor.depth - 1).attrs.id === block.id);
    if (next === cursorInSource) return;
    cursorInSource = next;
    updateSourceVisibility();
  }
  function updateFormula(text: string) {
    if (!isLatexLanguage(language) || lastFormulaSource === text) return;
    lastFormulaSource = text;
    void renderLatex(formula, text).then(() => {
      if (!destroyed) updateSourceVisibility();
    });
    updateSourceVisibility();
  }
  function updateControls() {
    const id = resolveLanguage(language);
    const latex = isLatexLanguage(language);
    wrap.classList.toggle('is-latex', latex);
    languagePicker.setLanguage(id);
    languageLabel.textContent = languages[id].name;
    copy.setAttribute('aria-label', latex ? 'Kopiér LaTeX-kilde' : 'Kopiér kode');
    lineToggle.setAttribute('aria-pressed', String(showLineNumbers));
    lineNumbers.hidden = !showLineNumbers;
    if (latex && editor.isEditable && !stopTrackingCursor) {
      const stopSelection = editor.onSelectionChange(trackCursor);
      const stopFocus = editor.onFocusChange((_editor, { focused }) => {
        if (focused) trackCursor();
      });
      stopTrackingCursor = () => { stopSelection(); stopFocus(); };
    } else if (!latex) {
      stopTrackingCursor?.();
      stopTrackingCursor = undefined;
      lastFormulaSource = undefined;
      sourceOpen = false;
      cursorInSource = false;
    }
    updateSourceVisibility();
  }
  function updateLineNumbers(text: string) {
    if (!showLineNumbers) return;
    const count = 1 + (text.match(/\r\n|[\r\n]/g)?.length ?? 0);
    if (count === renderedLineCount) return;
    renderedLineCount = count;
    lineNumbers.textContent = Array.from({ length: count }, (_, index) => index + 1).join('\n');
  }
  updateControls();
  const initialText = plainText(block);
  updateLineNumbers(initialText);
  updateFormula(initialText);

  const view = {
    dom: wrap,
    contentDOM: code,
    destroy: () => {
      destroyed = true;
      languagePicker.destroy();
      stopTrackingCursor?.();
      toolbar.removeEventListener('keydown', onToolbarKey);
      lineToggle.removeEventListener('click', onLineToggle);
      latexToggle.removeEventListener('click', onLatexToggle);
      copy.removeEventListener('click', onCopy);
      if (resetTimer) clearTimeout(resetTimer);
    },
  };
  const wrapped = wrapInBlockStructure(
    view,
    block.type,
    block.props,
    codeBlockProps,
    false,
    this.blockContentDOMAttributes,
  );
  return {
    ...wrapped,
    ignoreMutation(mutation) {
      return mutation.type !== 'selection' && !code.contains(mutation.target);
    },
    update(node) {
      if (node.type.name !== block.type) return false;
      const nextLanguage = String(node.attrs.language || notionCodeBlockOptions.defaultLanguage);
      const nextLineNumbers = node.attrs.showLineNumbers === true;
      if (nextLanguage !== language || nextLineNumbers !== showLineNumbers) {
        language = nextLanguage;
        showLineNumbers = nextLineNumbers;
        updateControls();
        applyDataAttributes(wrapped.dom, { language, showLineNumbers }, codeBlockProps);
      }
      if (showLineNumbers || isLatexLanguage(language)) {
        const text = node.textBetween(0, node.content.size, '\n', '\n');
        updateLineNumbers(text);
        updateFormula(text);
      }
      return true;
    },
  } satisfies ReturnType<CodeRender>;
}

/** Code block with language picker (inkl. C#), Shiki highlight via extension, and Kopiér. */
export function createNotionCodeBlock() {
  return {
    ...baseSpec,
    config: { ...baseSpec.config, propSchema: codeBlockProps },
    implementation: {
      ...baseSpec.implementation,
      meta: {
        ...baseSpec.implementation.meta,
        highlight: (block: { props: Record<string, unknown> }) =>
          highlightCodeLanguage(String(block.props.language ?? notionCodeBlockOptions.defaultLanguage)),
      },
      render: createCodeBlockWithCopy,
    },
  };
}
