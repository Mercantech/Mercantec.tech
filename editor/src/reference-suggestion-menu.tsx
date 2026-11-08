import React, { useEffect, useRef } from 'react';
import type { DefaultReactSuggestionItem, SuggestionMenuProps } from '@blocknote/react';
import { EmojiText } from './emoji-context';

export type ReferenceSuggestionItem = DefaultReactSuggestionItem;

/** The BlockNote controller still removes the query and handles keyboard selection. */
export function ReferenceSuggestionMenu<T extends ReferenceSuggestionItem>({ items, loadingState, selectedIndex, onItemClick }: SuggestionMenuProps<T>) {
  const menu = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const outer = menu.current;
    const selected = selectedIndex === undefined ? null : outer?.querySelector<HTMLElement>(`#bn-suggestion-menu-item-${selectedIndex}`);
    if (!outer || !selected) return;
    const container = outer.getBoundingClientRect(), item = selected.getBoundingClientRect();
    if (item.top < container.top) outer.scrollTop -= container.top - item.top;
    else if (item.bottom > container.bottom) outer.scrollTop += item.bottom - container.bottom;
  }, [selectedIndex, items]);
  return <div ref={menu} id="bn-suggestion-menu" className="bn-suggestion-menu notion-reference-suggestions" role="listbox" aria-label="Indhold i LUP" aria-busy={loadingState !== 'loaded'}>
    {items.map((item, index) => <button key={index} id={`bn-suggestion-menu-item-${index}`} type="button" role="option" tabIndex={-1} aria-selected={index === selectedIndex} className="notion-reference-suggestion" onMouseDown={event => event.preventDefault()} onClick={() => onItemClick?.(item)}>
      <strong><EmojiText text={item.title} /></strong>{item.subtext && <small><EmojiText text={item.subtext} /></small>}
    </button>)}
    {items.length === 0 && <p role="status">{loadingState === 'loaded' ? 'Ingen tilgængelige resultater.' : 'Søger…'}</p>}
  </div>;
}
