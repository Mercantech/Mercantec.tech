import type { AiAction } from './ai-block-text';

export interface AiTarget {
  id: string;
  text: string;
  fingerprint: string;
  selection?: { from: number; to: number; documentFingerprint: string; leading: string; trailing: string };
}

export interface AiHistoryMessage { role: 'user' | 'assistant'; text: string }

type EditorTargetSource = {
  getBlock: (id: string) => any;
  getTextCursorPosition: () => { block: any };
  getSelectedText?: () => string;
  getSelection?: () => { blocks: any[] } | undefined;
  prosemirrorState?: { selection: { from: number; to: number } };
  document: unknown;
};

export function blockFingerprint(block: unknown): string {
  return JSON.stringify(block);
}

/** Capture before the assistant takes focus; proposals always keep their own target. */
export function captureAiTarget(editor: EditorTargetSource, explicitId?: string): AiTarget | null {
  try {
    let block = explicitId ? editor.getBlock(explicitId) : editor.getTextCursorPosition().block;
    const rawSelected = explicitId ? '' : editor.getSelectedText?.() ?? '';
    const selected = rawSelected.trim();
    const range = editor.prosemirrorState?.selection;
    if (selected && range && range.from !== range.to) {
      const blocks = editor.getSelection?.()?.blocks;
      block = blocks?.[blocks.length - 1] ?? block;
      if (!block) return null;
      return { id: block.id, text: selected, fingerprint: blockFingerprint(block),
        selection: { from: range.from, to: range.to, documentFingerprint: blockFingerprint(editor.document),
          leading: rawSelected.match(/^[ \t\u00a0]*/)?.[0] ?? '', trailing: rawSelected.match(/[ \t\u00a0]*$/)?.[0] ?? '' } };
    }
    return block ? { id: block.id, text: plainAiBlock(block), fingerprint: blockFingerprint(block) } : null;
  } catch { return null; }
}

/** Position-based partial replacements require the whole document to remain unchanged. */
export function assertAiTargetCurrent(editor: Pick<EditorTargetSource, 'getBlock' | 'document'>, target: AiTarget, action: AiAction): void {
  const block = editor.getBlock(target.id);
  if (!block) throw new Error('Udgangspunktet er slettet. Vælg en ny blok eller markering, og bed om et nyt forslag.');
  if (blockFingerprint(block) !== target.fingerprint || action === 'rewrite' && target.selection && blockFingerprint(editor.document) !== target.selection.documentFingerprint)
    throw new Error('Teksten er ændret siden forslaget. Vælg den aktuelle tekst, og bed om et nyt forslag.');
}

export function boundedAiHistory(messages: AiHistoryMessage[]): AiHistoryMessage[] {
  return messages.filter(message => message.text.trim()).slice(-6).map(message => ({ role: message.role, text: message.text.slice(0, 4000) }));
}

export function aiBlocksPreview(blocks: { type: string; props?: Record<string, unknown>; content?: unknown; children?: any[] }[]): string {
  const names: Record<string, string> = { pageLink: 'Link til side', classRoster: 'Elevliste', pollLink: 'Afstemning', divider: 'Skillelinje' };
  return blocks.map(block => [plainAiBlock(block) || names[block.type] || block.type,
    block.children?.length ? aiBlocksPreview(block.children) : ''].filter(Boolean).join('\n')).join('\n\n');
}

export function assistantHistoryText(note: string | null | undefined, blocks: Parameters<typeof aiBlocksPreview>[0]): string {
  return [note?.trim(), 'Forslag:', aiBlocksPreview(blocks)].filter(Boolean).join('\n').slice(0, 4000);
}

export function canReplaceAiSelection(blocks: { type: string; content?: unknown; children?: unknown[] }[]): boolean {
  return blocks.length === 1 && ['paragraph', 'heading', 'bulletListItem', 'numberedListItem'].includes(blocks[0].type)
    && (typeof blocks[0].content === 'string' || Array.isArray(blocks[0].content)) && !blocks[0].children?.length;
}

function plainAiBlock(block: { type: string; props?: Record<string, unknown>; content?: unknown }): string {
  return block.type === 'mermaidDiagram' && typeof block.props?.source === 'string'
    ? block.props.source.trim() : contentText(block.content).trim();
}

function contentText(value: any, depth = 0): string {
  if (value == null || depth > 12) return '';
  if (typeof value === 'string') return value;
  if (Array.isArray(value)) return value.map(part => contentText(part, depth + 1)).join('');
  if (typeof value !== 'object') return '';
  if (Array.isArray(value.rows)) return value.rows.map((row: unknown) => contentText(row, depth + 1)).join('\n');
  if (Array.isArray(value.cells)) return value.cells.map((cell: unknown) => contentText(cell, depth + 1)).join(' | ');
  if (value.content != null) return contentText(value.content, depth + 1);
  return typeof value.text === 'string' ? value.text : '';
}
