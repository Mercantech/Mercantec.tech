import React, { Suspense, lazy, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { createPortal } from 'react-dom';
import { trackPosition } from '@blocknote/core';
import { TextSelection } from 'prosemirror-state';
import { createReactBlockSpec } from '@blocknote/react';
import type { ExcalidrawImperativeAPI, BinaryFileData } from '@excalidraw/excalidraw/types';
import '@excalidraw/excalidraw/index.css';
import { EmojiCatalog } from './emoji-context';
import { EmojiPicker, type PlatformEmoji } from './emoji-picker';
import { consumeDrawingOpen, useDrawingEnvironment, requestCreateDrawing } from './drawing-context';
import { DrawingApiError, DrawingParentMismatchError, drawingRequest, type DrawingCandidates, type DrawingContext, type DrawingGrant } from './drawing-api';
import { DrawingController, loadDrawingSdk } from './drawing-runtime';
import { useViewport } from './use-viewport';

const Canvas = lazy(() => loadDrawingSdk().then(module => ({ default: module.Excalidraw })));
const DrawingMenu = lazy(() => loadDrawingSdk().then(module => ({ default: function Menu() {
  const MainMenu = module.MainMenu;
  return <MainMenu><MainMenu.DefaultItems.ClearCanvas /><MainMenu.DefaultItems.ChangeCanvasBackground /><MainMenu.Separator /><MainMenu.DefaultItems.SearchMenu /><MainMenu.DefaultItems.CommandPalette /><MainMenu.DefaultItems.Help /></MainMenu>;
} })));
const rejectEmbeddable = () => false;
const drawingUiOptions = { canvasActions: { loadScene: false, saveToActiveFile: false, saveAsImage: false, export: false, toggleTheme: false } } as const;

function download(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob), link = document.createElement('a'); link.href = url; link.download = name; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
function useController(controller: DrawingController | null) {
  const subscribe = useCallback((listener: () => void) => controller?.subscribe(listener) ?? (() => {}), [controller]);
  const snapshot = useCallback(() => controller?.changeVersion ?? 0, [controller]);
  useSyncExternalStore(subscribe, snapshot, snapshot);
}

function DrawingSettings({ controller }: { controller: DrawingController }) {
  const [grants, setGrants] = useState<DrawingGrant[]>([]);
  const [candidates, setCandidates] = useState<DrawingCandidates>({ people: [], holds: [] });
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const base = '/' + controller.id;
  const refresh = async () => setGrants(await drawingRequest<DrawingGrant[]>(controller.env, base + '/grants'));
  useEffect(() => { void refresh().catch(value => setError(value.message)); }, [controller]);
  useEffect(() => {
    const signal = new AbortController(); const timer = setTimeout(() => {
      void drawingRequest<DrawingCandidates>(controller.env, base + '/candidates?search=' + encodeURIComponent(search), { signal: signal.signal }).then(setCandidates).catch(value => { if (!signal.signal.aborted) setError(value.message); });
    }, 180);
    return () => { clearTimeout(timer); signal.abort(); };
  }, [controller, search]);
  const work = async (operation: () => Promise<unknown>) => { setBusy(true); setError(''); try { await operation(); await controller.refresh(); await refresh(); } catch (value) { setError(value instanceof Error ? value.message : 'Adgangen kunne ikke ændres.'); } finally { setBusy(false); } };
  return <div className="notion-drawing-settings">
    {controller.env.domain === 'notion' && <label>Ved udgivelse<select value={controller.context?.mode || 'snapshot'} disabled={busy} onChange={event => { const mode = event.target.value; void work(() => drawingRequest<DrawingContext>(controller.env, base + '/settings', { method: 'PUT', body: JSON.stringify({ mode, expectedAccessVersion: controller.context?.accessVersion }) })); }}><option value="snapshot">Fast tegning</option><option value="live">Live fællestavle</option></select></label>}
    <label>Giv skriveadgang til tegningen<input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Søg navn eller hold" /></label>
    <div className="notion-drawing-candidates">
      {candidates.people.map(item => <button key={item.id} type="button" disabled={busy || grants.some(grant => grant.userSub === item.id)} onClick={() => void work(() => drawingRequest(controller.env, base + '/grants', { method: 'PUT', body: JSON.stringify({ userSub: item.id }) }))}>{item.displayName} · Giv adgang</button>)}
      {candidates.holds.map(item => <button key={item.id} type="button" disabled={busy || grants.some(grant => grant.holdId === item.id)} onClick={() => void work(() => drawingRequest(controller.env, base + '/grants', { method: 'PUT', body: JSON.stringify({ holdId: item.id }) }))}>{item.displayName} · Giv holdet adgang</button>)}
    </div>
    {grants.map(item => <div key={item.id} className="notion-drawing-grant"><span>{item.displayName}</span><button type="button" disabled={busy} onClick={() => void work(() => drawingRequest(controller.env, base + '/grants/' + item.id, { method: 'DELETE' }))}>Fjern adgang</button></div>)}
    {error && <p className="notion-drawing-error" role="alert">{error}</p>}
  </div>;
}

class DrawingCanvasBoundary extends React.Component<{ children: React.ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() { return this.state.failed ? <p className="notion-drawing-error" role="alert">Tegneværktøjet kunne ikke åbnes. Luk tegningen, og genindlæs siden.</p> : this.props.children; }
}
function DrawingDialog({ controller, close, discard, portalHost }: { controller: DrawingController; close: () => void; discard: () => void; portalHost: HTMLElement }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const api = useRef<ExcalidrawImperativeAPI | null>(null);
  const lastPointer = useRef(0);
  const [settings, setSettings] = useState(false);
  const [emojisOpen, setEmojisOpen] = useState(false);
  const [error, setError] = useState('');
  const emojis = useContext(EmojiCatalog);
  const picker = useId();
  const menu = useMemo(() => <DrawingMenu />, []);
  useController(controller);
  const attach = useCallback((value: ExcalidrawImperativeAPI) => { api.current = value; controller.attach(value); }, [controller]);
  const onChange = useCallback((elements: Parameters<DrawingController['localChange']>[0], state: Parameters<DrawingController['localChange']>[1], files: Parameters<DrawingController['localChange']>[2]) => controller.localChange(elements, state, files), [controller]);
  const onPointerUpdate = useCallback(({ pointer, button }: { pointer: { x: number; y: number; tool: 'pointer' | 'laser' }; button: 'down' | 'up' }) => { const now = performance.now(); if (now - lastPointer.current >= 33 || button === 'up') { lastPointer.current = now; controller.pointer(pointer, button); } }, [controller]);
  useEffect(() => {
    const element = dialog.current!; element.showModal();
    const overflow = document.body.style.overflow; document.body.style.overflow = 'hidden';
    void controller.open().catch(value => setError(value.message));
    return () => { controller.detach(); element.close(); document.body.style.overflow = overflow; };
  }, [controller]);
  const exportFile = async (type: 'png' | 'svg' | 'source') => {
    try {
      setError(''); const module = await loadDrawingSdk(); await controller.loadFiles();
      const appState = { viewBackgroundColor: controller.background, exportScale: 1, exportBackground: true, exportWithDarkMode: controller.env.theme === 'dark', exportEmbedScene: false };
      if (type === 'source') download(new Blob([module.serializeAsJSON(controller.elements, appState, controller.files, 'local')], { type: 'application/vnd.excalidraw+json' }), 'tegning.excalidraw');
      else if (type === 'svg') download(new Blob([(await module.exportToSvg({ elements: controller.elements.filter(item => !item.isDeleted), appState, files: controller.files })).outerHTML], { type: 'image/svg+xml' }), 'tegning.svg');
      else download(await module.exportToBlob({ elements: controller.elements.filter(item => !item.isDeleted), appState, files: controller.files, mimeType: 'image/png', maxWidthOrHeight: 4096 }), 'tegning.png');
    } catch (value) { setError(value instanceof Error ? value.message : 'Tegningen kunne ikke eksporteres.'); }
  };
  const insertEmoji = async (emoji: PlatformEmoji) => {
    try {
      const current = api.current; if (!current || !controller.canEdit) return;
      const response = await fetch(emoji.contentUrl, { cache: 'no-store' }); if (!response.ok) throw new Error('Emojien kunne ikke hentes.');
      const blob = await response.blob(); const bitmap = await createImageBitmap(blob);
      const ratio = bitmap.height / bitmap.width; bitmap.close();
      const dataURL = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result)); reader.onerror = reject; reader.readAsDataURL(blob); });
      const id = crypto.randomUUID().replace(/-/g, '') as BinaryFileData['id'];
      current.addFiles([{ id, dataURL: dataURL as BinaryFileData['dataURL'], mimeType: blob.type as BinaryFileData['mimeType'], created: Date.now() }]);
      const module = await loadDrawingSdk(), state = current.getAppState();
      const image = module.convertToExcalidrawElements([{ type: 'image', fileId: id, x: state.width / 2 / state.zoom.value - state.scrollX - 48, y: state.height / 2 / state.zoom.value - state.scrollY - 48 * ratio, width: 96, height: 96 * ratio, status: 'saved' }]);
      const elements = module.restoreElements([...current.getSceneElementsIncludingDeleted(), ...image], null, { repairBindings: true });
      current.updateScene({ elements, captureUpdate: module.CaptureUpdateAction.IMMEDIATELY });
      setEmojisOpen(false);
    } catch (value) { setError(value instanceof Error ? value.message : 'Emojien kunne ikke indsættes.'); }
  };
  // Keep clipboard events outside ProseMirror's native DOM handlers while letting
  // Excalidraw's document listeners receive them. React context stays intact.
  return createPortal(<dialog ref={dialog} className="notion-drawing-dialog" data-notion-theme={controller.env.theme} aria-label="Tegning" onKeyDown={event => event.stopPropagation()} onCancel={event => { event.preventDefault(); const state = api.current?.getAppState(); if (!state?.openDialog && !state?.openMenu) close(); }}>
    <header className="notion-drawing-dialog-head"><strong>Tegning</strong><span className="notion-drawing-status" role="status">{controller.status}</span><div className="notion-drawing-actions">
      {controller.canEdit && <button type="button" onClick={() => { setSettings(false); setEmojisOpen(value => !value); }} aria-expanded={emojisOpen}>Emojis</button>}
      {controller.context?.canManageDrawing && !controller.env.presentation && <button type="button" onClick={() => { setEmojisOpen(false); setSettings(value => !value); }} aria-expanded={settings}>Adgang</button>}
      <details><summary>Eksportér</summary><button type="button" onClick={() => void exportFile('png')}>PNG</button><button type="button" onClick={() => void exportFile('svg')}>SVG</button><button type="button" onClick={() => void exportFile('source')}>Excalidraw-fil</button></details>
      <button type="button" onClick={close} aria-label="Luk tegning">Luk</button>
    </div></header>
    {(error || controller.error) && <div className="notion-drawing-error" role="alert"><p>{error || controller.error}</p>{controller.dirty && <><button type="button" onClick={() => void controller.retrySave().catch(value => setError(value.message))}>Prøv at gemme igen</button><button type="button" onClick={discard}>Forkast ugemte ændringer</button></>}</div>}
    {settings && controller.context?.canManageDrawing && <DrawingSettings controller={controller} />}
    {emojisOpen && <div className="notion-drawing-emoji-picker"><EmojiPicker id={picker} emojis={emojis} onPick={item => { void insertEmoji(item); }} onClose={() => setEmojisOpen(false)} /></div>}
    <div className="notion-drawing-canvas"><DrawingCanvasBoundary><Suspense fallback={<p role="status">Åbner tegningen…</p>}>
      <Canvas initialData={{ elements: controller.elements, appState: { ...controller.viewState, viewBackgroundColor: controller.background }, files: controller.files, scrollToContent: !controller.viewState.zoom }} excalidrawAPI={attach} langCode="da-DK" theme={controller.env.theme} viewModeEnabled={!controller.canEdit} isCollaborating={!!controller.provider} handleKeyboardGlobally={false} autoFocus aiEnabled={false} validateEmbeddable={rejectEmbeddable} UIOptions={drawingUiOptions} onChange={onChange} onPointerUpdate={onPointerUpdate}>
        {menu}
      </Canvas>
    </Suspense></DrawingCanvasBoundary></div>
  </dialog>, portalHost);
}

export const createDrawing = createReactBlockSpec({
  type: 'drawing', propSchema: { schemaVersion: { default: 1 }, drawingId: { default: '' } }, content: 'none',
}, {
  render: function DrawingBlock({ block, editor }) {
    const [container,nearViewport]=useViewport<HTMLElement>();
    const runtime = useDrawingEnvironment();
    const controller = runtime && block.props.drawingId ? runtime.scope.get(block.props.drawingId) : null;
    const [opened, setOpened] = useState(false);
    const active=useRef(false);active.current=nearViewport || opened;
    const [error, setError] = useState('');
    const [creating, setCreating] = useState(false);
    const [refreshAttempt, setRefreshAttempt] = useState(0);
    const trigger = useRef<HTMLButtonElement>(null);
    const scroll = useRef({ x: 0, y: 0 });
    const selection = useRef<{ from: () => number; to: () => number } | null>(null);
    useController(controller);
    useEffect(() => () => {
      if (!controller || !runtime) return;
      try {
        const references = (items: any[]): boolean => items.some(item => item.type === 'drawing' && item.props?.drawingId === controller.id || references(item.children || []));
        if (!references(editor.document)) runtime.scope.discard(controller);
      } catch { /* Provider disposal handles a destroyed parent editor. */ }
    }, [controller, runtime?.scope, editor]);
    const capture = () => {
      scroll.current = { x: window.scrollX, y: window.scrollY };
      const state = editor.prosemirrorState.selection;
      selection.current = { from: trackPosition(editor, state.from, 'right'), to: trackPosition(editor, state.to, 'left') };
    };
    const open = () => { capture(); setOpened(true); };
    const close = () => {
      setOpened(false);
      const position = scroll.current;
      window.scrollTo(position.x, position.y);
      // Native modal dialogs keep the editor inert until React closes/unmounts them.
      requestAnimationFrame(() => {
        if (!editor.isEditable) trigger.current?.focus({ preventScroll: true });
        else {
          editor.focus();
          try { if (selection.current) editor.transact(transaction => { const from = selection.current!.from(), to = selection.current!.to(); transaction.setSelection(TextSelection.create(transaction.doc, from, to)); }); } catch { /* Keep the current caret if its original block was removed. */ }
        }
        window.scrollTo(position.x, position.y);
      });
    };
    useEffect(() => {
      const onOpen = () => { if (consumeDrawingOpen(block.id)) open(); };
      window.addEventListener('lup-open-drawing', onOpen); onOpen();
      return () => window.removeEventListener('lup-open-drawing', onOpen);
    }, [block.id]);
    useEffect(() => {
      if (!controller || !runtime) return;
      let cancelled = false;
      let copying = false;
      const copyToTarget = async () => {
        if (cancelled || copying || !editor.isEditable || runtime.env.readOnly || runtime.env.presentation) return false;
        const current = editor.getBlock(block.id);
        if (current?.type !== 'drawing' || current.props.drawingId !== controller.id) return false;
        copying = true;
        try {
          const created = await requestCreateDrawing(runtime.env, block.id, controller.id);
          const live = editor.getBlock(block.id);
          if (!cancelled && live?.type === 'drawing' && live.props.drawingId === controller.id) { editor.updateBlock(live, { props: { drawingId: created.id } }); setError(''); }
          return true;
        } finally { copying = false; }
      };
      const refresh = async () => {
        try {
          const context = await controller.refresh();
          if (!cancelled && context.blockId !== block.id) await copyToTarget();
          if (!cancelled) setError('');
        } catch (value) {
          if (!cancelled) {
            try {
              // A published snapshot may be readable even when its mutable head is not.
              // The server independently authorizes cloning the accessible source.
              if ((value instanceof DrawingParentMismatchError || value instanceof DrawingApiError && value.status === 404) && await copyToTarget()) return;
              setError(value instanceof Error ? value.message : 'Tegningen kunne ikke åbnes.');
            } catch (copyError) { if (!cancelled) setError(copyError instanceof Error ? copyError.message : 'Tegningen kunne ikke kopieres.'); }
          }
        }
      };
      void refresh();
      // The first reconciliation also repairs pasted drawing ownership. Subsequent
      // preview refreshes only matter while the block or its dialog is visible.
      const timer = runtime.env.presentation ? undefined : setInterval(() => { if(active.current)void refresh(); }, 15000);
      const focus = () => { if(active.current)void refresh(); }; window.addEventListener('focus', focus);
      return () => { cancelled = true; clearInterval(timer); window.removeEventListener('focus', focus); };
    }, [controller, block.id, runtime?.env.presentation, refreshAttempt]);
    const create = async () => {
      if (!runtime || !editor.isEditable || creating) return;
      setCreating(true); setError('');
      try { const created = await requestCreateDrawing(runtime.env, block.id); if (editor.getBlock(block.id)?.type === 'drawing') { editor.updateBlock(block, { props: { drawingId: created.id } }); open(); } }
      catch (value) { setError(value instanceof Error ? value.message : 'Tegningen kunne ikke oprettes.'); }
      finally { setCreating(false); }
    };
    const preview = runtime?.env.theme === 'dark' ? controller?.context?.previewDarkUrl : controller?.context?.previewLightUrl;
    return <section ref={container} className="notion-drawing-block" contentEditable={false} aria-label="Tegning">
      <button ref={trigger} type="button" className="notion-drawing-preview" disabled={creating || !runtime} onMouseDown={event => event.preventDefault()} onClick={() => { if (controller) open(); else void create(); }} aria-label={controller ? 'Åbn tegning' : 'Opret tegning'}>
        {preview ? <img src={preview} alt="Tegning" draggable={false} loading="lazy" decoding="async" /> : <span>{creating ? 'Opretter…' : controller ? 'Åbn tegning' : 'Opret tegning'}</span>}
      </button>
      {error && <p className="notion-drawing-error" role="alert">{error} <button type="button" onClick={() => setRefreshAttempt(value => value + 1)}>Prøv igen</button></p>}
      {opened && controller && runtime?.portalHost && <DrawingDialog controller={controller} portalHost={runtime.portalHost} close={close} discard={() => { close(); runtime.scope.discard(controller); }} />}
    </section>;
  },
  toExternalHTML: () => <figure><figcaption>Tegning fra LUP</figcaption></figure>,
});
