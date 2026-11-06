import {
  createCodeBlockSpec,
  wrapInBlockStructure,
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
type CodeRender = NonNullable<typeof baseSpec.implementation.render>;

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

  const language = resolveLanguage(String(block.props.language || notionCodeBlockOptions.defaultLanguage));
  const languages = notionCodeBlockOptions.supportedLanguages!;
  const languageSelect = document.createElement('select');
  languageSelect.className = 'notion-code-language-select';
  languageSelect.setAttribute('aria-label', 'Kodesprog');
  for (const [id, { name }] of Object.entries(languages)) {
    languageSelect.add(new Option(name, id));
  }
  languageSelect.value = language;
  const onLanguageChange = () => {
    if (editor.isEditable) editor.updateBlock(block.id, { props: { language: languageSelect.value } });
  };
  languageSelect.addEventListener('change', onLanguageChange);

  const languageLabel = document.createElement('span');
  languageLabel.className = 'notion-code-language';
  languageLabel.textContent = languages[language].name;
  toolbar.append(languageSelect, languageLabel);

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
    status.textContent = copied ? 'Koden er kopieret.' : 'Koden kunne ikke kopieres.';
    if (resetTimer) clearTimeout(resetTimer);
    resetTimer = setTimeout(() => {
      copy.textContent = 'Kopiér';
      copy.classList.remove('is-copied');
      status.textContent = '';
    }, 1600);
  };
  copy.addEventListener('click', onCopy);
  toolbar.append(copy, status);

  const pre = document.createElement('pre');
  const code = document.createElement('code');
  pre.appendChild(code);

  wrap.appendChild(toolbar);
  wrap.appendChild(pre);

  const view = {
    dom: wrap,
    contentDOM: code,
    destroy: () => {
      destroyed = true;
      languageSelect.removeEventListener('change', onLanguageChange);
      copy.removeEventListener('click', onCopy);
      if (resetTimer) clearTimeout(resetTimer);
    },
  };
  return wrapInBlockStructure(
    view,
    block.type,
    block.props,
    this.propSchema ?? baseSpec.config.propSchema,
    false,
    this.blockContentDOMAttributes,
  );
}

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
