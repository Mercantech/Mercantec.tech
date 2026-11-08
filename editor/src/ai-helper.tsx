import React, { createContext, useContext, useEffect, useId, useRef, useState } from 'react';
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
import { aiBlocksPreview, assertAiTargetCurrent, assistantHistoryText, boundedAiHistory, canReplaceAiSelection, captureAiTarget, type AiHistoryMessage, type AiTarget } from './ai-conversation';
import { EmojiText } from './emoji-context';

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
    history?: AiHistoryMessage[];
  },
  signal?: AbortSignal,
): Promise<AiResponse> {
  const token = await getToken();
  signal?.throwIfAborted();
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
  openOnBlock: ((blockId: string) => void) | null;
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

interface AssistantTurn {
  id: number;
  prompt: string;
  action: AiAction;
  target: AiTarget | null;
  response?: AiResponse;
  state: 'pending' | 'ready' | 'stopped' | 'failed';
  error?: string;
  applied?: boolean;
}

const mascotUrl = '/images/newyear/lup-fireworks-player.png';
const actionNames: Record<AiAction, string> = { generate: 'Ny tekst', rewrite: 'Omskriv', mermaid: 'Diagram' };

export function AiHelperPanel({
  editor, enabled, pageTitle, holdId, pages, members, getToken, open, onOpenChange,
  anchorBlockId = null, anchorText = null, target = null, onCaptureContext,
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
  anchorBlockId?: string | null;
  anchorText?: string | null;
  target?: AiTarget | null;
  onCaptureContext?: () => void;
}) {
  const [action, setAction] = useState<AiAction>('generate');
  const [prompt, setPrompt] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [turns, setTurns] = useState<AssistantTurn[]>([]);
  const [feedback, setFeedback] = useState('');
  const panelId = useId();
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const shellRef = useRef<HTMLElement>(null);
  const launcherRef = useRef<HTMLButtonElement>(null);
  const logRef = useRef<HTMLDivElement>(null);
  const abortRef = useRef<AbortController | null>(null);
  const nextId = useRef(0);
  const mounted = useRef(true);
  const wasOpen = useRef(false);
  const nearBottom = useRef(true);
  const currentTarget = target;

  useEffect(() => {
    if (open) {
      wasOpen.current = true;
      requestAnimationFrame(() => inputRef.current?.focus({ preventScroll: true }));
    } else if (wasOpen.current) {
      requestAnimationFrame(() => launcherRef.current?.focus({ preventScroll: true }));
    }
  }, [open]);
  useEffect(() => {
    if (open && nearBottom.current) requestAnimationFrame(() => {
      if (logRef.current) logRef.current.scrollTop = logRef.current.scrollHeight;
    });
  }, [turns, open]);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; abortRef.current?.abort(); };
  }, []);
  useEffect(() => {
    let frame = 0;
    const controls = new Set<Element>();
    const selector = '.christmas-corner, .newyear-launcher, .christmas-corner .snow-panel, .christmas-corner .christmas-note';
    const fit = () => {
      const shell = shellRef.current;
      if (!shell) return;
      const viewport = window.visualViewport;
      const width = viewport?.width ?? document.documentElement.clientWidth;
      const height = viewport?.height ?? window.innerHeight;
      const top = viewport?.offsetTop ?? 0;
      const left = viewport?.offsetLeft ?? 0;
      const margin = width <= 640 ? 8 : 20;
      const minHeight = Math.min(open ? 100 : 58, height - margin * 2);
      const footer = Math.max(0, parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--lup-footer-clearance')) || 0);
      const right = left + width - margin;
      const assistantWidth = open ? Math.min(360, width - margin * 2) : 58;
      let bottom = top + height - margin - Math.min(footer, Math.max(0, height - margin * 2 - minHeight));
      for (const control of controls) {
        const style = getComputedStyle(control);
        const bounds = control.getBoundingClientRect();
        if (style.display === 'none' || style.visibility === 'hidden' || !bounds.width || !bounds.height) continue;
        if (bounds.bottom <= top || bounds.top >= top + height || bounds.right <= right - assistantWidth || bounds.left >= right) continue;
        bottom = Math.min(bottom, bounds.top - 10);
      }
      // In a short viewport, the panel scrolls internally rather than growing above the screen.
      bottom = Math.max(top + margin + minHeight, bottom);
      const available = Math.max(minHeight, bottom - top - margin);
      shell.style.setProperty('--ai-available-height', available + 'px');
      shell.style.setProperty('--ai-available-width', Math.max(200, width - margin * 2) + 'px');
      shell.style.setProperty('--ai-edge-bottom', Math.max(margin, window.innerHeight - bottom) + 'px');
      shell.style.setProperty('--ai-edge-right', Math.max(margin, document.documentElement.clientWidth - right) + 'px');
      shell.dataset.constrained = available < 380 ? 'true' : 'false';
    };
    const scheduleFit = () => {
      if (frame) return;
      frame = requestAnimationFrame(() => { frame = 0; fit(); });
    };
    const resize = new ResizeObserver(scheduleFit);
    const observeControls = () => {
      for (const control of document.querySelectorAll(selector)) {
        if (controls.has(control)) continue;
        controls.add(control);
        resize.observe(control);
      }
      for (const control of controls) if (!control.isConnected) { resize.unobserve(control); controls.delete(control); }
      scheduleFit();
    };
    const theme = new MutationObserver(scheduleFit);
    theme.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'data-bs-theme', 'style'] });
    const addedControls = new MutationObserver(records => {
      if (records.some(record => [...record.addedNodes, ...record.removedNodes].some(node => node instanceof Element && (node.matches(selector) || node.querySelector(selector))))) observeControls();
    });
    addedControls.observe(document.body, { childList: true, subtree: true });
    observeControls();
    fit();
    window.addEventListener('resize', scheduleFit, { passive: true });
    window.addEventListener('scroll', scheduleFit, { passive: true });
    window.visualViewport?.addEventListener('resize', scheduleFit, { passive: true });
    window.visualViewport?.addEventListener('scroll', scheduleFit, { passive: true });
    return () => {
      if (frame) cancelAnimationFrame(frame);
      resize.disconnect(); theme.disconnect(); addedControls.disconnect();
      window.removeEventListener('resize', scheduleFit);
      window.removeEventListener('scroll', scheduleFit);
      window.visualViewport?.removeEventListener('resize', scheduleFit);
      window.visualViewport?.removeEventListener('scroll', scheduleFit);
    };
  }, [open]);
  const history = () => boundedAiHistory(turns.filter(turn => turn.state === 'ready' && turn.response).flatMap(turn => [
    { role: 'user' as const, text: turn.prompt },
    { role: 'assistant' as const, text: assistantHistoryText(turn.response!.assistantNote, turn.response!.blocks ?? []) },
  ]));
  const submit = async () => {
    if (!enabled || abortRef.current) return;
    const text = prompt.trim();
    if (!text) { setError('Skriv, hvad du gerne vil have hjælp til.'); inputRef.current?.focus(); return; }
    if (text.length > 4000) { setError('Beskeden må højst være 4.000 tegn. Del den gerne op i flere beskeder.'); return; }
    const snapshot = currentTarget ?? captureAiTarget(editor, anchorBlockId ?? undefined);
    const selectionText = selectionTextForAi(action, snapshot?.text ?? anchorText, selectionOrBlockText(editor));
    if (action === 'rewrite' && !selectionText) {
      setError('Vælg en blok med tekst eller en markering, så kan jeg hjælpe med at omskrive den.');
      return;
    }
    const id = ++nextId.current;
    const controller = new AbortController();
    abortRef.current = controller;
    nearBottom.current = true;
    setBusy(true); setError(''); setFeedback(''); setPrompt('');
    setTurns(current => [...current, { id, prompt: text, action, target: snapshot, state: 'pending' }]);
    try {
      const data = await requestEditorAi(getToken, {
        action, prompt: text, selectionText, pageTitle, holdId,
        pages: (pages ?? []).map(page => ({ id: page.id, title: page.title })),
        members: (members ?? []).map(member => ({ id: member.id, displayName: member.displayName })),
        history: history(),
      }, controller.signal);
      controller.signal.throwIfAborted();
      if (!mounted.current) return;
      if (!data.blocks?.length) throw new Error('Jeg fik ikke et brugbart forslag tilbage. Prøv at beskrive ønsket lidt anderledes.');
      setTurns(current => current.map(turn => turn.id === id ? { ...turn, response: data, state: 'ready' } : turn));
    } catch (failure) {
      if (!mounted.current) return;
      const stopped = controller.signal.aborted;
      const message = stopped ? 'Forslaget blev stoppet.' : failure instanceof Error ? failure.message : 'Forslaget kunne ikke hentes. Prøv igen.';
      setTurns(current => current.map(turn => turn.id === id ? { ...turn, state: stopped ? 'stopped' : 'failed', error: message } : turn));
      setPrompt(current => current || text);
    } finally {
      if (abortRef.current === controller) {
        abortRef.current = null;
        if (mounted.current) setBusy(false);
      }
    }
  };

  const apply = (turn: AssistantTurn, mode: 'insert' | 'replace') => {
    if (!enabled || turn.applied || !turn.response?.blocks?.length) return;
    setError('');
    try {
      const snapshot = turn.target;
      if (!snapshot) throw new Error('Vælg først en blok i dokumentet, og bed om et nyt forslag.');
      const blocks = turn.response.blocks;
      assertAiTargetCurrent(editor, snapshot, mode === 'replace' ? 'rewrite' : 'generate');
      if (mode === 'replace' && snapshot.selection) {
        if (!canReplaceAiSelection(blocks)) throw new Error('Forslaget har flere eller særlige blokke. Indsæt det efter teksten, eller bed om én samlet formulering.');
        const mapped = toEditorBlock(blocks[0]) as any;
        const replacement = typeof mapped.content === 'string'
          ? snapshot.selection.leading + mapped.content + snapshot.selection.trailing
          : [snapshot.selection.leading, ...mapped.content, snapshot.selection.trailing].filter(part => part !== '');
        editor.focus();
        editor.transact(() => {
          editor._tiptapEditor.commands.setTextSelection({ from: snapshot.selection!.from, to: snapshot.selection!.to });
          editor.insertInlineContent(replacement);
        });
      } else {
        insertAiBlocks(editor, blocks, mode === 'replace' ? 'rewrite' : turn.action === 'mermaid' ? 'mermaid' : 'generate', snapshot.id);
      }
      setTurns(current => current.map(item => item.id === turn.id ? { ...item, applied: true } : item));
      setFeedback(mode === 'replace' ? 'Teksten er erstattet.' : 'Indsat.');
      onCaptureContext?.();
    } catch (failure) { setError(failure instanceof Error ? failure.message : 'Forslaget kunne ikke indsættes.'); }
  };
  const preset = (mode: AiAction, instruction?: string) => {
    setAction(mode);
    if (instruction) setPrompt(instruction);
    setError('');
    inputRef.current?.focus({ preventScroll: true });
  };
  const handleKeys = (event: React.KeyboardEvent) => {
    event.stopPropagation();
    if (event.nativeEvent.isComposing) return;
    if (event.key === 'Escape') { event.preventDefault(); onOpenChange(false); }
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); void submit(); }
  };
  const ready = turns.some(turn => turn.state === 'ready' && !turn.applied);

  return <aside ref={shellRef} className="notion-ai-assistant" aria-label="AI-assistent" onKeyDown={handleKeys}>
    <button ref={launcherRef} type="button" className="notion-ai-launcher" hidden={open} onMouseDown={event => event.preventDefault()} onClick={() => onOpenChange(true)} aria-label="Åbn AI-assistent" aria-controls={panelId} aria-expanded={open} title={busy ? 'Assistenten arbejder…' : ready ? 'Et forslag er klar' : 'Din skrivehjælper'}>
      <span className="notion-ai-avatar"><img src={mascotUrl} alt="" draggable={false} /></span>
      {(busy || ready) && <span className={`notion-ai-launcher-dot${busy ? ' is-busy' : ''}`} aria-hidden="true" />}
    </button>
    <section id={panelId} className="notion-ai-panel" hidden={!open} role="dialog" aria-label="AI-skriveassistent" aria-modal="false">
      <header className="notion-ai-panel-head">
        <span className="notion-ai-avatar"><img src={mascotUrl} alt="" draggable={false} /></span>
        <div><strong>LUP-assistent</strong><small title={pageTitle}><EmojiText text={pageTitle || 'Dit dokument'} /></small></div>
        <button type="button" className="notion-ai-minimize" onClick={() => onOpenChange(false)} aria-label="Minimér AI-assistent" title="Minimér (Esc)">−</button>
      </header>
      <div className="notion-ai-context">
        {currentTarget?.text && <p className="notion-ai-source"><span>{currentTarget.selection ? 'Markering' : 'Aktuel blok'}</span> {blockTextPreview(currentTarget.text, 100)}</p>}
        <button type="button" onMouseDown={event => event.preventDefault()} onClick={() => { onCaptureContext?.(); inputRef.current?.focus({ preventScroll: true }); }} disabled={!enabled || busy}>Brug aktuel tekst</button>
      </div>
      <div ref={logRef} className="notion-ai-conversation" role="log" aria-label="Samtale med AI-assistenten" aria-live="polite" aria-relevant="additions text" onScroll={() => {
        const log = logRef.current; if (log) nearBottom.current = log.scrollHeight - log.scrollTop - log.clientHeight < 48;
      }}>
        {turns.map(turn => <div className="notion-ai-turn" key={turn.id}>
          <div className="notion-ai-user-message"><span>{actionNames[turn.action]}</span><p>{turn.prompt}</p></div>
          {turn.state === 'pending' && <p className="notion-ai-working" role="status">Jeg arbejder på et forslag…</p>}
          {(turn.state === 'failed' || turn.state === 'stopped') && <p className="notion-ai-error" role={turn.state === 'failed' ? 'alert' : 'status'}>{turn.error}</p>}
          {turn.response && <article className="notion-ai-proposal" aria-label="AI-forslag">
            {turn.response.assistantNote?.trim() && <p className="notion-ai-answer">{turn.response.assistantNote.trim()}</p>}
            <details className="notion-ai-proposal-preview" open><summary>Forslag · {turn.response.blocks!.length} {turn.response.blocks!.length === 1 ? 'blok' : 'blokke'}</summary><pre>{aiBlocksPreview(turn.response.blocks!)}</pre></details>
            <div className="notion-ai-proposal-actions">
              <button type="button" disabled={!enabled || turn.applied} onClick={() => apply(turn, 'insert')}>Indsæt forslag</button>
              {turn.action === 'rewrite' && <button type="button" disabled={!enabled || turn.applied || !!turn.target?.selection && !canReplaceAiSelection(turn.response.blocks!)} onClick={() => apply(turn, 'replace')}>{turn.target?.selection ? 'Erstat markering' : 'Erstat blok'}</button>}
              {turn.applied && <span>Brugt i dokumentet</span>}
            </div>
          </article>}
        </div>)}
      </div>
      <div className="notion-ai-composer">
        <div className="notion-ai-actions" role="group" aria-label="Skrivehjælp">
          <button type="button" aria-pressed={action === 'generate'} onClick={() => preset('generate')} title="Generér ny tekst">Skriv</button>
          <button type="button" aria-pressed={action === 'rewrite'} onClick={() => preset('rewrite')}>Omskriv</button>
          <button type="button" onClick={() => preset('rewrite', 'Forkort teksten, og bevar hovedpointerne.')}>Forkort</button>
          <button type="button" aria-pressed={action === 'mermaid'} onClick={() => preset('mermaid')}>Diagram</button>
        </div>
        <textarea ref={inputRef} rows={3} maxLength={4000} value={prompt} disabled={!enabled} aria-label="Besked til AI-assistenten" placeholder={turns.length ? 'Skriv videre, fx «gør forslaget mere konkret»…' : 'Hvad vil du skrive eller forbedre?'} onChange={event => setPrompt(event.target.value)} />
        <div className="notion-ai-panel-foot">
          <span className="notion-ai-hint">{actionNames[action]} · Ctrl+Enter sender</span>
          {busy ? <button type="button" className="notion-ai-stop" onClick={() => abortRef.current?.abort()}>Stop svar</button>
            : <button type="button" className="notion-ai-submit" disabled={!enabled || !prompt.trim()} onClick={() => void submit()} aria-label="Send besked">Send</button>}
        </div>
        {error && <p className="notion-ai-error" role="alert">{error}</p>}
        {feedback && <p className="notion-ai-note" role="status">{feedback}</p>}
      </div>
    </section>
  </aside>;
}
