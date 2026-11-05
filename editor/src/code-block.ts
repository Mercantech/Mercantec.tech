import {
  createCodeBlockSpec,
  createLanguageSelect,
  type CodeBlockOptions,
} from '@blocknote/core';
import { codeBlockOptions as bundledOptions, syntaxHighlighter } from '@blocknote/code-block';

export { syntaxHighlighter };

const bundled = bundledOptions.supportedLanguages;

const preferredOrder = [
  'text', 'csharp', 'javascript', 'typescript', 'jsx', 'tsx', 'html', 'css', 'scss',
  'json', 'python', 'java', 'kotlin', 'sql', 'shellscript', 'markdown', 'xml', 'yaml',
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
  if (normalized in langs) return normalized;
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

type CodeRender = NonNullable<ReturnType<typeof createCodeBlockSpec>['implementation']['render']>;

function createCodeBlockWithCopy(
  block: Parameters<CodeRender>[0],
  editor: Parameters<CodeRender>[1],
) {
  const wrap = document.createElement('div');
  wrap.className = 'notion-code-block';

  const toolbar = document.createElement('div');
  toolbar.className = 'notion-code-toolbar';
  toolbar.contentEditable = 'false';

  const language = resolveLanguage(String(block.props.language || notionCodeBlockOptions.defaultLanguage));
  const languages = notionCodeBlockOptions.supportedLanguages!;
  const languageSelect = createLanguageSelect(block, editor, language, languages);
  toolbar.appendChild(languageSelect.dom);

  const copy = document.createElement('button');
  copy.type = 'button';
  copy.className = 'notion-code-copy';
  copy.textContent = 'Kopiér';
  copy.setAttribute('aria-label', 'Kopiér kode');
  let resetTimer: ReturnType<typeof setTimeout> | undefined;
  const onCopy = async (event: MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    const live = editor.getBlock(block.id);
    const text = plainText(live ?? block);
    try {
      await navigator.clipboard.writeText(text);
      copy.textContent = 'Kopieret';
      copy.classList.add('is-copied');
    } catch {
      copy.textContent = 'Fejl';
      copy.classList.remove('is-copied');
    }
    if (resetTimer) clearTimeout(resetTimer);
    resetTimer = setTimeout(() => {
      copy.textContent = 'Kopiér';
      copy.classList.remove('is-copied');
    }, 1600);
  };
  copy.addEventListener('click', onCopy);
  toolbar.appendChild(copy);

  const pre = document.createElement('pre');
  const code = document.createElement('code');
  pre.appendChild(code);

  wrap.appendChild(toolbar);
  wrap.appendChild(pre);

  return {
    dom: wrap,
    contentDOM: code,
    destroy: () => {
      languageSelect.destroy();
      copy.removeEventListener('click', onCopy);
      if (resetTimer) clearTimeout(resetTimer);
    },
  };
}

const baseSpec = createCodeBlockSpec(notionCodeBlockOptions);

/** Code block with language picker (inkl. C#), Shiki highlight via extension, and Kopiér. */
export function createNotionCodeBlock() {
  return {
    ...baseSpec,
    implementation: {
      ...baseSpec.implementation,
      render: createCodeBlockWithCopy,
    },
  };
}
