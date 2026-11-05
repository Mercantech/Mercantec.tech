import React, { createContext, useContext, useEffect, useRef, useState } from 'react';
import type { BlockNoteEditor } from '@blocknote/core';
import { SideMenuExtension, insertOrUpdateBlockForSlashMenu } from '@blocknote/core/extensions';
import { useBlockNoteEditor, useComponentsContext, useExtensionState } from '@blocknote/react';
import {
  anchoredApplyMode,
  blockTextPreview,
  selectionTextForAi,
  type AiAction,
} from './ai-block-text';
import { toEditorBlock } from './ai-blocks.mjs';

export type { AiAction };

export interface AiBlockDto {
  type: string;
  props?: Record<string, unknown>;
  content?: unknown;
  children?: AiBlockDto[];
}

export interface AiPageRef {
  id: string;
  title: string;
}

export interface AiMemberRef {
  id: string;
  displayName: string;
}

interface AiResponse {
  blocks?: AiBlockDto[];
  assistantNote?: string | null;
  configured?: boolean;
  message?: string;
  error?: string;
}

type TokenFn = () => Promise<string>;

export async function requestEditorAi(
  getToken: TokenFn,
  body: {
    action: AiAction;
    prompt: string;
    selectionText?: string;
    pageTitle?: string;
    holdId?: string;
    pages?: AiPageRef[];
    members?: AiMemberRef[];
  },
  signal?: AbortSignal,
): Promise<AiResponse> {
  const token = await getToken();
  const response = await fetch('/api/ai/editor', {
    method: 'POST',
    headers: {
      Authorization: 'Bearer ' + token,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
    signal,
    cache: 'no-store',
  });
  let data: AiResponse = {};
  try {
    data = await response.json();
  } catch {
    /* ignore */
  }
  if (!response.ok) {
    const message =
      data.message ||
      (response.status === 503
        ? 'OpenAI er ikke konfigureret på serveren.'
        : response.status === 403
          ? 'Kun undervisere kan bruge AI-hjælperen.'
          : data.error === 'selection_required'
            ? 'Markér tekst eller placér markøren i en blok med indhold for at omskrive.'
            : data.error === 'prompt_required'
              ? 'Skriv en instruktion først.'
              : 'AI-forespørgslen fejlede. Prøv igen.');
    throw new Error(message);
  }
  return data;
}

function placeCursor(editor: BlockNoteEditor<any, any, any>, block: { id: string } | undefined) {
  if (!block) return;
  try {
    editor.setTextCursorPosition(block, 'end');
  } catch {
    /* Void blocks have no caret. */
  }
}

export function insertAiBlocks(
  editor: BlockNoteEditor<any, any, any>,
  blocks: AiBlockDto[],
  mode: AiAction,
  anchorBlockId?: string,
): void {
  if (!blocks.length) return;
  editor.focus();
  const mapped = blocks.map(toEditorBlock).filter(Boolean) as any[];
  if (!mapped.length) return;

  if (anchorBlockId) {
    const anchor = editor.getBlock(anchorBlockId);
    if (!anchor) throw new Error('Blokken findes ikke længere. Åbn AI igen fra blokmenuen.');
    const apply = anchoredApplyMode(mode, anchor);
    let last: { id: string } | undefined;
    editor.transact(() => {
      if (apply === 'replace') {
        const replaced = editor.replaceBlocks([anchor], mapped);
        last = replaced.insertedBlocks[replaced.insertedBlocks.length - 1];
        return;
      }
      if (apply === 'fill-empty') {
        const updated = editor.updateBlock(anchor, mapped[0]);
        const rest = mapped.slice(1);
        const inserted = rest.length ? editor.insertBlocks(rest, updated, 'after') : [];
        last = inserted[inserted.length - 1] ?? updated;
        return;
      }
      const inserted = editor.insertBlocks(mapped, anchor, 'after');
      last = inserted[inserted.length - 1];
    });
    placeCursor(editor, last);
    return;
  }

  const cursor = editor.getTextCursorPosition().block;

  if (mode === 'rewrite') {
    const replaced = editor.replaceBlocks([cursor], mapped);
    placeCursor(editor, replaced.insertedBlocks[replaced.insertedBlocks.length - 1]);
    return;
  }

  if (cursor.content !== undefined) {
    insertOrUpdateBlockForSlashMenu(editor, mapped[0]);
    if (mapped.length > 1) {
      const after = editor.getTextCursorPosition().block;
      editor.insertBlocks(mapped.slice(1), after, 'after');
    }
    return;
  }

  const inserted = editor.insertBlocks(mapped, cursor, 'after');
  placeCursor(editor, inserted[inserted.length - 1]);
}

function selectionOrBlockText(editor: BlockNoteEditor<any, any, any>): string {
  try {
    const selected = editor.getSelectedText?.();
    if (typeof selected === 'string' && selected.trim()) return selected.trim();
  } catch {
    /* ignore */
  }
  try {
    const block = editor.getTextCursorPosition().block as { content?: unknown };
    if (typeof block.content === 'string') return block.content;
    if (Array.isArray(block.content)) {
      return block.content
        .map((part: any) => (typeof part?.text === 'string' ? part.text : ''))
        .join('');
    }
  } catch {
    /* ignore */
  }
  return '';
}

const OpenAiOnBlockContext = createContext<((blockId: string) => void) | null>(null);

export function OpenAiOnBlockProvider({
  openOnBlock,
  children,
}: {
  openOnBlock: (blockId: string) => void;
  children: React.ReactNode;
}) {
  return <OpenAiOnBlockContext.Provider value={openOnBlock}>{children}</OpenAiOnBlockContext.Provider>;
}

/** Drag-handle item. Label is the Danish menu name «AI». */
export function AiDragHandleItem() {
  const openOnBlock = useContext(OpenAiOnBlockContext);
  const Components = useComponentsContext();
  const editor = useBlockNoteEditor<any, any, any>();
  const block = useExtensionState(SideMenuExtension, {
    editor,
    selector: (state) => state?.block,
  });
  if (!Components || !openOnBlock || !block) return null;
  return (
    <Components.Generic.Menu.Item className="bn-menu-item" onClick={() => openOnBlock(block.id)}>
      AI
    </Components.Generic.Menu.Item>
  );
}

export function AiHelperPanel({
  editor,
  enabled,
  pageTitle,
  holdId,
  pages,
  members,
  getToken,
  open,
  onOpenChange,
  anchorBlockId = null,
  anchorText = null,
}: {
  editor: BlockNoteEditor<any, any, any>;
  enabled: boolean;
  pageTitle: string;
  holdId?: string;
  pages?: AiPageRef[];
  members?: AiMemberRef[];
  getToken: TokenFn;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Set when the panel was opened from the drag handle. */
  anchorBlockId?: string | null;
  /** Snapshot of that block's text. null means toolbar or slash /ai. */
  anchorText?: string | null;
}) {
  const [action, setAction] = useState<AiAction>('generate');
  const [prompt, setPrompt] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [note, setNote] = useState('');
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    if (open) {
      abortRef.current?.abort();
      abortRef.current = null;
      setBusy(false);
      setError('');
      setNote('');
      queueMicrotask(() => inputRef.current?.focus());
    } else {
      abortRef.current?.abort();
      abortRef.current = null;
      setBusy(false);
    }
  }, [open, anchorBlockId]);

  useEffect(() => () => abortRef.current?.abort(), []);

  if (!open) return null;

  const submit = async () => {
    if (!enabled || busy) return;
    const text = prompt.trim();
    if (!text) {
      setError('Skriv en instruktion først.');
      return;
    }
    if (anchorBlockId && !editor.getBlock(anchorBlockId)) {
      setError('Blokken findes ikke længere. Åbn AI igen fra blokmenuen.');
      return;
    }
    const selectionText = selectionTextForAi(action, anchorText, selectionOrBlockText(editor));
    if (action === 'rewrite' && !selectionText) {
      setError(anchorText !== null
        ? 'Blokken har ingen tekst at omskrive.'
        : 'Markér tekst eller placér markøren i en blok med indhold for at omskrive.');
      return;
    }

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;
    setBusy(true);
    setError('');
    setNote('');
    try {
      const data = await requestEditorAi(
        getToken,
        {
          action,
          prompt: text,
          selectionText,
          pageTitle,
          holdId,
          pages: (pages ?? []).map((page) => ({ id: page.id, title: page.title })),
          members: (members ?? []).map((member) => ({ id: member.id, displayName: member.displayName })),
        },
        controller.signal,
      );
      const blocks = data.blocks ?? [];
      if (!blocks.length) throw new Error('AI returnerede ingen blokke.');
      insertAiBlocks(editor, blocks, action, anchorBlockId ?? undefined);
      setNote(data.assistantNote?.trim() || 'Indsat.');
      setPrompt('');
    } catch (ex) {
      if ((ex as Error)?.name === 'AbortError') return;
      setError(ex instanceof Error ? ex.message : 'AI-forespørgslen fejlede.');
    } finally {
      if (abortRef.current === controller) abortRef.current = null;
      setBusy(false);
    }
  };

  return (
    <div className="notion-ai-panel" role="dialog" aria-label={anchorText !== null ? 'AI-hjælper for blokken' : 'AI-hjælper'}>
      <div className="notion-ai-panel-head">
        <strong>AI-hjælper</strong>
        <button type="button" onClick={() => onOpenChange(false)} disabled={busy} aria-label="Luk">
          Luk
        </button>
      </div>
      {anchorText !== null && (
        <p className="notion-ai-source">
          {blockTextPreview(anchorText) ? <>Udgangspunkt: «{blockTextPreview(anchorText)}»</> : 'Udgangspunkt: tom blok'}
        </p>
      )}
      <div className="notion-ai-actions" role="group" aria-label="Handling">
        <button type="button" className={action === 'generate' ? 'is-active' : undefined} disabled={busy} onClick={() => setAction('generate')}>
          Generér
        </button>
        <button type="button" className={action === 'rewrite' ? 'is-active' : undefined} disabled={busy} onClick={() => setAction('rewrite')}>
          Omskriv
        </button>
        <button type="button" className={action === 'mermaid' ? 'is-active' : undefined} disabled={busy} onClick={() => setAction('mermaid')}>
          Diagram
        </button>
      </div>
      <textarea
        ref={inputRef}
        rows={3}
        value={prompt}
        disabled={busy || !enabled}
        placeholder={
          action === 'mermaid'
            ? 'Beskriv diagrammet, fx «testpyramide med E2E, integration og unit»'
            : action === 'rewrite'
              ? 'Hvordan skal teksten omskrives?'
              : 'Hvad skal AI lave? Fx «læringsmål om loops» eller «callout med tip til debugging»'
        }
        onChange={(event) => setPrompt(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && (event.metaKey || event.ctrlKey)) {
            event.preventDefault();
            void submit();
          }
          if (event.key === 'Escape') {
            event.preventDefault();
            onOpenChange(false);
          }
        }}
      />
      <div className="notion-ai-panel-foot">
        <button type="button" className="notion-ai-submit" disabled={busy || !enabled} onClick={() => void submit()}>
          {busy ? 'Arbejder…' : 'Indsæt'}
        </button>
        <span className="notion-ai-hint">Ctrl+Enter sender</span>
      </div>
      {error && (
        <p className="notion-ai-error" role="alert">
          {error}
        </p>
      )}
      {note && !error && (
        <p className="notion-ai-note" role="status">
          {note}
        </p>
      )}
    </div>
  );
}
