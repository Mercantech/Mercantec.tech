/** Platform emoji metadata is shared by the picker, colon menu and typed shortcodes. */

export const emojiCategories = [
  { id: 'all', label: 'Alle' },
  { id: 'custom', label: 'Egne' },
  { id: 'smileys', label: 'Smileys' },
  { id: 'gestures', label: 'Hænder' },
  { id: 'symbols', label: 'Symboler' },
  { id: 'learning', label: 'Læring' },
  { id: 'technology', label: 'Teknik' },
  { id: 'everyday', label: 'Hverdag' },
];

function normalizeSearch(value) {
  return String(value ?? '').toLowerCase().normalize('NFD').replace(/\p{M}/gu, '')
    .replace(/æ/g, 'ae').replace(/ø/g, 'o').replace(/[:_-]+/g, ' ').replace(/\s+/g, ' ').trim();
}

export function filterCustomEmojis(emojis, query, category = 'all') {
  const words = normalizeSearch(query).split(' ').filter(Boolean);
  const list = Array.isArray(emojis) ? emojis : [];
  return list.filter((item) => {
    if (!item || typeof item.name !== 'string') return false;
    if (category === 'custom' ? item.isStandard === true : category !== 'all' && !(item.isStandard === true && item.category === category)) return false;
    const keywords = Array.isArray(item.keywords) ? item.keywords.filter(word => typeof word === 'string') : [];
    const text = normalizeSearch([item.name, ...keywords].join(' '));
    return words.every(word => text.includes(word));
  });
}

export function expandEmojiShortcodes(content, emojis) {
  if (!Array.isArray(content) || !Array.isArray(emojis) || emojis.length === 0) return { content, changed: false };
  const byName = new Map();
  for (const emoji of emojis) {
    if (emoji && typeof emoji.id === 'string' && typeof emoji.name === 'string') byName.set(emoji.name.toLowerCase(), emoji);
  }
  if (byName.size === 0) return { content, changed: false };
  return expandParts(content, byName);
}

function expandParts(parts, byName) {
  const next = [];
  let changed = false;
  for (const part of parts) {
    if (typeof part === 'string') {
      const pieces = splitPlain(part, {}, byName);
      if (pieces) { changed = true; next.push(...pieces); }
      else next.push(part);
      continue;
    }
    if (!part || typeof part !== 'object') { next.push(part); continue; }
    if (part.type === 'text' && typeof part.text === 'string') {
      if (part.styles && part.styles.code) { next.push(part); continue; }
      const pieces = splitPlain(part.text, part.styles || {}, byName);
      if (pieces) { changed = true; next.push(...pieces); }
      else next.push(part);
      continue;
    }
    if (part.type === 'link' && Array.isArray(part.content)) {
      const inner = expandParts(part.content, byName);
      if (inner.changed) { changed = true; next.push({ ...part, content: inner.content }); }
      else next.push(part);
      continue;
    }
    next.push(part);
  }
  return { content: changed ? next : parts, changed };
}

function splitPlain(text, styles, byName) {
  const re = /:([a-z0-9_]{2,32}):/gi;
  let last = 0;
  let changed = false;
  const pieces = [];
  for (const match of text.matchAll(re)) {
    const emoji = byName.get(match[1].toLowerCase());
    if (!emoji || match.index === undefined) continue;
    changed = true;
    const before = text.slice(last, match.index);
    if (before) pieces.push({ type: 'text', text: before, styles: { ...styles } });
    pieces.push({ type: 'customEmoji', props: { emojiId: emoji.id, name: emoji.name } });
    last = match.index + match[0].length;
  }
  if (!changed) return null;
  const after = text.slice(last);
  if (after) pieces.push({ type: 'text', text: after, styles: { ...styles } });
  return pieces;
}
