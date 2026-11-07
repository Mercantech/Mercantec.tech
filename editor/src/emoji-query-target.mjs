const changed = () => new Error('Teksten ved markøren er ændret. Vælg indsættelsesstedet igen.');

/** Capture before a native file chooser takes focus; Yjs-aware mapping is provided by BlockNote. */
export function captureEmojiQuery(editor, query, mapPosition) {
  const state = editor.prosemirrorState;
  const text = ':' + query;
  const to = state.selection.from;
  const from = to - text.length;
  if (state.selection.from !== state.selection.to || from < 0 || state.doc.textBetween(from, to, '\n', '\0') !== text) throw changed();
  const blockId = editor.getTextCursorPosition().block.id;
  return { text, blockId, from: mapPosition(editor, from, 'right'), to: mapPosition(editor, to, 'left') };
}

/** Only the exact original query in its original block may be replaced after a delayed upload. */
export function resolveEmojiQuery(editor, target) {
  const doc = editor.prosemirrorState.doc;
  const from = target.from(), to = target.to();
  if (!Number.isInteger(from) || !Number.isInteger(to) || from < 0 || to < from || to > doc.content.size || doc.textBetween(from, to, '\n', '\0') !== target.text) throw changed();
  const resolved = doc.resolve(from);
  let blockId;
  for (let depth = resolved.depth; depth >= 0; depth--) {
    const id = resolved.node(depth).attrs.id;
    if (typeof id === 'string') { blockId = id; break; }
  }
  if (blockId !== target.blockId || !editor.getBlock(target.blockId)) throw changed();
  return { from, to };
}
