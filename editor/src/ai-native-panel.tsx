import React, { useEffect, useId, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { createNativeAiApi } from './ai-native-api';
import { NativeAiReview } from './ai-native-review';
import { aiNativeError } from './ai-native-errors';
import { aiRunWorking, type AiNativeTarget, type AiRun, type AiRunSummary } from './ai-native-types';

type Props = { review: NativeAiReview; pageId: string; pageTitle: string; enabled: boolean; open: boolean; panelId?: string; target: AiNativeTarget | null; onClose: () => void; onReveal: () => void; onCapture: () => void; getToken: () => Promise<string> };
const statusLabel: Record<string, string> = { queued: 'I kø', gathering: 'Læser relevante kilder', generating: 'Skriver forslag', ready: 'Klar til gennemgang', needs_continuation: 'Klar til at fortsætte', cancelled: 'Stoppet', failed: 'Opgaven blev afbrudt' };
const drawerWidthKey = 'lup.ai.drawer.width.v1';
const storedDrawerWidth = () => {
  try { const value = JSON.parse(window.localStorage.getItem(drawerWidthKey) || '400'); return typeof value === 'number' && Number.isFinite(value) ? Math.max(320, Math.min(900, value)) : 400; }
  catch { return 400; }
};

export function NativeAiPanel({ review, pageId, pageTitle, enabled, open, panelId: suppliedPanelId, target, onClose, onReveal, onCapture, getToken }: Props) {
  useSyncExternalStore(review.subscribe, review.snapshot);
  const tokenRef = useRef(getToken); tokenRef.current = getToken;
  const api = useMemo(() => createNativeAiApi(() => tokenRef.current()), []);
  const [runs, setRuns] = useState<AiRunSummary[]>([]);
  const [run, setRun] = useState<AiRun | null>(null);
  const runRef = useRef(run); runRef.current = run;
  const [instruction, setInstruction] = useState('');
  const [scope, setScope] = useState('section');
  const [mode, setMode] = useState('generate');
  const [adjustId, setAdjustId] = useState<string | null>(null);
  const [excluded, setExcluded] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [streamState, setStreamState] = useState('');
  const [streamRetry, setStreamRetry] = useState(0);
  const [unsynced, setUnsynced] = useState(0);
  const textarea = useRef<HTMLTextAreaElement>(null);
  const mounted = useRef(true);
  const pendingReviews = useRef(new Map<string, { runId: string; hunkId: string; version: number; status: string }>());
  const syncing = useRef(false);
  const generatedPanelId = useId();
  const panelId = suppliedPanelId || generatedPanelId;
  const [preferredWidth, setPreferredWidth] = useState(storedDrawerWidth);
  const [viewportWidth, setViewportWidth] = useState(() => window.innerWidth);
  const [viewport, setViewport] = useState(() => ({ top: 0, height: window.innerHeight }));
  const [resizing, setResizing] = useState(false);
  const drag = useRef<{ pointerId: number; startX: number; startWidth: number; handle: HTMLElement } | null>(null);
  const maxWidth = Math.max(320, Math.min(900, viewportWidth - 300));
  const width = Math.min(preferredWidth, maxWidth);
  const expanded = width > Math.min(400, maxWidth) + 1;
  const releaseResize = () => {
    const current = drag.current; drag.current = null;
    if (current?.handle.hasPointerCapture(current.pointerId)) current.handle.releasePointerCapture(current.pointerId);
  };
  const finishResize = () => { releaseResize(); setResizing(false); };
  useEffect(() => {
    const resize = () => { releaseResize(); setResizing(false); setViewportWidth(window.innerWidth); };
    window.addEventListener('resize', resize);
    return () => { window.removeEventListener('resize', resize); releaseResize(); };
  }, []);
  useEffect(() => { if (!open) finishResize(); }, [open]);
  useLayoutEffect(() => {
    if (!open) return;
    let frame = 0;
    const header = document.querySelector<HTMLElement>('.lup-header');
    const measure = () => {
      frame = 0;
      const visible = window.visualViewport;
      const offset = visible?.offsetTop || 0;
      const height = visible?.height || window.innerHeight;
      const top = Math.min(offset + height, Math.max(offset, header?.getBoundingClientRect().bottom || 0));
      const available = Math.max(0, height - (top - offset));
      setViewport(current => current.top === top && current.height === available ? current : { top, height: available });
    };
    const schedule = () => { if (!frame) frame = requestAnimationFrame(measure); };
    measure();
    const observer = new ResizeObserver(schedule); if (header) observer.observe(header);
    window.addEventListener('scroll', schedule, { passive: true }); window.addEventListener('resize', schedule);
    window.visualViewport?.addEventListener('resize', schedule); window.visualViewport?.addEventListener('scroll', schedule);
    return () => { cancelAnimationFrame(frame); observer.disconnect(); window.removeEventListener('scroll', schedule); window.removeEventListener('resize', schedule); window.visualViewport?.removeEventListener('resize', schedule); window.visualViewport?.removeEventListener('scroll', schedule); };
  }, [open]);
  useEffect(() => {
    const timer = window.setTimeout(() => { try { window.localStorage.setItem(drawerWidthKey, JSON.stringify(preferredWidth)); } catch { /* The drawer also works without persistent storage. */ } }, 180);
    return () => window.clearTimeout(timer);
  }, [preferredWidth]);
  const resizeKey = (event: React.KeyboardEvent<HTMLElement>) => {
    const step = event.shiftKey ? 40 : 20;
    const next = event.key === 'Home' ? 320 : event.key === 'End' ? maxWidth : event.key === 'ArrowLeft' ? width + step : event.key === 'ArrowRight' ? width - step : null;
    if (next === null) return;
    event.preventDefault(); event.stopPropagation(); setPreferredWidth(Math.max(320, Math.min(maxWidth, next)));
  };
  const consume = (value: AiRun) => {
    if (!mounted.current || value.pageId.toLowerCase() !== pageId.toLowerCase()) return;
    if (runRef.current?.id === value.id && value.eventCursor < runRef.current.eventCursor) return;
    value = { ...value, hunks: value.hunks.map(hunk => {
      const optimistic = pendingReviews.current.get(hunk.id + ':' + hunk.version);
      return optimistic ? { ...hunk, status: optimistic.status as typeof hunk.status } : hunk;
    }) };
    review.enabled = enabled && !aiRunWorking(value);
    runRef.current = value; setRun(value); review.setRun(value);
    setRuns(current => [value, ...current.filter(item => item.id !== value.id)].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
  };
  const syncReviews = async () => {
    if (syncing.current) return;
    syncing.current = true;
    try {
      while (pendingReviews.current.size && mounted.current) {
        const [key, item] = pendingReviews.current.entries().next().value!;
        const next = await api.review(item.runId, item.hunkId, item.status, item.version);
        pendingReviews.current.delete(key); setUnsynced(pendingReviews.current.size);
        if (runRef.current?.id === next.id) consume(next);
      }
    } catch (reason) {
      if (mounted.current) setError((reason instanceof Error ? reason.message : 'Reviewstatus kunne ikke gemmes.') + ' Godkendte ændringer er gemt i dokumentet. Prøv at gemme status igen.');
    } finally { syncing.current = false; }
  };
  const queueReview = (runId: string, hunkId: string, version: number, status: string) => {
    pendingReviews.current.set(hunkId + ':' + version, { runId, hunkId, version, status });
    setUnsynced(pendingReviews.current.size);
    void syncReviews();
  };
  useEffect(() => {
    mounted.current = true;
    const abort = new AbortController();
    void api.list(pageId, abort.signal).then(async values => {
      if (abort.signal.aborted) return;
      setRuns(values);
      if (!runRef.current && values[0]) { const next = await api.read(values[0].id); if (!abort.signal.aborted && !runRef.current) consume(next); }
    }).catch(reason => { if (!abort.signal.aborted) setError(reason instanceof Error ? reason.message : 'Kladder kunne ikke hentes.'); }).finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => { mounted.current = false; abort.abort(); review.setRun(null); };
  }, [api, pageId]);
  useEffect(() => { review.enabled = enabled && !busy && !aiRunWorking(run); review.refresh(); }, [review, enabled, busy, run?.status]);
  useEffect(() => { setScope(target?.selection ? 'selection' : 'section'); }, [target]);
  useEffect(() => { if (open) textarea.current?.focus(); }, [open, adjustId]);
  // Receipts survive reloads and lost PATCH responses. Reconcile without applying text twice.
  useEffect(() => {
    if (!run) return;
    for (const hunk of run.hunks) if (review.hasReceipt(hunk) && hunk.status !== 'accepted') queueReview(run.id, hunk.id, hunk.version, 'accepted');
  }, [run, review]);
  useEffect(() => {
    if (!run || !aiRunWorking(run)) { setStreamState(''); return; }
    const abort = new AbortController(); let cursor = run.eventCursor; let attempts = 0;
    const pause = (ms: number) => new Promise<void>(resolve => {
      const timer = window.setTimeout(resolve, ms);
      abort.signal.addEventListener('abort', () => { clearTimeout(timer); resolve(); }, { once: true });
    });
    void (async () => {
      while (!abort.signal.aborted) {
        try {
          setStreamState(attempts ? 'Forbinder igen…' : 'Forbundet');
          await api.stream(run.id, cursor, abort.signal, next => { if (abort.signal.aborted) return; cursor = Math.max(cursor, next.eventCursor); attempts = 0; consume(next); }, () => { if (!abort.signal.aborted) setStreamState('Skriver næste ændring…'); });
          if (abort.signal.aborted) return;
          // A closed stream is resumable; completed server-side changes remain available.
          const next = await api.read(run.id); if (abort.signal.aborted) return; cursor = Math.max(cursor, next.eventCursor); consume(next);
          if (!aiRunWorking(next)) return;
        } catch (reason) {
          if (abort.signal.aborted) return;
          if (++attempts >= 5) { setStreamState('Forbindelsen er afbrudt. Arbejdet fortsætter på serveren.'); setError(reason instanceof Error ? reason.message : 'Streamen blev afbrudt.'); return; }
          setStreamState('Forbinder igen… Forslagene er gemt.');
        }
        await pause(Math.min(1000 * 2 ** attempts, 15000));
      }
    })();
    // Hiding the drawer keeps this subscription alive. Navigating never stops the background run.
    return () => abort.abort();
  }, [api, run?.id, run?.status, streamRetry]);
  const accept = (ids: string[]) => {
    try {
      setError(''); const accepted = review.accept(ids);
      for (const hunk of accepted) queueReview(runRef.current!.id, hunk.id, hunk.version, 'accepted');
    } catch (reason) {
      review.refresh(); setError(reason instanceof Error ? reason.message : 'Forslaget kunne ikke godkendes.');
      for (const id of ids) if (review.conflicts.has(id)) {
        const hunk = runRef.current?.hunks.find(item => item.id === id);
        if (hunk) queueReview(runRef.current!.id, id, hunk.version, 'conflict');
      }
    }
  };
  const reject = (ids: string[]) => {
    if (!review.enabled) { setError('Vent til AI-opgaven er færdig, eller stop den før du gennemgår forslagene.'); return; }
    const current = runRef.current; if (!current) return;
    const rejected = current.hunks.filter(hunk => ids.includes(hunk.id) && !review.hasReceipt(hunk));
    consume({ ...current, hunks: current.hunks.map(hunk => ids.includes(hunk.id) && !review.hasReceipt(hunk) ? { ...hunk, status: 'rejected' } : hunk) });
    for (const hunk of rejected) queueReview(current.id, hunk.id, hunk.version, 'rejected');
  };
  const adjust = (id: string) => { setAdjustId(id); setInstruction(''); onReveal(); textarea.current?.focus(); };
  review.onAccept = accept; review.onReject = reject; review.onAdjust = adjust;
  const submit = async (event?: React.FormEvent) => {
    event?.preventDefault();
    if (!instruction.trim() || !enabled || busy || aiRunWorking(runRef.current) || !review.editor) return;
    const current = runRef.current;
    try {
      setError(''); setBusy(true); review.enabled = false; review.refresh();
      const hunk = current?.hunks.find(item => item.id === adjustId);
      const liveTarget = hunk ? review.captureHunkTarget(hunk) : target || review.capture();
      const document = review.editor.document;
      const next = adjustId && current ? await api.adjust(current.id, { instruction: instruction.trim(), document, target: liveTarget, hunkIds: [adjustId], excludedSourceIds: excluded })
        : await api.create({ pageId, instruction: instruction.trim(), mode, scope, document, target: liveTarget, excludedSourceIds: excluded, clientRequestId: crypto.randomUUID() });
      consume(next); setInstruction(''); setAdjustId(null);
    } catch (reason) { if (mounted.current) setError(reason instanceof Error ? reason.message : 'Opgaven kunne ikke startes.'); }
    finally { if (mounted.current) setBusy(false); }
  };
  const resume = async () => {
    if (!run || !review.editor || !enabled || busy) return;
    setBusy(true); setError('');
    try { consume(await api.continue(run.id, { document: review.editor.document, target: target || review.capture() })); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Opgaven kunne ikke fortsættes.'); }
    finally { if (mounted.current) setBusy(false); }
  };
  const stop = async () => { if (!run) return; try { consume(await api.cancel(run.id)); } catch (reason) { setError(reason instanceof Error ? reason.message : 'Opgaven kunne ikke stoppes.'); } };
  const remove = async () => {
    if (!run) return;
    try { await api.remove(run.id); setRuns(current => current.filter(item => item.id !== run.id)); setRun(null); runRef.current = null; review.setRun(null); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Opgaven kunne ikke slettes.'); }
  };
  const pending = (run?.hunks || []).filter(hunk => ['ready', 'conflict'].includes(hunk.status) && !review.hasReceipt(hunk));
  const accepted = (run?.hunks || []).filter(hunk => hunk.status === 'accepted' || review.hasReceipt(hunk)).length;
  const newTask = () => { setRun(null); runRef.current = null; review.setRun(null); setAdjustId(null); onCapture(); textarea.current?.focus(); };
  const showProposal = (id: string) => {
    const proposal = [...document.querySelectorAll<HTMLElement>('[data-ai-hunk]')].find(element => element.dataset.aiHunk === id);
    onClose();
    requestAnimationFrame(() => proposal?.scrollIntoView({ block: 'center', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' }));
  };
  const scopeLabel = scope === 'selection' ? 'Markeret tekst' : scope === 'page' ? 'Aktiv side' : 'Ved dit afsnit';
  if (!open) return null;
  return <aside id={panelId} className="native-ai-panel" role={viewportWidth <= 760 ? 'dialog' : undefined} aria-modal={viewportWidth <= 760 ? true : undefined} aria-label="AI-skrivepartner" data-expanded={expanded} data-resizing={resizing} style={{ '--native-ai-width': width + 'px', '--native-ai-top': viewport.top + 'px', '--native-ai-height': viewport.height + 'px' } as React.CSSProperties} onKeyDown={event => {
    if (event.key === 'Tab' && viewportWidth <= 760) {
      const controls = [...event.currentTarget.querySelectorAll<HTMLElement>('button,input,textarea,select,a[href],summary,[tabindex]')].filter(element => element.tabIndex >= 0 && !element.matches(':disabled') && element.getClientRects().length);
      const first = controls[0], last = controls.at(-1);
      if (first && last && (event.shiftKey ? document.activeElement === first : document.activeElement === last)) { event.preventDefault(); (event.shiftKey ? last : first).focus(); }
      return;
    }
    const target = event.target as HTMLElement;
    const nestedSurface = target.closest('[role="menu"],[role="listbox"],[role="dialog"]');
    if (event.key !== 'Escape' || event.defaultPrevented || target.closest('select') || nestedSurface && nestedSurface !== event.currentTarget) return;
    event.preventDefault(); event.stopPropagation();
    if (drag.current) { setPreferredWidth(drag.current.startWidth); finishResize(); } else onClose();
  }}>
    <div className="native-ai-resize" role="separator" aria-label="AI-panelets bredde" aria-orientation="vertical" aria-controls={panelId} aria-valuemin={320} aria-valuemax={maxWidth} aria-valuenow={Math.round(width)} aria-valuetext={Math.round(width) + ' pixels'} tabIndex={0} onKeyDown={resizeKey}
      onPointerDown={event => { if (event.button !== 0 || viewportWidth <= 760) return; event.preventDefault(); event.currentTarget.focus(); drag.current = { pointerId: event.pointerId, startX: event.clientX, startWidth: width, handle: event.currentTarget }; event.currentTarget.setPointerCapture(event.pointerId); setResizing(true); }}
      onPointerMove={event => { const current = drag.current; if (current?.pointerId === event.pointerId) setPreferredWidth(Math.max(320, Math.min(maxWidth, current.startWidth + current.startX - event.clientX))); }}
      onPointerUp={finishResize} onPointerCancel={() => { if (drag.current) setPreferredWidth(drag.current.startWidth); finishResize(); }} onLostPointerCapture={() => { drag.current = null; setResizing(false); }} />
    <header><div><strong id={panelId + '-title'}>AI</strong><small title={pageTitle}>{pageTitle}</small></div><div className="native-ai-header-actions"><button type="button" className="native-ai-icon native-ai-expand" onClick={() => setPreferredWidth(expanded ? 400 : maxWidth)} aria-label={expanded ? 'Gør AI-panelet smallere' : 'Udvid AI-panelet'} title={expanded ? 'Gør AI-panelet smallere' : 'Giv AI mere plads'} aria-pressed={expanded}><span aria-hidden="true">{expanded ? '↙' : '↗'}</span></button><button type="button" className="native-ai-icon" onClick={onClose} aria-label="Skjul AI-panelet" title="Skjul AI-panelet (Esc)">×</button></div></header>
    <div className="native-ai-panel-body">
      {loading && <p role="status">Henter dine private kladder…</p>}
      {!run && <div className="native-ai-intro"><strong>Hvad skal vi arbejde med?</strong><p>Beskriv det her. Forslagene vises direkte i teksten, hvor du kan godkende dem.</p><div className="native-ai-starters">{['Gør teksten tydeligere', 'Skriv videre', 'Lav et overblik'].map(prompt => <button type="button" key={prompt} disabled={!enabled} onClick={() => { setInstruction(prompt); setMode(prompt === 'Gør teksten tydeligere' ? 'rewrite' : 'generate'); textarea.current?.focus(); }}>{prompt}</button>)}</div></div>}
      {run && <>
        <div className="native-ai-task"><strong>{run.instruction}</strong><p role="status" aria-live="polite">{statusLabel[run.status] || run.status}{aiRunWorking(run) && <span className="native-ai-working" aria-hidden="true"> ···</span>}</p>{run.progress && <small>{run.progress}</small>}{streamState && aiRunWorking(run) && <small>{streamState}</small>}</div>
        {run.coverage?.partial && <p className="native-ai-coverage is-partial">Udvalgte dele læst · {run.coverage.activeBlocks} af {run.coverage.totalBlocks} blokke</p>}
        <div className="native-ai-batch"><span>{pending.length} forslag · {accepted} godkendt</span>{aiRunWorking(run) && <small>Forslagene kan gennemgås, når opgaven er færdig.</small>}{pending.length > 0 && <div><button type="button" disabled={!review.enabled || review.conflicts.size > 0} onClick={() => accept(pending.map(item => item.id))}>Godkend alle</button><button type="button" disabled={!review.enabled} onClick={() => reject(pending.map(item => item.id))}>Afvis alle</button></div>}</div>
        {pending.map(hunk => <div key={hunk.id} className={'native-ai-hunk-summary' + (review.conflicts.has(hunk.id) ? ' is-conflict' : '')}><button type="button" onClick={() => showProposal(hunk.id)} title="Se forslaget i teksten">{review.conflicts.has(hunk.id) ? '⚠ ' : '↳ '}{hunk.explanation || 'Ændring ' + (hunk.order + 1)}</button><button type="button" onClick={() => adjust(hunk.id)}>Justér</button></div>)}
        {run.status === 'needs_continuation' && <div className="native-ai-continuation"><p>De færdige forslag er gemt. Fortsæt for at skrive næste del.</p><button type="button" disabled={!enabled || busy} onClick={() => { void resume(); }}>Fortsæt</button></div>}
        {run.status === 'failed' && <button type="button" disabled={!enabled || busy} onClick={() => { void resume(); }}>Fortsæt opgaven</button>}
        {run.error && <p className="native-ai-error" role="alert">{aiNativeError(run.error)}</p>}
        <div className="native-ai-task-actions">{aiRunWorking(run) && <button type="button" onClick={() => { void stop(); }}>Stop opgaven</button>}<button type="button" onClick={newTask}>Ny opgave</button></div>
        {(run.coverage || run.sources.length > 0) && <details className="native-ai-context"><summary>Kontekst og kilder · {run.sources.length}</summary>{run.coverage && <p className="native-ai-coverage">{run.coverage.activeBlocks} af {run.coverage.totalBlocks} blokke læst{run.coverage.detail && <span>{run.coverage.detail}</span>}</p>}<div className="native-ai-sources">{run.sources.map(source => <label key={source.id}><input type="checkbox" checked={!excluded.includes(source.id)} onChange={event => setExcluded(current => event.target.checked ? current.filter(id => id !== source.id) : [...current, source.id])} /><span><a href={source.href} target="_blank" rel="noopener noreferrer">{source.title}</a>{source.location && <small>{source.location}</small>}{source.state !== 'ready' && <small>{source.state}</small>}{source.excerpt && <small>{source.excerpt}</small>}</span></label>)}{run.sources.length > 0 && <small>Fravalg bruges ved næste opgave eller justering.</small>}</div></details>}
      </>}
      {error && <div className="native-ai-error" role="alert"><p>{error}</p>{unsynced > 0 && <button type="button" onClick={() => { setError(''); void syncReviews(); }}>Gem reviewstatus igen</button>}{aiRunWorking(run) && <button type="button" onClick={() => { setError(''); setStreamRetry(value => value + 1); }}>Forbind igen</button>}</div>}
      {runs.length > 0 && <details className="native-ai-history"><summary>Tidligere opgaver · {runs.length}</summary><label className="native-ai-run-picker">Opgave<select aria-label="Vælg gemt AI-opgave" value={run?.id || ''} onChange={event => { setError(''); setAdjustId(null); void api.read(event.target.value).then(consume).catch(reason => setError(reason instanceof Error ? reason.message : 'Kladde kunne ikke åbnes.')); }}><option value="" disabled>Vælg opgave</option>{runs.map(item => <option key={item.id} value={item.id}>{item.instruction}</option>)}</select></label>{run && !aiRunWorking(run) && <button type="button" onClick={() => { void remove(); }}>Slet kladde</button>}<small>Private kladder gemmes i 30 dage.</small></details>}
    </div>
    <form className="native-ai-composer" onSubmit={event => { void submit(event); }}>
      {adjustId && <div className="native-ai-adjust-label"><strong>Justér dette forslag</strong><button type="button" onClick={() => setAdjustId(null)}>Annuller</button></div>}
      <label htmlFor={panelId + '-instruction'} className="native-ai-sr-only">Instruktion til AI</label>
      <textarea ref={textarea} id={panelId + '-instruction'} value={instruction} onChange={event => setInstruction(event.target.value)} placeholder={adjustId ? 'Hvordan skal forslaget ændres?' : 'Hvad skal vi skrive eller forbedre?'} onKeyDown={event => { if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); void submit(); } }} />
      {!adjustId && <details className="native-ai-options"><summary>Tilpas opgaven <span>{scopeLabel}</span></summary><div className="native-ai-options-fields"><select aria-label="AI-opgave" value={mode} onChange={event => setMode(event.target.value)}><option value="generate">Skriv indhold</option><option value="rewrite">Omskriv</option><option value="mermaid">Diagram</option></select><select aria-label="Redigeringsområde" value={scope} onChange={event => setScope(event.target.value)}><option value="section">Ved dette afsnit</option><option value="page">Aktiv side</option>{target?.selection && <option value="selection">Markeret tekst</option>}</select><button type="button" disabled={!enabled} onClick={onCapture}>{target?.selection ? 'Opdatér markering' : 'Brug markering / sted'}</button></div></details>}
      <div className="native-ai-compose-foot"><small className="native-ai-composer-context">Privat kladde · Ctrl/⌘ + Enter</small><button type="submit" disabled={!enabled || !instruction.trim() || busy || aiRunWorking(run)}>{busy ? 'Starter…' : adjustId ? 'Justér forslag' : 'Lav forslag'}</button></div>
    </form>
  </aside>;
}
