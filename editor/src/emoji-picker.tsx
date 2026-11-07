import React, { useEffect, useMemo, useRef, useState } from 'react';
import { emojiCategories, filterCustomEmojis } from './custom-emoji.mjs';

export type PlatformEmoji = {
  id: string;
  name: string;
  contentUrl: string;
  isStandard?: boolean;
  category?: string;
  keywords?: string[];
};

export function EmojiPicker({ id, emojis, onPick, onClose }: {
  id: string;
  emojis: PlatformEmoji[];
  onPick: (emoji: PlatformEmoji) => void;
  onClose: () => void;
}) {
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('all');
  const [preview, setPreview] = useState<PlatformEmoji | null>(null);
  const search = useRef<HTMLInputElement>(null);
  const grid = useRef<HTMLDivElement>(null);
  const results = useMemo(() => filterCustomEmojis(emojis, query, category) as PlatformEmoji[], [emojis, query, category]);
  const activePreview = preview && results.some(item => item.id === preview.id) ? preview : null;

  useEffect(() => { search.current?.focus({ preventScroll: true }); }, []);
  useEffect(() => { grid.current?.scrollTo({ top: 0 }); }, [query, category]);

  const onKeyDown = (event: React.KeyboardEvent) => {
    // Keep search and grid shortcuts out of BlockNote's document shortcuts.
    event.stopPropagation();
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
    }
  };
  const moveInGrid = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    const buttons = grid.current?.querySelectorAll<HTMLButtonElement>('button');
    if (!buttons?.length) return;
    const columns = getComputedStyle(grid.current!).gridTemplateColumns.split(' ').length;
    let next: number;
    switch (event.key) {
      case 'ArrowRight': next = Math.min(buttons.length - 1, index + 1); break;
      case 'ArrowLeft': next = Math.max(0, index - 1); break;
      case 'ArrowDown': next = Math.min(buttons.length - 1, index + columns); break;
      case 'ArrowUp': next = index - columns; break;
      case 'Home': next = 0; break;
      case 'End': next = buttons.length - 1; break;
      default: return;
    }
    event.preventDefault();
    if (next < 0) search.current?.focus();
    else buttons[next].focus({ preventScroll: true });
    if (next >= 0) buttons[next].scrollIntoView({ block: 'nearest', inline: 'nearest' });
  };
  const reset = () => { setQuery(''); setCategory('all'); search.current?.focus(); };

  return <section id={id} className="notion-emoji-panel" aria-label="Platformens emojis" onKeyDown={onKeyDown}>
    <div className="notion-emoji-search-row">
      <label className="notion-emoji-search">
        <svg className="notion-emoji-search-icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 4.5 4.5"/></svg>
        <input ref={search} type="search" value={query} onChange={event => setQuery(event.target.value)} aria-label="Søg efter emoji" placeholder="Søg navn eller fx glad, raket…" autoComplete="off" maxLength={100} onKeyDown={event => {
          if (event.key === 'ArrowDown') {
            const first = grid.current?.querySelector<HTMLButtonElement>('button');
            if (first) { event.preventDefault(); first.focus({ preventScroll: true }); }
          }
        }} />
      </label>
      <button type="button" className="notion-emoji-close" onClick={onClose} aria-label="Luk emoji-vælger" title="Luk (Esc)">×</button>
    </div>
    <div className="notion-emoji-categories" role="group" aria-label="Emojikategorier">
      {emojiCategories.map(item => <button key={item.id} type="button" aria-pressed={category === item.id} onClick={() => setCategory(item.id)}>{item.label}</button>)}
    </div>
    <div className="notion-emoji-results" role="status" aria-live="polite">{results.length} {results.length === 1 ? 'emoji' : 'emojis'}</div>
    <div ref={grid} className="notion-emoji-grid" role="group" aria-label="Emojis" onMouseLeave={() => setPreview(null)}>
      {results.map((item, index) => <button key={item.id} type="button" className="notion-emoji-option" title={`:${item.name}:`} aria-label={`:${item.name}:`} onClick={() => onPick(item)} onFocus={() => setPreview(item)} onMouseEnter={() => setPreview(item)} onKeyDown={event => moveInGrid(event, index)}><img src={item.contentUrl} alt="" draggable={false} loading="lazy" /></button>)}
      {results.length === 0 && <div className="notion-emoji-empty">
        <p>{emojis.length === 0 ? 'Ingen emojis endnu.' : category === 'custom' && !query ? 'Ingen egne emojis endnu.' : 'Ingen emojis matcher dine filtre.'}</p>
        {query || category !== 'all' ? <button type="button" onClick={reset}>Nulstil filtre</button> : <a href="/emojis">Åbn biblioteket</a>}
      </div>}
    </div>
    <div className="notion-emoji-preview" aria-hidden="true">
      {activePreview ? <><img src={activePreview.contentUrl} alt="" /><span>:{activePreview.name}:</span><small>{activePreview.isStandard ? 'Standard' : 'Egen emoji'}</small></> : <span>Vælg en emoji · Esc lukker</span>}
    </div>
  </section>;
}
