import React, { useEffect, useRef, useState } from 'react';
import type { DefaultReactGridSuggestionItem, GridSuggestionMenuProps } from '@blocknote/react';

export type EmojiSuggestionItem = DefaultReactGridSuggestionItem & { label?: string; description?: string };

/** The controller still owns query removal and keyboard navigation. Only the view is replaced. */
export function EmojiSuggestionMenu({ items, loadingState, selectedIndex, onItemClick, columns, onAdd }: GridSuggestionMenuProps<EmojiSuggestionItem> & { onAdd?: () => void }) {
  const grid = useRef<HTMLDivElement>(null);
  const [hovered, setHovered] = useState<number | null>(null);
  const active = items[hovered ?? selectedIndex ?? 0];
  const loading = loadingState !== 'loaded';

  useEffect(() => { setHovered(null); }, [items]);
  useEffect(() => {
    const container = grid.current;
    const selected = selectedIndex === undefined ? null : container?.querySelector<HTMLElement>(`#bn-suggestion-menu-item-${selectedIndex}`);
    if (!container || !selected) return;
    // Scroll only the results, keeping the document and its caret where they were.
    const outer = container.getBoundingClientRect(), inner = selected.getBoundingClientRect();
    if (inner.top < outer.top) container.scrollTop -= outer.top - inner.top;
    else if (inner.bottom > outer.bottom) container.scrollTop += inner.bottom - outer.bottom;
  }, [selectedIndex, items]);

  return <section className="notion-emoji-suggestion-menu" aria-label="Emojiforslag">
    <header className="notion-emoji-suggestion-header">
      <strong>Emojis</strong>
      {onAdd && <button type="button" className="notion-emoji-suggestion-add" aria-label="Tilføj emoji" title="Tilføj emoji" onMouseDown={event => event.preventDefault()} onClick={onAdd}>+</button>}
    </header>
    <div ref={grid} id="bn-suggestion-menu" className="notion-emoji-suggestion-grid" role="listbox" aria-label="Emojis" aria-busy={loading} style={{ gridTemplateColumns: `repeat(${columns},minmax(0,1fr))` }} onMouseLeave={() => setHovered(null)}>
      {items.map((item, index) => <button key={item.id} id={`bn-suggestion-menu-item-${index}`} type="button" role="option" tabIndex={-1} className="notion-emoji-suggestion-option" aria-label={item.label || item.id} aria-selected={index === selectedIndex} title={item.description ? `${item.label || item.id} · ${item.description}` : item.label || item.id} onMouseDown={event => event.preventDefault()} onMouseEnter={() => setHovered(index)} onClick={() => onItemClick?.(item)}>{item.icon}</button>)}
      {items.length === 0 && <p className="notion-emoji-suggestion-status" role="status">{loading ? 'Henter emojis…' : 'Ingen emojis matcher.'}</p>}
    </div>
    <footer className="notion-emoji-suggestion-footer" aria-hidden="true">
      <span className="notion-emoji-suggestion-label">{active?.label || active?.id || 'Vælg en emoji'}</span>
      <span className="notion-emoji-suggestion-count">{loading ? '…' : items.length}</span>
    </footer>
  </section>;
}
