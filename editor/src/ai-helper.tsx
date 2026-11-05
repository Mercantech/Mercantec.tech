import React, { useEffect, useRef, useState } from 'react';
import type { BlockNoteEditor } from '@blocknote/core';
import { insertOrUpdateBlockForSlashMenu } from '@blocknote/core/extensions';

export type AiAction = 'generate' | 'rewrite' | 'mermaid';

export interface AiBlockDto {
  type: string;
  props?: Record<string, unknown>;
  content?: unknown;
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
  body: { action: AiAction; prompt: string; selectionText?: string; pageTitle?: string },
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

function toEditorBlock(block: AiBlockDto): Record<string, unknown> {
  const out: Record<string, unknown> = { type: block.type };
  if (block.props && typeof block.props === 'object') out.props = block.props;
  if (block.type === 'mermaidDiagram') return out;
  if (typeof block.content === 'string') out.content = block.content;
  else if (Array.isArray(block.content)) out.content = block.content;
  else if (block.content != null) out.content = String(block.content);
  else out.content = '';
  return out;
}

export function insertAiBlocks(
  editor: BlockNoteEditor<any, any, any>,
  blocks: AiBlockDto[],
  mode: AiAction,
): void {
  if (!blocks.length) return;
  editor.focus();
  const mapped = blocks.map(toEditorBlock) as any[];
  const cursor = editor.getTextCursorPosition().block;

  if (mode === 'rewrite') {
    const replaced = editor.replaceBlocks([cursor], mapped);
    const last = replaced.insertedBlocks[replaced.insertedBlocks.length - 1];
    if (last) editor.setTextCursorPosition(last, 'end');
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
  const last = inserted[inserted.length - 1];
  if (last) editor.setTextCursorPosition(last, 'end');
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

export function AiHelperPanel({
  editor,
  enabled,
  pageTitle,
  getToken,
  open,
  onOpenChange,
}: {
  editor: BlockNoteEditor<any, any, any>;
  enabled: boolean;
  pageTitle: string;
  getToken: TokenFn;
  open: boolean;
  onOpenChange: (open: boolean) => void;
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
      setError('');
      setNote('');
      queueMicrotask(() => inputRef.current?.focus());
    } else {
      abortRef.current?.abort();
      abortRef.current = null;
      setBusy(false);
    }
  }, [open]);

  useEffect(() => () => abortRef.current?.abort(), []);

  if (!open) return null;

  const submit = async () => {
    if (!enabled || busy) return;
    const text = prompt.trim();
    if (!text) {
      setError('Skriv en instruktion først.');
      return;
    }
    const selectionText = action === 'rewrite' ? selectionOrBlockText(editor) : undefined;
    if (action === 'rewrite' && !selectionText) {
      setError('Markér tekst eller placér markøren i en blok med indhold for at omskrive.');
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
        { action, prompt: text, selectionText, pageTitle },
        controller.signal,
      );
      const blocks = data.blocks ?? [];
      if (!blocks.length) throw new Error('AI returnerede ingen blokke.');
      insertAiBlocks(editor, blocks, action);
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
    <div className="notion-ai-panel" role="dialog" aria-label="AI-hjælper">
      <div className="notion-ai-panel-head">
        <strong>AI-hjælper</strong>
        <button type="button" onClick={() => onOpenChange(false)} disabled={busy} aria-label="Luk">
          Luk
        </button>
      </div>
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
