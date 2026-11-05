import React, { useEffect, useRef, useState } from 'react';
import { createReactBlockSpec } from '@blocknote/react';
import {
  formatMermaidRenderError,
  MERMAID_RENDER_CONFIG,
  prepareMermaidSource,
  stripScriptsFromSvg,
} from './mermaid-utils';

/** Default sample matches the pyramid / test-layer diagram teachers often paste from Notion. */
export const DEFAULT_MERMAID_SOURCE = `flowchart TB
  subgraph Lag[" "]
    E2E["E2E (End-to-End)"]
    INT["Integration"]
    UNIT["Unit"]
  end
  E2E --- INT
  INT --- UNIT

  style E2E fill:#e1f5fe
  style INT fill:#fff3e0
  style UNIT fill:#e8f5e9`;

type MermaidApi = {
  initialize: (config: Record<string, unknown>) => void;
  render: (id: string, source: string) => Promise<{ svg: string }>;
};

let mermaidPromise: Promise<MermaidApi> | null = null;

function loadMermaid() {
  if (!mermaidPromise) {
    mermaidPromise = import('mermaid').then((mod) => {
      const mermaid = (mod.default ?? mod) as MermaidApi;
      mermaid.initialize({ ...MERMAID_RENDER_CONFIG, theme: 'neutral' });
      return mermaid;
    });
  }
  return mermaidPromise;
}

function readEditorTheme(): 'light' | 'dark' {
  const host = document.querySelector('.notion-blocknote-host') as HTMLElement | null;
  return host?.dataset.notionTheme === 'dark' ? 'dark' : 'light';
}

function MermaidPreview({
  source,
  onSourceRepaired,
}: {
  source: string;
  onSourceRepaired?: (fixed: string) => void;
}) {
  const [svgMarkup, setSvgMarkup] = useState('');
  const [error, setError] = useState('');
  const [repairNote, setRepairNote] = useState('');
  const [theme, setTheme] = useState<'light' | 'dark'>(readEditorTheme);
  const onRepairedRef = useRef(onSourceRepaired);
  onRepairedRef.current = onSourceRepaired;

  useEffect(() => {
    const sync = () => setTheme(readEditorTheme());
    const observer = new MutationObserver(sync);
    const root = document.querySelector('.notion-blocknote-host');
    if (root) observer.observe(root, { attributes: true, attributeFilter: ['data-notion-theme'] });
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const { source: code, repaired } = prepareMermaidSource(source);
    if (!code) {
      setSvgMarkup('');
      setError('');
      setRepairNote('');
      return;
    }
    let cancelled = false;
    const renderId = `notion-mmd-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
    void (async () => {
      try {
        const mermaid = await loadMermaid();
        mermaid.initialize({
          ...MERMAID_RENDER_CONFIG,
          theme: theme === 'dark' ? 'dark' : 'neutral',
        });
        const { svg } = await mermaid.render(renderId, code);
        if (cancelled) return;
        setSvgMarkup(stripScriptsFromSvg(svg));
        setError('');
        if (repaired) {
          setRepairNote('Fjernede fri tekst efter en kant; koden er rettet automatisk.');
          onRepairedRef.current?.(code);
        } else {
          setRepairNote('');
        }
      } catch (err) {
        if (!cancelled) {
          setSvgMarkup('');
          setRepairNote('');
          setError(formatMermaidRenderError(err));
        }
      }
    })();
    return () => { cancelled = true; };
  }, [source, theme]);

  return <>
    <div className="notion-mermaid-host">
      {svgMarkup ? (
        <div
          className="notion-mermaid-svg"
          role="img"
          aria-label="Diagram"
          dangerouslySetInnerHTML={{ __html: svgMarkup }}
        />
      ) : null}
    </div>
    {repairNote && <p className="notion-mermaid-hint">{repairNote}</p>}
    {error && <p className="notion-mermaid-error" role="alert">{error}</p>}
  </>;
}

function MermaidDiagramBlock({
  source,
  editable,
  onChange,
}: {
  source: string;
  editable: boolean;
  onChange: (value: string) => void;
}) {
  const [draft, setDraft] = useState(source);
  const [editing, setEditing] = useState(editable && !source.trim());
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const area = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    setDraft(source);
  }, [source]);

  useEffect(() => () => { if (timer.current) clearTimeout(timer.current); }, []);

  useEffect(() => {
    if (editing) area.current?.focus();
  }, [editing]);

  const update = (value: string) => {
    setDraft(value);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => onChange(value), 400);
  };

  const finish = () => {
    onChange(draft);
    if (draft.trim()) setEditing(false);
  };

  return <div
    className={`notion-mermaid-block${editing ? ' is-editing' : ''}`}
    contentEditable={false}
    data-mermaid="true"
    aria-label="Diagram"
  >
    {editable && editing ? (
      <label className="notion-mermaid-editor">
        <span className="notion-visually-hidden">Mermaid-kode</span>
        <textarea
          ref={area}
          value={draft}
          spellCheck={false}
          rows={Math.min(16, Math.max(6, draft.split('\n').length + 1))}
          onChange={(event) => update(event.target.value)}
          onBlur={finish}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              event.preventDefault();
              finish();
            }
          }}
          placeholder={'flowchart TB\n  A --> B'}
        />
        <span className="notion-mermaid-hint">Esc eller klik udenfor gemmer koden</span>
      </label>
    ) : (
      <div
        className="notion-mermaid-surface"
        role={editable ? 'button' : undefined}
        tabIndex={editable ? 0 : undefined}
        title={editable ? 'Dobbeltklik for at redigere Mermaid-kode' : undefined}
        onDoubleClick={() => { if (editable) setEditing(true); }}
        onKeyDown={(event) => {
          if (!editable) return;
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            setEditing(true);
          }
        }}
      >
        <MermaidPreview
          source={draft}
          onSourceRepaired={(fixed) => {
            if (fixed === draft) return;
            setDraft(fixed);
            onChange(fixed);
          }}
        />
        {!draft.trim() && <p className="notion-block-hint">Dobbeltklik for at skrive Mermaid-kode.</p>}
      </div>
    )}
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
    onChange={(source) => {
      if (source !== block.props.source) editor.updateBlock(block, { props: { source } });
    }}
  />,
  toExternalHTML: ({ block }) => <pre><code className="language-mermaid">{block.props.source}</code></pre>,
});
