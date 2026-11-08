import React, { createContext, useContext, useEffect, useMemo, useRef } from 'react';
import type { EmojiAnalysis, EmojiMetadata, PlatformEmoji } from './emoji-picker';
import { emojiTitleCatalog, splitEmojiTitle } from './emoji-title.mjs';

export const EmojiCatalog = createContext<PlatformEmoji[]>([]);
export const EmojiReload = createContext<() => void>(() => {});
export const EmojiCreate = createContext<((file: File, name: string, signal: AbortSignal, metadata: EmojiMetadata) => Promise<PlatformEmoji>) | undefined>(undefined);
export const EmojiAnalyze = createContext<((file: File, signal: AbortSignal) => Promise<EmojiAnalysis>) | undefined>(undefined);

/** Titles render with the existing runtime catalog; no document nodes or access tickets are stored. */
export function EmojiText({ text }: { text: string }) {
  const emojis = useContext(EmojiCatalog);
  const catalog = useMemo(() => emojiTitleCatalog(emojis), [emojis]);
  const parts = useMemo(() => splitEmojiTitle(text, catalog), [text, catalog]);
  return <span className="notion-emoji-text">{parts.map((part, index) => part.emoji
    ? <CustomEmojiImage key={index} emoji={part.emoji} label={`:${part.emoji.name}:`} />
    : <React.Fragment key={index}>{part.text}</React.Fragment>)}</span>;
}

/** Resolve images from the current catalog so stored blocks never contain access tickets. */
export function CustomEmojiImage({ emoji, label }: { emoji: PlatformEmoji; label: string }) {
  const reload = useContext(EmojiReload);
  const tried = useRef(false);
  useEffect(() => { tried.current = false; }, [emoji.contentUrl]);
  return <img className="notion-custom-emoji" src={emoji.contentUrl} alt={emoji.altText?.trim() || label} title={label} draggable={false} contentEditable={false} onError={() => { if (tried.current) return; tried.current = true; reload(); }} />;
}
