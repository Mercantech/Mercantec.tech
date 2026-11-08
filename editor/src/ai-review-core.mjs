import { Fragment, Slice } from 'prosemirror-model';

/** Canonical comparisons concern only the original target, never the whole page. */
export function canonicalAiJson(value) {
  // JSON.stringify (and the server snapshot) represents missing array entries as null.
  if (Array.isArray(value)) return '[' + value.map(item => canonicalAiJson(item) ?? 'null').join(',') + ']';
  if (value && typeof value === 'object') return '{' + Object.keys(value).filter(key => value[key] !== undefined).sort().map(key => JSON.stringify(key) + ':' + canonicalAiJson(value[key])).join(',') + '}';
  return JSON.stringify(value);
}
export const aiReceiptKey = hunk => hunk.id;
export function aiHasReceipt(receipts, hunk) {
  if (receipts.has(aiReceiptKey(hunk)) || receipts.has(hunk.id + ':' + hunk.version)) return true;
  // Previously stored version keys still represent this same stable operation.
  for (const key of receipts.keys()) if (key.startsWith(hunk.id + ':')) return true;
  return false;
}

export function findAiBlockNodes(doc, ids) {
  const wanted = new Set(ids); const found = [];
  doc.descendants((node, pos) => {
    if (typeof node.attrs.id === 'string' && wanted.has(node.attrs.id)) found.push({ id: node.attrs.id, node, from: pos, to: pos + node.nodeSize });
  });
  return found.sort((a, b) => a.from - b.from);
}

export function aiSelectionSliceMatches(doc, from, to, slice) {
  return Number.isInteger(from) && Number.isInteger(to) && from >= 0 && to > from && to <= doc.content.size && canonicalAiJson(doc.slice(from, to).toJSON()) === canonicalAiJson(slice);
}

/** Construct every step before committing. A conflict or invalid structure leaves the live document untouched. */
export function buildAiReviewTransaction(state, changes) {
  const ordered = [...changes].sort((a, b) => b.from - a.from || b.to - a.to || (b.order || 0) - (a.order || 0));
  for (let index = 1; index < ordered.length; index++) {
    const previous = ordered[index - 1]; const next = ordered[index];
    const siblingInserts = next.from === next.to && previous.from === previous.to;
    if (!siblingInserts && (next.to > previous.from || next.from === previous.from && next.to !== next.from && previous.to !== previous.from)) throw new Error('Forslagene overlapper. Godkend dem enkeltvis, eller få dem justeret.');
  }
  const tr = state.tr;
  for (const change of ordered) {
    if (change.selection) {
      // Open paragraph edges merge with the surrounding marked text. Structured edges stay closed.
      const nodes = change.nodes;
      const start = state.doc.resolve(change.from), end = state.doc.resolve(change.to);
      const firstTextblock = nodes[0]?.childCount === 1 && nodes[0]?.firstChild?.isTextblock ? nodes[0].firstChild : null;
      if (nodes.length === 1 && firstTextblock?.sameMarkup(start.parent) && start.sameParent(end)) {
        tr.replace(change.from, change.to, new Slice(nodes[0].firstChild.content, 0, 0));
      } else {
        const content = Fragment.fromArray(nodes);
        const openStart = firstTextblock?.sameMarkup(start.parent) ? 2 : 0;
        const last = nodes[nodes.length - 1];
        const openEnd = last?.childCount === 1 && last?.firstChild?.isTextblock && last.firstChild.sameMarkup(end.parent) ? 2 : 0;
        tr.replace(change.from, change.to, new Slice(content, openStart, openEnd));
      }
    } else tr.replaceWith(change.from, change.to, change.nodes);
  }
  tr.doc.check();
  return tr;
}
