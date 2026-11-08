/** Development-only integration fixture. Run through Vite; it is not included in the editor library build. */
import React, { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { BlockNoteSchema, defaultBlockSpecs, type BlockNoteEditor } from '@blocknote/core';
import { createReactBlockSpec, SideMenu, SideMenuController, useCreateBlockNote } from '@blocknote/react';
import { BlockNoteView } from '@blocknote/mantine';
import { withCollaboration } from '@blocknote/core/yjs';
import { TextSelection } from 'prosemirror-state';
import { yUndoPluginKey } from 'y-prosemirror';
import * as Y from 'yjs';
import '@blocknote/mantine/style.css';
import '../src/editor.css';
import { NativeAiReview, nativeAiReviewExtension } from '../src/ai-native-review';
import { NativeAiPanel } from '../src/ai-native-panel';
import { NativeAiLauncher } from '../src/ai-native-launcher';
import { OpenAiOnBlockProvider } from '../src/ai-helper';
import { NotionDragHandleMenu } from '../src/turn-into';
import { canonicalAiJson, findAiBlockNodes } from '../src/ai-review-core.mjs';
import type { AiBlock, AiHunk, AiNativeTarget, AiRun } from '../src/ai-native-types';
import layoutCss from '../../web/Layout/MainLayout.razor.css?raw';
import workspaceCss from '../../web/Pages/Notion/Index.razor.css?raw';
import * as sidebar from '../../web/wwwroot/js/notion-sidebar.js';

const pageId = '00000000-0000-4000-8000-000000000001';
const runId = '00000000-0000-4000-8000-000000000002';
const placeholder = (type: string, label: string) => createReactBlockSpec({ type, content: 'none', propSchema: { reference: { default: '' }, title: { default: label }, source: { default: '' } } }, { render: ({ block }) => <div style={{ padding: '12px', border: '1px solid var(--notion-rule)', borderRadius: 5 }} contentEditable={false}>{label} · {block.props.reference || block.props.title}</div> })();
const schema = BlockNoteSchema.create({ blockSpecs: { ...defaultBlockSpecs, drawing: placeholder('drawing', 'Tegning'), pageMedia: placeholder('pageMedia', 'Materiale'), entityLink: placeholder('entityLink', 'Reference'), mermaidDiagram: placeholder('mermaidDiagram', 'Mermaid') } });
const sample = (): AiBlock[] => [
  { id: 'title', type: 'heading', props: { level: 1 }, content: 'Normalisering af databaser' },
  { id: 'a', type: 'paragraph', content: [{ type: 'text', text: 'Bevar starten. ', styles: { bold: true } }, { type: 'text', text: 'Dette afsnit kan forklares tydeligere.', styles: {} }] },
  { id: 'b', type: 'paragraph', content: 'Vi samler data i tabeller og undgår gentagelser.' },
  { id: 'c', type: 'paragraph', content: [{ type: 'text', text: 'Denne del kan forbedres. ', styles: {} }, { type: 'text', text: 'Bevar slutningen.', styles: { italic: true } }] },
  { id: 'table', type: 'table', content: { type: 'tableContent', rows: [{ cells: ['Elev', 'Hold', 'Lokale'] }, { cells: ['Anna', 'H1', 'A.2'] }, { cells: ['Bo', 'H1', 'A.2'] }] } },
  { id: 'code', type: 'codeBlock', props: { language: 'sql' }, content: 'SELECT Elev.Navn, Hold.Navn\nFROM Elev JOIN Hold ON Elev.HoldId = Hold.Id;' },
  { id: 'diagram', type: 'mermaidDiagram', props: { source: 'flowchart LR\n A[Gentagelser] --> B[Opdel tabeller]\n B --> C[Relationer]' } },
  { id: 'drawing', type: 'drawing', props: { reference: 'owned-drawing-42' } },
  { id: 'media', type: 'pageMedia', props: { reference: 'file-42' } },
  { id: 'reference', type: 'entityLink', props: { reference: 'notion-page-42', title: 'Databaser – teori' } },
];
let apiRun: AiRun | null = null;
const realFetch = window.fetch.bind(window);
window.fetch = async (input, init) => {
  const url = String(input instanceof Request ? input.url : input);
  if (!url.includes('/api/ai/editor/runs')) return realFetch(input, init);
  const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
  if (init?.method === 'PATCH' && apiRun) {
    const body = JSON.parse(String(init.body)); const id = url.split('/').at(-1);
    apiRun = { ...apiRun, hunks: apiRun.hunks.map(hunk => hunk.id === id ? { ...hunk, status: body.status } : hunk) };
    return response(apiRun);
  }
  if (url.includes('?pageId=')) return response(apiRun ? [{ id: apiRun.id, pageId, instruction: apiRun.instruction, mode: apiRun.mode, scope: apiRun.scope, status: apiRun.status, createdAtUtc: apiRun.createdAtUtc, expiresAtUtc: apiRun.expiresAtUtc }] : []);
  return response(apiRun);
};
const frame = () => new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
function readFixtureTheme(): 'light' | 'dark' {
  const root = document.documentElement;
  if (root.dataset.bsTheme === 'light' || root.dataset.bsTheme === 'dark') return root.dataset.bsTheme;
  const scheme = getComputedStyle(root).colorScheme.trim().replace(/^only\s+/, '');
  if (scheme === 'light' || scheme === 'dark') return scheme;
  return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}
function App() {
  const ydoc = useMemo(() => new Y.Doc(), []);
  const review = useMemo(() => new NativeAiReview(ydoc), [ydoc]);
  const editor = useCreateBlockNote(withCollaboration({ schema, extensions: [nativeAiReviewExtension(review)], collaboration: { fragment: ydoc.getXmlFragment('document-store'), user: { name: 'Underviser', color: '#2452e8' }, provider: undefined } }), []);
  const [open, setOpen] = useState(true);
  const [target, setTarget] = useState<AiNativeTarget | null>(null);
  const [revision, setRevision] = useState(0);
  const [ready, setReady] = useState(false);
  const [theme, setTheme] = useState(readFixtureTheme);
  const [workspace, setWorkspace] = useState<HTMLDivElement | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(true);
  const [sidebarMobile, setSidebarMobile] = useState(false);
  const [focused, setFocused] = useState(false);
  const launcher = useRef<HTMLButtonElement>(null);
  const aiLauncherSlot = useMemo(() => workspace?.querySelector<HTMLElement>('.notion-ai-toggle-slot') ?? null, [workspace]);
  const closePanel = () => { setOpen(false); requestAnimationFrame(() => launcher.current?.focus()); };
  const revealPanel = () => {
    workspace?.dispatchEvent(new CustomEvent('notion-layout-reveal', { detail: { panel: 'ai' } }));
    setOpen(true);
  };
  useEffect(() => {
    if (!workspace) return;
    const callbacks = { invokeMethodAsync: (method: string, ...args: unknown[]) => {
      if (method === 'CloseNotionSidebar') setSidebarOpen(false);
      if (method === 'OnNotionSidebarViewport') { setSidebarMobile(Boolean(args[0])); setSidebarOpen(!args[0] && Boolean(args[1])); }
      if (method === 'OnNotionFocusMode') setFocused(Boolean(args[0]));
      return Promise.resolve();
    } };
    const preference = sidebar.mount(workspace, callbacks);
    sidebar.setOpen(workspace, !preference.mobile && preference.desktopOpen);
    (window as any).nativeIdeQa = { setFocus: (value: boolean) => sidebar.setFocus(workspace, value), revealAi: () => workspace.dispatchEvent(new CustomEvent('notion-layout-reveal', { detail: { panel: 'ai' } })), scrollCenterTop: () => sidebar.scrollCenterTop(workspace) };
    setSidebarMobile(preference.mobile); setSidebarOpen(!preference.mobile && preference.desktopOpen);
    const focusChanged = (event: Event) => setFocused(Boolean((event as CustomEvent).detail?.focused));
    workspace.addEventListener('notion-layout-focus', focusChanged);
    return () => { workspace.removeEventListener('notion-layout-focus', focusChanged); sidebar.destroy(workspace); delete (window as any).nativeIdeQa; };
  }, [workspace]);
  useLayoutEffect(() => { if (workspace) sidebar.setOpen(workspace, sidebarOpen); }, [workspace, sidebarOpen, sidebarMobile]);
  useEffect(() => {
    const sync = () => setTheme(readFixtureTheme());
    const observer = new MutationObserver(sync);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme', 'data-theme', 'style', 'class'] });
    const media = matchMedia('(prefers-color-scheme: dark)');
    media.addEventListener('change', sync);
    return () => { observer.disconnect(); media.removeEventListener('change', sync); };
  }, []);
  useEffect(() => {
    let updates = 0; ydoc.on('update', () => updates++);
    const snapshot = () => ({ document: JSON.parse(JSON.stringify(editor.document)), text: editor.prosemirrorState.doc.textContent, updates, receipts: [...ydoc.getMap('ai-review-receipts').keys()], conflicts: [...review.conflicts.entries()], proposals: document.querySelectorAll('.native-ai-proposal').length });
    const reset = async () => {
      apiRun = null; review.setRun(null); ydoc.getMap('ai-review-receipts').clear();
      editor.replaceBlocks(editor.document, sample() as never);
      yUndoPluginKey.getState(editor.prosemirrorState)?.undoManager.clear();
      setTarget(null); setRevision(value => value + 1); await frame(); return snapshot();
    };
    const makeHunk = (operation: AiHunk['operation'], ids: string[], after: AiBlock[], order = 0, target?: AiNativeTarget): AiHunk => ({ id: crypto.randomUUID(), version: 1, order, operation, targetBlockIds: ids, before: ids.map(id => JSON.parse(JSON.stringify(editor.getBlock(id)))), after, status: 'ready', explanation: 'Gør forklaringen tydeligere og bevar den faglige sammenhæng.', sourceIds: ['source-1'], baseFingerprint: 'server-fingerprint', target });
    const propose = async (hunks?: AiHunk[]) => {
      apiRun = { id: runId, pageId, mode: 'rewrite', scope: 'page', instruction: 'Gør introduktionen mere forståelig for elever på H1.', status: 'ready', hunks: hunks || [makeHunk('replace', ['b'], [{ type: 'paragraph', content: 'Vi opdeler data i tabeller med ét tydeligt ansvar. En nøgle forbinder tabellerne, så vi kan finde sammenhængen uden at gentage oplysninger.' }])], sources: [{ id: 'source-1', kind: 'notion-page', sourceId: 'theory-1', title: 'Databaser – fælles teorinotesbog', href: '/theory/databaser', excerpt: 'Normalisering mindsker gentagelser og gør det lettere at opdatere data.', state: 'ready' }], coverage: { activeBlocks: editor.document.length, totalBlocks: editor.document.length, activeTokens: 800, externalTokens: 150, partial: false }, progress: 'Forslagene er klar til gennemgang.', model: 'gpt-6.1-sol', error: undefined, eventCursor: 1, outputTokens: 100, createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date(Date.now() + 30 * 86400000).toISOString() };
      review.setRun(apiRun); setRevision(value => value + 1); await frame(); return apiRun;
    };
    const select = (fromId: string, fromOffset: number, toId: string, toOffset: number) => {
      const doc = editor.prosemirrorState.doc;
      const from = findAiBlockNodes(doc, [fromId])[0].from + 2 + fromOffset;
      const to = findAiBlockNodes(doc, [toId])[0].from + 2 + toOffset;
      editor.transact(tr => { tr.setSelection(TextSelection.create(tr.doc, from, to)); });
      const selected = review.capture(); setTarget(selected); return selected;
    };
    const assert = (condition: unknown, message: string) => { if (!condition) throw new Error(message); };
    const runChecks = async () => {
      const results: string[] = [];
      await reset(); const original = snapshot(); await propose();
      assert(canonicalAiJson(editor.document) === canonicalAiJson(original.document), 'Preview mutated canonical blocks');
      assert(snapshot().updates === original.updates, 'Preview emitted a Yjs update');
      assert(document.querySelectorAll('.native-ai-proposal').length === 1, 'Ready hunk did not render inline'); results.push('private decorations leave canonical/Yjs content unchanged');
      review.accept([apiRun!.hunks[0].id]); const accepted = snapshot();
      assert(accepted.text.includes('ét tydeligt ansvar'), 'Acceptance missing text');
      assert(accepted.receipts.length === 1, 'Receipt missing');
      review.accept([apiRun!.hunks[0].id]); assert(snapshot().text === accepted.text, 'Duplicate acceptance inserted twice');
      review.setRun({ ...apiRun!, hunks: apiRun!.hunks.map(hunk => ({ ...hunk, version: hunk.version + 1 })) });
      review.accept([apiRun!.hunks[0].id]); assert(snapshot().text === accepted.text, 'An adjusted version replayed an accepted stable operation');
      editor.undo(); await frame(); assert(canonicalAiJson(editor.document) === canonicalAiJson(original.document), 'One undo did not restore document');
      assert(snapshot().receipts.length === 1, 'Undo removed receipt and permits replay'); results.push('accept is one undo; receipt prevents replay before and after undo');
      await reset(); await propose(); editor.updateBlock('a', { content: 'Redigering et andet sted.' }); await frame();
      assert(review.conflicts.size === 0, 'Unrelated editing invalidated target'); review.accept([apiRun!.hunks[0].id]); results.push('unrelated collaboration edits permit approval');
      await reset(); const h1 = makeHunk('replace', ['a'], [{ type: 'paragraph', content: 'Ny første' }]); const h2 = makeHunk('replace', ['b'], [{ type: 'paragraph', content: 'Ny anden' }], 1); await propose([h1, h2]);
      editor.updateBlock('b', { content: 'Samtidig ændring i målet.' }); await frame(); const beforeConflict = canonicalAiJson(editor.document);
      let blocked = false; try { review.accept([h1.id, h2.id]); } catch { blocked = true; }
      assert(blocked && canonicalAiJson(editor.document) === beforeConflict, 'Atomic all partially applied'); assert(review.conflicts.has(h2.id), 'Target conflict missing'); results.push('target conflicts stop atomic approval of every hunk');
      await reset(); const selection = select('a', 15, 'c', 24);
      const selected = makeHunk('replace_selection', selection.blockIds, [{ type: 'paragraph', content: 'Ny første forklaring.' }, { type: 'paragraph', content: 'Ny sidste forklaring.' }], 0, JSON.parse(JSON.stringify(selection)));
      await propose([selected]); editor.insertBlocks([{ type: 'paragraph', content: 'Indsat før målet.' }], 'title', 'before'); await frame(); review.accept([selected.id]);
      assert(editor.prosemirrorState.doc.textContent.includes('Bevar starten. Ny første forklaring.'), 'Leading boundary text missing');
      assert(editor.prosemirrorState.doc.textContent.includes('Ny sidste forklaring. Bevar slutningen.'), 'Trailing boundary text missing');
      assert(editor.getBlock('drawing')?.props.reference === 'owned-drawing-42', 'Drawing reference lost'); results.push('persisted Yjs anchors preserve multi-block partial boundary text and media');
      await reset(); editor.replaceBlocks(editor.document, [{ id: 'parent', type: 'bulletListItem', content: [{ type: 'text', text: 'Bevar før. ', styles: { bold: true } }, { type: 'text', text: 'gammel tekst', styles: {} }], children: [{ id: 'child', type: 'bulletListItem', content: [{ type: 'text', text: 'gammelt ', styles: {} }, { type: 'text', text: 'Bevar efter.', styles: { italic: true } }] }, { id: 'keep-child', type: 'bulletListItem', content: 'Bevar separat barn.' }] }, { id: 'outside', type: 'paragraph', content: 'Helt udenfor.' }] as never);
      const nestedSelection = select('parent', 11, 'child', 8);
      assert(nestedSelection.blockIds.includes('parent') && nestedSelection.blockIds.includes('child'), 'Nested selection did not include its partial parent and child');
      const nestedHunk = makeHunk('replace_selection', nestedSelection.blockIds, [{ type: 'paragraph', content: 'Ny forklaring. ' }], 0, JSON.parse(JSON.stringify(nestedSelection)));
      await propose([nestedHunk]); review.accept([nestedHunk.id]);
      assert(editor.prosemirrorState.doc.textContent === 'Bevar før. Ny forklaring. Bevar efter.Bevar separat barn.Helt udenfor.', 'Nested selection lost outside text');
      assert(editor.getBlock('keep-child')?.content?.[0]?.text === 'Bevar separat barn.', 'Nested selection lost unselected sibling'); results.push('nested partial selection preserves parent text, child suffix and unselected list siblings');
      await reset(); let cellFrom = 0; let otherCell = 0;
      editor.prosemirrorState.doc.descendants((node, pos) => { if (node.isText && node.text === 'Elev') cellFrom = pos; if (node.isText && node.text === 'Anna') otherCell = pos; });
      editor.transact(tr => { tr.setSelection(TextSelection.create(tr.doc, cellFrom + 1, cellFrom + 3)); });
      const cellSelection = review.capture(); const cellHunk = makeHunk('replace_selection', cellSelection.blockIds, [{ type: 'paragraph', content: 'XX' }], 0, JSON.parse(JSON.stringify(cellSelection)));
      await propose([cellHunk]); editor.transact(tr => { tr.insertText('!', otherCell + 4); }); await frame(); review.accept([cellHunk.id]);
      assert(editor.getBlock('table')?.type === 'table', 'Cell selection removed the table');
      assert(editor.prosemirrorState.doc.textContent.includes('EXXvHoldLokaleAnna!H1A.2BoH1A.2'), 'Cell selection changed unselected cells or failed to preserve their live edit'); results.push('partial table-cell selection preserves table structure and live edits in other cells');
      await reset(); const keptDrawing = JSON.parse(JSON.stringify(editor.getBlock('drawing')));
      editor.replaceBlocks(editor.document, [{ id: 'a', type: 'paragraph', content: 'FØR gammel' }, keptDrawing, { id: 'c', type: 'paragraph', content: 'slut EFTER' }] as never);
      const opaqueSelection = select('a', 4, 'c', 4);
      assert(opaqueSelection.blockIds.includes('drawing'), 'Selection omitted its wholly selected opaque block');
      const opaqueHunk = makeHunk('replace_selection', opaqueSelection.blockIds, [{ type: 'paragraph', content: 'Ny først.' }, keptDrawing, { type: 'paragraph', content: 'Ny sidst.' }], 0, JSON.parse(JSON.stringify(opaqueSelection)));
      await propose([opaqueHunk]); review.accept([opaqueHunk.id]);
      assert(editor.getBlock('drawing')?.props.reference === 'owned-drawing-42', 'Selected drawing lost stable ownership ID');
      assert(editor.prosemirrorState.doc.textContent === 'FØR Ny først.Ny sidst. EFTER', 'Opaque selection lost unselected edge text'); results.push('selection crossing opaque drawing retains its exact ID and ownership reference');
      await reset(); const high = makeHunk('insert_after', ['b'], [{ type: 'paragraph', content: 'Del 3' }], 3); const low = makeHunk('insert_after', ['b'], [{ type: 'paragraph', content: 'Del 1' }], 1); const middle = makeHunk('insert_after', ['b'], [{ type: 'paragraph', content: 'Del 2' }], 2); await propose([low, middle, high]);
      review.accept([high.id]); review.accept([low.id]); review.accept([middle.id]);
      assert(editor.prosemirrorState.doc.textContent.includes('Del 1Del 2Del 3'), 'Continuation sibling ordering failed'); results.push('continuation insertions stay ordered across out-of-order approval');
      await reset(); const originalDrawing = JSON.parse(JSON.stringify(editor.getBlock('drawing'))); const preserve = makeHunk('replace', ['drawing'], [originalDrawing]); await propose([preserve]); review.accept([preserve.id]);
      assert(editor.getBlock('drawing')?.props.reference === 'owned-drawing-42', 'Existing opaque block ID changed'); results.push('opaque original blocks retain IDs and references');
      await reset(); await propose(); return results;
    };
    (window as any).nativeAiQa = { ready: true, editor, review, ydoc, reset, propose, makeHunk, select, snapshot, runChecks, setOpen, setTarget, accept: (ids?: string[]) => review.accept(ids || apiRun!.hunks.map(hunk => hunk.id)), setTheme: (name: string, colors: Record<string, string> = {}) => { document.documentElement.dataset.theme = name; document.body.dataset.theme = name; for (const [key, value] of Object.entries(colors)) document.documentElement.style.setProperty('--' + key, value); } };
    void reset().then(() => propose()).then(() => setReady(true));
  }, [editor, review, ydoc]);
  return <OpenAiOnBlockProvider openOnBlock={blockId => { setTarget(review.capture(blockId)); revealPanel(); }}>
    <style>{layoutCss}</style>
    <style>{workspaceCss}</style>
    <style>{`.qa-app-header strong{font-size:17px}.qa-app-header span{font-size:12px;color:var(--muted,#63728b)}.qa-shell button{font-family:inherit;cursor:pointer}.qa-shell .notion-sidebar-create{width:100%;min-height:40px}.qa-shell .notion-page-list{list-style:none;padding:0;margin:0;display:grid;gap:8px;align-content:start}.qa-shell .notion-page-list button{border:0;background:none;color:var(--ink,#17243b);text-align:left;padding:8px;font-size:13px}.qa-shell .notion-page-list [aria-current]{color:var(--blue,#2452e8);font-weight:600}.qa-shell .notion-blocknote-host{border:0;border-radius:0}`}</style>
    <div className="lup notion-ide-shell"><div className="app">
    <header className="qa-app-header lup-header member-header"><strong>Dokumenter</strong><span>Fælles teori · Databaser</span></header>
    <main className="body qa-shell" id="main-content">
      <div ref={setWorkspace} className={'notion-workspace' + (sidebarOpen ? ' sidebar-open' : '') + (focused ? ' notion-focus-mode' : '')} data-sidebar-ready="true" data-focus-mode={String(focused)} data-edge-to-edge="true">
        {sidebarOpen && sidebarMobile && <button id="notion-sidebar-backdrop" className="notion-sidebar-backdrop" type="button" tabIndex={-1} aria-label="Luk sidemenu" onClick={() => setSidebarOpen(false)} />}
        <aside id="notion-sidebar" className="notion-sidebar" hidden={!sidebarOpen} role={sidebarOpen && sidebarMobile ? 'dialog' : undefined} aria-modal={sidebarOpen && sidebarMobile ? true : undefined} aria-label="Sideoversigt" tabIndex={-1}>
          <div className="notion-sidebar-heading"><div><span className="notion-sidebar-eyebrow">Teori</span><h2>Sider</h2></div><button type="button" className="notion-icon-button notion-sidebar-close" aria-label="Skjul sidemenu" onClick={() => setSidebarOpen(false)}>×</button></div>
          <div className="notion-page-search"><label className="notion-visually-hidden" htmlFor="notion-page-search">Find en side</label><input id="notion-page-search" type="search" placeholder="Find en side…" /></div>
          <button type="button" className="notion-sidebar-create">Ny side</button>
          <div className="notion-page-list-heading"><span>Alle sider</span><span>4</span></div>
          <nav className="notion-page-list" aria-label="Dokumentsider"><button type="button">SQL og relationer</button><button type="button" aria-current="page">Normalisering af databaser</button><button type="button">Praktiske øvelser</button><button type="button">Faglige begreber</button></nav>
          <details className="notion-sidebar-tools"><summary>Notesbogsværktøjer</summary><button type="button">Opdatér sidelisten</button></details>
          <div className="notion-sidebar-resize" role="separator" aria-label="Sideoversigtens bredde" aria-orientation="vertical" tabIndex={0} />
        </aside>
        <article className="notion-document" aria-label="Dokumenteditor">
          <div className="notion-document-toolbar">
            <h1 className="notion-visually-hidden">Normalisering af databaser</h1>
            <div className="notion-toolbar-nav"><button type="button" className="notion-sidebar-toggle" aria-expanded={sidebarOpen && !focused} aria-controls="notion-sidebar" aria-label={sidebarOpen && !focused ? 'Skjul sidemenu' : 'Vis sidemenu'} onClick={() => { if (focused && workspace) sidebar.setFocus(workspace, false); setSidebarOpen(focused || !sidebarOpen); }}><svg className="ui-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path d="M3 3h18v18H3zM9 3v18m5-12-3 3 3 3" /></svg><span>Sider</span></button><button type="button" className="notion-focus-toggle" aria-pressed={focused} aria-label={focused ? 'Afslut fokus' : 'Fokus'} onClick={() => workspace && sidebar.toggleFocus(workspace)}><svg className="ui-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path d="M3 4h18v13H3zm9 13v5m-5 0h10M8 8l8 5V8l-8 5V8" /></svg><span>{focused ? 'Afslut fokus' : 'Fokus'}</span></button></div>
            <form className="notion-title-form" onSubmit={event => event.preventDefault()}><label className="notion-visually-hidden" htmlFor="notion-title">Sidens titel</label><input id="notion-title" className="notion-title" defaultValue="Normalisering af databaser" maxLength={200} placeholder="Sidens titel" /></form>
            <div className="notion-toolbar-actions"><span className="notion-save-status saved" role="status"><i aria-hidden="true" /><span className="notion-save-label">Gemt i skyen</span></span><span className="notion-ai-toggle-slot" /><button type="button" className="action notion-publish-button" aria-label="Udgiv til LUP">Udgiv</button><details className="notion-document-more"><summary aria-label="Flere sidehandlinger"><svg className="ui-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><path d="M5 11h.01v2H5zm7 0h.01v2H12zm7 0h.01v2H19z" /></svg><span>Mere</span></summary><div className="notion-document-menu"><p className="notion-publication-status">Kladde</p><nav className="breadcrumbs notion-toolbar-breadcrumbs" aria-label="Brødkrummer"><a href="/notion">Notesbøger</a><span>›</span><a href="/notion">Undervisning</a><span>›</span><span>Teori</span></nav><button type="button">Præsenter</button><button type="button">Kopiér link</button><a href="/notion/connections">Graf for kollektionen</a><button type="button">Ny underside</button><button type="button">Opdatér sideoversigt</button></div></details></div>
          </div>
          <div className="notion-blocknote-host" data-notion-theme={theme}><div className="native-ai-workspace" data-ai-open={open && !focused}><div className="native-ai-document"><NativeAiLauncher slot={aiLauncherSlot} buttonRef={launcher} panelId="qa-ai-drawer" enabled visible={open && !focused} onToggle={() => { if (open && !focused) closePanel(); else { setTarget(review.capture()); revealPanel(); } }} /><BlockNoteView editor={editor} editable theme={theme} formattingToolbar={false} sideMenu={false} emojiPicker={false}><SideMenuController sideMenu={props => <SideMenu {...props} dragHandleMenu={NotionDragHandleMenu} />} /></BlockNoteView></div>{ready && workspace && <NativeAiPanel key={revision} panelId="qa-ai-drawer" layoutRoot={workspace} review={review} pageId={pageId} pageTitle="Normalisering af databaser" enabled open={open && !focused} target={target} onClose={closePanel} onReveal={revealPanel} onCapture={() => setTarget(review.capture())} getToken={async () => 'fixture-only'} />}</div></div>
        </article>
        <div className="notion-ai-slot" />
      </div>
    </main>
    <footer className="footer">LUP · Fælles undervisningsplatform</footer>
    </div></div>
  </OpenAiOnBlockProvider>;
}
document.body.style.cssText = 'margin:0;padding:0;background:var(--bg,#f4f6fa);font-family:Arial,sans-serif';
createRoot(document.getElementById('root')!).render(<App />);
