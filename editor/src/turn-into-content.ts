/** Copies a block's raw text or markdown into the target format. */

export type TurnIntoBlock = {
  id?: string;
  type: string;
  props?: Record<string, unknown>;
  content?: unknown;
  children?: TurnIntoBlock[];
};

export type TurnIntoUpdate = {
  type: string;
  props?: Record<string, boolean | number | string>;
  content?: string | unknown[];
};

export type TurnIntoOptions = {
  supportedLanguages?: Record<string, { aliases?: string[] }>;
  defaultCodeLanguage?: string;
};

const INLINE_TARGETS = new Set([
  'paragraph',
  'heading',
  'quote',
  'bulletListItem',
  'numberedListItem',
  'checkListItem',
  'toggleListItem',
  'callout',
  'learningGoal',
]);

const STRONG_MERMAID = /^(?:flowchart|sequenceDiagram|classDiagram|stateDiagram(?:-v2)?|erDiagram|gitGraph|mindmap|timeline|quadrantChart|xychart(?:-beta)?|block-beta|sankey(?:-beta)?|architecture(?:-beta)?|requirementDiagram|C4Context|C4Container|C4Component|C4Dynamic|C4Deployment)\b/i;
const WEAK_MERMAID = /^(?:graph|journey|gantt|pie|kanban|zenuml|packet(?:-beta)?)\b/i;

function isRecord(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function normalizeNewlines(text: string): string {
  return text.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
}

/** Whole-block markdown fence. Inner fences or surrounding prose stay untouched. */
export function unwrapFence(text: string): { language: string; body: string } | null {
  const normalized = normalizeNewlines(text).replace(/^\uFEFF/, '').trim();
  if (!normalized.startsWith('```')) return null;
  const lineBreak = normalized.indexOf('\n');
  if (lineBreak < 0) return null;
  const info = normalized.slice(3, lineBreak).trim().toLowerCase();
  if (info.includes('`')) return null;
  if (!normalized.endsWith('```')) return null;
  let body = normalized.slice(lineBreak + 1, -3);
  if (body.endsWith('\n')) body = body.slice(0, -1);
  return { language: info, body };
}

export function looksLikeMermaid(text: string): boolean {
  const lines = normalizeNewlines(text)
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith('%%'));
  const first = lines[0] ?? '';
  if (STRONG_MERMAID.test(first)) return true;
  if (!WEAK_MERMAID.test(first)) return false;
  return lines.some((line) => /-->|---|==>|subgraph\b|participant\b|section\b/.test(line));
}

function resolveLanguage(language: string | undefined, options?: TurnIntoOptions): string {
  const fallback = options?.defaultCodeLanguage || 'csharp';
  const raw = (language || '').trim().toLowerCase();
  if (!raw) return fallback;
  const supported = options?.supportedLanguages;
  const alias = raw === 'mmd' ? 'mermaid' : raw;
  if (!supported) return alias;
  if (alias in supported) return alias;
  const mapped = Object.entries(supported).find(([id, meta]) =>
    id.toLowerCase() === alias || meta.aliases?.some((item) => item.toLowerCase() === alias),
  )?.[0];
  return mapped || alias;
}

function inlineToText(content: unknown, links: 'label' | 'with-href'): string {
  if (typeof content === 'string') return normalizeNewlines(content);
  if (!Array.isArray(content)) return '';
  return content.map((part) => inlinePartToText(part, links)).join('');
}

function inlinePartToText(part: unknown, links: 'label' | 'with-href'): string {
  if (typeof part === 'string') return normalizeNewlines(part);
  if (!isRecord(part)) return '';
  if (part.type === 'link' || (typeof part.href === 'string' && part.content !== undefined)) {
    const label = inlineToText(part.content, links);
    const href = typeof part.href === 'string' ? part.href : '';
    if (links === 'with-href' && href && label && label !== href && !label.includes(href)) return `${label} (${href})`;
    return label || href;
  }
  if (part.type === 'text' || typeof part.text === 'string') return normalizeNewlines(String(part.text ?? ''));
  if (part.type === 'pageMention') {
    const id = isRecord(part.props) ? String(part.props.pageId || '') : '';
    return id ? `side:${id}` : '';
  }
  if (part.type === 'studentMention') {
    const id = isRecord(part.props) ? String(part.props.membershipId || '') : '';
    return id ? `@${id}` : '';
  }
  if (part.content !== undefined) return inlineToText(part.content, links);
  return '';
}

function tableToText(content: unknown, links: 'label' | 'with-href'): string {
  if (!isRecord(content) || !Array.isArray(content.rows)) return '';
  return content.rows.map((row) => {
    if (!isRecord(row) || !Array.isArray(row.cells)) return '';
    return row.cells.map((cell) => {
      if (Array.isArray(cell)) return inlineToText(cell, links);
      if (isRecord(cell)) return inlineToText(cell.content, links);
      return '';
    }).join('\t');
  }).join('\n');
}

function propText(block: TurnIntoBlock): string {
  const props = block.props || {};
  return ['name', 'caption', 'title', 'url']
    .map((key) => props[key])
    .filter((value): value is string => typeof value === 'string' && value.trim().length > 0)
    .join('\n');
}

/** Visible text, including newlines that BlockNote stores inside inline text. */
export function blockRawText(block: TurnIntoBlock, links: 'label' | 'with-href' = 'label'): string {
  if (block.type === 'mermaidDiagram') return normalizeNewlines(String(block.props?.source ?? ''));
  if (block.type === 'codeBlock') return inlineToText(block.content, 'label');
  if (isRecord(block.content) && block.content.type === 'tableContent') return tableToText(block.content, links);
  if (Array.isArray(block.content) || typeof block.content === 'string') return inlineToText(block.content, links);
  return propText(block);
}

function stringForTarget(block: TurnIntoBlock, target: 'code' | 'diagram' | 'inline'): string {
  const links = target === 'code' ? 'with-href' : 'label';
  const text = blockRawText(block, links);
  // A paragraph that is one markdown fence carries the inner source, not the fence markers.
  // An existing code block keeps its characters, including fences the user typed as code.
  if (target === 'diagram' || (target === 'code' && block.type !== 'codeBlock')) {
    const fence = unwrapFence(text);
    if (fence) return fence.body;
  }
  return text;
}

function inlineContentFor(block: TurnIntoBlock): string | unknown[] {
  if (block.type === 'codeBlock' || block.type === 'mermaidDiagram') return stringForTarget(block, 'inline');
  if (Array.isArray(block.content)) return block.content;
  if (typeof block.content === 'string') return normalizeNewlines(block.content);
  if (isRecord(block.content) && block.content.type === 'tableContent') return tableToText(block.content, 'with-href');
  return propText(block);
}

/**
 * Block update that carries the source text into the target.
 * Inline targets keep styles and links. Code keeps the characters and line breaks.
 * Diagrams take the raw source, unwrapping a surrounding markdown fence.
 */
export function buildTurnIntoUpdate(
  block: TurnIntoBlock,
  type: string,
  props?: Record<string, boolean | number | string>,
  options?: TurnIntoOptions,
): TurnIntoUpdate {
  const menu = { ...(props || {}) };

  if (type === 'mermaidDiagram') {
    return { type, props: { source: stringForTarget(block, 'diagram') } };
  }

  if (type === 'codeBlock') {
    if (block.type === 'codeBlock') {
      const language = typeof block.props?.language === 'string' ? block.props.language : undefined;
      return {
        type,
        props: { ...menu, language: resolveLanguage(language, options) },
        content: blockRawText(block),
      };
    }
    if (block.type === 'mermaidDiagram') {
      return {
        type,
        props: { ...menu, language: resolveLanguage('mermaid', options) },
        content: blockRawText(block),
      };
    }
    const plain = blockRawText(block, 'label');
    const fence = unwrapFence(plain);
    const hinted = fence
      ? (fence.language || (looksLikeMermaid(fence.body) ? 'mermaid' : String(menu.language || '')))
      : (looksLikeMermaid(plain) ? 'mermaid' : String(menu.language || ''));
    return {
      type,
      props: { ...menu, language: resolveLanguage(hinted, options) },
      content: stringForTarget(block, 'code'),
    };
  }

  if (INLINE_TARGETS.has(type)) {
    if (type === 'callout' && block.type === 'callout') {
      for (const key of ['tone', 'emojiId', 'emojiName']) {
        const value = block.props?.[key];
        if (typeof value === 'string') menu[key] = value;
      }
    }
    const update: TurnIntoUpdate = { type, content: inlineContentFor(block) };
    if (Object.keys(menu).length) update.props = menu;
    return update;
  }

  const update: TurnIntoUpdate = { type };
  if (Object.keys(menu).length) update.props = menu;
  return update;
}
