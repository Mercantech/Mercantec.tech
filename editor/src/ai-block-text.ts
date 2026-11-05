export type AiAction = 'generate' | 'rewrite' | 'mermaid';

export interface BlockTextSource {
  type?: string;
  content?: unknown;
  props?: object | null;
}

/** Plain text of one block: inline content, table cells, or Mermaid source. */
export function blockPlainText(block: BlockTextSource | null | undefined): string {
  if (!block) return '';
  if (block.type === 'mermaidDiagram') {
    const props = block.props;
    if (props && typeof props === 'object' && 'source' in props && typeof props.source === 'string') return props.source.trim();
    return '';
  }
  return flattenContent(block.content).trim();
}

/**
 * selectionText for POST /api/ai/editor.
 * anchorText === null means the toolbar or slash menu (rewrite uses the cursor block only).
 * A string means the drag-handle launch: that block is the starting point for every action.
 */
export function selectionTextForAi(
  action: AiAction,
  anchorText: string | null,
  cursorText: string,
): string | undefined {
  if (anchorText !== null) {
    const text = anchorText.trim();
    return text.length ? text : undefined;
  }
  if (action === 'rewrite') {
    const text = cursorText.trim();
    return text.length ? text : undefined;
  }
  return undefined;
}

export type AnchoredApplyMode = 'replace' | 'fill-empty' | 'insert-after';

/** How AI output lands on the drag-handle block. Rewrite replaces it; other actions keep existing text. */
export function anchoredApplyMode(action: AiAction, block: { content?: unknown }): AnchoredApplyMode {
  if (action === 'rewrite') return 'replace';
  if (isEmptyInlineContent(block.content)) return 'fill-empty';
  return 'insert-after';
}

export function blockTextPreview(text: string, max = 140): string {
  const flat = text.replace(/\s+/g, ' ').trim();
  if (flat.length <= max) return flat;
  return flat.slice(0, max - 1).trimEnd() + '…';
}

/** True only for an empty paragraph-like inline block — never for tables or void blocks. */
export function isEmptyInlineContent(content: unknown): boolean {
  if (typeof content === 'string') return content.trim().length === 0;
  if (!Array.isArray(content)) return false;
  if (content.length === 0) return true;
  return content.every((part) => {
    if (typeof part === 'string') return part.trim().length === 0;
    if (!part || typeof part !== 'object') return true;
    const record = part as Record<string, unknown>;
    if (record.content != null) return isEmptyInlineContent(record.content);
    if (typeof record.text === 'string') return record.text.trim().length === 0;
    return false;
  });
}

function flattenContent(value: unknown, depth = 0): string {
  if (depth > 10 || value == null) return '';
  if (typeof value === 'string') return value;
  if (Array.isArray(value)) return value.map((item) => flattenContent(item, depth + 1)).join('');
  if (typeof value !== 'object') return '';
  const record = value as Record<string, unknown>;
  if (Array.isArray(record.rows)) {
    return record.rows
      .map((row) => flattenContent(row, depth + 1))
      .filter((row) => row.trim().length > 0)
      .join('\n');
  }
  if (Array.isArray(record.cells)) {
    return record.cells
      .map((cell) => flattenContent(cell, depth + 1).trim())
      .filter((cell) => cell.length > 0)
      .join(' | ');
  }
  if (record.content != null) return flattenContent(record.content, depth + 1);
  if (typeof record.text === 'string') return record.text;
  return '';
}
