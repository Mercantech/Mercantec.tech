/** A rendering-only view of title shortcodes. Stored titles and references stay plain text. */
const catalogs = new WeakMap();

export function emojiTitleCatalog(emojis) {
  if (!Array.isArray(emojis)) return new Map();
  let indexed = catalogs.get(emojis);
  if (!indexed) {
    indexed = new Map();
    for (const emoji of emojis) {
      if (typeof emoji?.name !== 'string' || typeof emoji?.contentUrl !== 'string') continue;
      const name = emoji.name.toLowerCase();
      if (!indexed.has(name)) indexed.set(name, emoji);
    }
    catalogs.set(emojis, indexed);
  }
  return indexed;
}

export function splitEmojiTitle(value, catalog) {
  const text = typeof value === 'string' ? value : '';
  const parts = [];
  let index = 0;
  // Match the platform's title renderer: do not expand shortcode-looking word fragments.
  const shortcodes = /(?<![\p{L}\p{N}_]):([a-zA-Z0-9_-]{2,32}):/gu;
  for (const match of text.matchAll(shortcodes)) {
    const emoji = catalog.get(match[1].toLowerCase());
    if (!emoji) continue;
    if (match.index > index) parts.push({ text: text.slice(index, match.index) });
    parts.push({ emoji });
    index = match.index + match[0].length;
  }
  if (index < text.length || parts.length === 0) parts.push({ text: text.slice(index) });
  return parts;
}
