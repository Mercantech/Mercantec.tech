import React, { useEffect, useId, useRef, useState } from 'react';
import { createReactBlockSpec } from '@blocknote/react';
import {
  formatMermaidRenderError,
  MERMAID_RENDER_CONFIG,
  normalizeMermaidSource,
  prepareMermaidSource,
} from './mermaid-utils';
import { readMermaidPalette, mermaidThemeVariables, prepareMermaidSvg } from './mermaid-theme';
import { useViewport } from './use-viewport';

/** The sample uses the current platform palette, including seasonal themes. */
export const DEFAULT_MERMAID_SOURCE = `flowchart TB
  subgraph Lag[" "]
    E2E["E2E (End-to-End)"]
    INT["Integration"]
    UNIT["Unit"]
  end
  E2E --- INT
  INT --- UNIT`;

type MermaidApi = {
  initialize: (config: Record<string, unknown>) => void;
  render: (id: string, source: string) => Promise<{ svg: string }>;
};
type Palette = ReturnType<typeof readMermaidPalette>;

let mermaidPromise: Promise<MermaidApi> | null = null;
let renderQueue = Promise.resolve();
let renderSequence = 0;

function loadMermaid() {
  mermaidPromise ??= import('mermaid')
    .then(mod => (mod.default ?? mod) as MermaidApi)
    .catch(error => { mermaidPromise = null; throw error; });
  return mermaidPromise;
}

function renderMermaid(source: string, palette: Palette, cancelled: () => boolean) {
  // Mermaid keeps global configuration. Keep initialize and the entire render in one queue.
  const request = renderQueue.then(async () => {
    if (cancelled()) return null;
    const mermaid = await loadMermaid();
    if (cancelled()) return null;
    mermaid.initialize({
      ...MERMAID_RENDER_CONFIG,
      theme: 'base',
      themeVariables: mermaidThemeVariables(palette),
    });
    const id = `notion-mmd-${Date.now().toString(36)}-${++renderSequence}`;
    try {
      const { svg } = await mermaid.render(id, source);
      return cancelled() ? null : prepareMermaidSvg(svg, palette);
    } finally {
      // Older Mermaid versions can leave their temporary drawing container after a parse error.
      document.getElementById('d' + id)?.remove();
    }
  });
  renderQueue = request.then(() => {}, () => {});
  return request;
}

function MermaidPreview({ source, showSourceOnError }: { source: string; showSourceOnError: boolean }) {
  const [container,nearViewport]=useViewport<HTMLDivElement>();
  const probe = useRef<HTMLSpanElement>(null);
  const rendered = useRef('');
  const [palette, setPalette] = useState<Palette | null>(null);
  const [svgMarkup, setSvgMarkup] = useState('');
  const [error, setError] = useState('');
  const [repairNote, setRepairNote] = useState('');
  const [rendering, setRendering] = useState(false);

  useEffect(() => {
    const element = probe.current;
    if (!element) return;
    const sync = () => {
      const next = readMermaidPalette(element);
      setPalette(previous => JSON.stringify(previous) === JSON.stringify(next) ? previous : next);
    };
    const observer = new MutationObserver(sync);
    for (let ancestor = element.parentElement; ancestor; ancestor = ancestor.parentElement) {
      observer.observe(ancestor, {
        attributes: true,
        attributeFilter: ['class', 'style', 'data-theme', 'data-notion-theme', 'data-color-scheme'],
      });
    }
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    media.addEventListener('change', sync);
    sync();
    return () => { observer.disconnect(); media.removeEventListener('change', sync); };
  }, []);

  useEffect(() => {
    const { source: code, repaired } = prepareMermaidSource(source);
    if (!code) {
      rendered.current='';setSvgMarkup(''); setError(''); setRepairNote(''); setRendering(false);
      return;
    }
    if (!palette || !nearViewport) { setRendering(false); return; }
    const signature=JSON.stringify([source,palette]);
    if(rendered.current===signature)return;
    let cancelled = false;
    const timer = setTimeout(() => {
      setRendering(true);
      void (async () => {
        try {
          return { markup: await renderMermaid(normalizeMermaidSource(source), palette, () => cancelled), corrected: false };
        } catch (error) {
          if (!repaired || cancelled) throw error;
          return { markup: await renderMermaid(code, palette, () => cancelled), corrected: true };
        }
      })().then(({ markup, corrected }) => {
        if (cancelled || markup === null) return;
        setSvgMarkup(markup);
        rendered.current=signature;
        setError('');
        setRepairNote(corrected ? 'Forhåndsvisningen udelader fri tekst efter en pil. Din Mermaid-kode er bevaret.' : '');
      }).catch(error => {
        if (cancelled) return;
        rendered.current='';
        setSvgMarkup('');
        setRepairNote('');
        setError(formatMermaidRenderError(error));
      }).finally(() => { if (!cancelled) setRendering(false); });
    }, 160);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [source, palette, nearViewport]);

  return <>
    <span ref={probe} className="notion-visually-hidden" aria-hidden="true" />
    <div ref={container} className="notion-mermaid-host" role="region" aria-label="Mermaid-diagram" aria-busy={rendering}>
      {svgMarkup && <div className="notion-mermaid-svg" role="img" aria-label="Diagram" dangerouslySetInnerHTML={{ __html: svgMarkup }} />}
      {rendering && !svgMarkup && <p className="notion-block-hint" role="status">Tegner diagrammet…</p>}
      {!nearViewport && !svgMarkup && source.trim() && <p className="notion-block-hint">Diagrammet indlæses, når det vises.</p>}
    </div>
    {repairNote && <p className="notion-mermaid-hint">{repairNote}</p>}
    {error && <p className="notion-mermaid-error" role="alert">{error}</p>}
    {error && showSourceOnError && <pre className="notion-mermaid-source-fallback"><code className="language-mermaid">{source}</code></pre>}
  </>;
}

function MermaidDiagramBlock({ source, editable, onChange }: {
  source: string;
  editable: boolean;
  onChange: (value: string) => void;
}) {
  const [draft, setDraft] = useState(source);
  const [editing, setEditing] = useState(editable && !source.trim());
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'error'>('idle');
  const copyTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const draftRef = useRef(source);
  const onChangeRef = useRef(onChange);
  const alive = useRef(true);
  const area = useRef<HTMLTextAreaElement>(null);
  const diagramButton = useRef<HTMLButtonElement>(null);
  const sourceId = useId();
  const previewId = useId();
  onChangeRef.current = onChange;

  useEffect(() => {
    if (source === draftRef.current) return;
    draftRef.current = source;
    setDraft(source);
  }, [source]);

  useEffect(() => {
    alive.current = true;
    return () => { alive.current = false; clearTimeout(copyTimer.current); };
  }, []);

  useEffect(() => {
    if (editing && editable) area.current?.focus();
  }, [editing, editable]);

  const update = (value: string) => {
    draftRef.current = value;
    setDraft(value);
    // Save into the document immediately. Only the expensive SVG preview is debounced.
    onChangeRef.current(value);
  };

  const showDiagram = () => setEditing(false);
  const copy = async () => {
    let copied = false;
    try { await navigator.clipboard.writeText(draftRef.current); copied = true; }
    catch { /* Clipboard permission errors are reported beside the action. */ }
    if (!alive.current) return;
    setCopyState(copied ? 'copied' : 'error');
    clearTimeout(copyTimer.current);
    copyTimer.current = setTimeout(() => setCopyState('idle'), 1600);
  };

  return <div className={`notion-mermaid-block${editing ? ' is-editing' : ''}`} contentEditable={false} data-mermaid="true" aria-label="Mermaid-diagram">
    <div className="notion-mermaid-toolbar">
      <span className="notion-mermaid-language">Mermaid</span>
      {editable && <div className="notion-mermaid-tabs" role="group" aria-label="Diagramvisning">
        <button ref={diagramButton} type="button" aria-pressed={!editing} aria-controls={previewId} onClick={showDiagram}>Diagram</button>
        <button type="button" aria-pressed={editing} aria-controls={sourceId} onClick={() => setEditing(true)}>Kode</button>
      </div>}
      <button type="button" className={`notion-mermaid-copy${copyState === 'copied' ? ' is-copied' : ''}`} aria-label="Kopiér Mermaid-kode" onClick={() => { void copy(); }}>
        {copyState === 'copied' ? 'Kopieret' : copyState === 'error' ? 'Fejl' : 'Kopiér'}
      </button>
      <span className="notion-mermaid-status notion-visually-hidden" role="status">
        {copyState === 'copied' ? 'Mermaid-koden er kopieret.' : copyState === 'error' ? 'Mermaid-koden kunne ikke kopieres.' : ''}
      </span>
    </div>
    <div id={sourceId} className="notion-mermaid-editor" hidden={!editing || !editable}>
      <label className="notion-mermaid-source-label" htmlFor={sourceId + '-input'}>Mermaid-kode</label>
      <textarea
        id={sourceId + '-input'}
        ref={area}
        value={draft}
        readOnly={!editable}
        spellCheck={false}
        wrap="off"
        rows={Math.min(14, Math.max(4, draft.split('\n').length + 1))}
        onChange={event => update(event.target.value)}
        onKeyDown={event => {
          event.stopPropagation();
          if (event.key !== 'Escape') return;
          event.preventDefault();
          showDiagram();
          diagramButton.current?.focus();
        }}
        placeholder={'flowchart TB\n  A --> B'}
      />
    </div>
    <div
      id={previewId}
      className="notion-mermaid-surface"
      onDoubleClick={() => { if (editable) setEditing(true); }}
    >
      <MermaidPreview source={draft} showSourceOnError={!editable} />
      {!draft.trim() && <p className="notion-block-hint">{editable ? 'Vælg Kode for at skrive Mermaid-kode.' : 'Diagrammet er tomt.'}</p>}
    </div>
  </div>;
}

export const createMermaidDiagram = createReactBlockSpec({
  type: 'mermaidDiagram',
  propSchema: {
    schemaVersion: { default: 1 },
    source: { default: DEFAULT_MERMAID_SOURCE },
  },
  content: 'none',
}, {
  render: ({ block, editor }) => <MermaidDiagramBlock
    source={block.props.source}
    editable={editor.isEditable}
    onChange={source => {
      const live = editor.getBlock(block.id);
      if (live?.type === 'mermaidDiagram' && source !== live.props.source) {
        editor.updateBlock(live, { props: { source } });
      }
    }}
  />,
  toExternalHTML: ({ block }) => <pre><code className="language-mermaid">{block.props.source}</code></pre>,
});
