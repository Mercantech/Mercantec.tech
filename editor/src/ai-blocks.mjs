const contentless = new Set(['mermaidDiagram', 'pageLink', 'entityLink', 'divider', 'slideBreak']);

/** Map a sanitized AI block to the PartialBlock shape BlockNote insertBlocks accepts. */
export function toEditorBlock(block) {
  if (!block || typeof block.type !== 'string' || !block.type) return null;
  const out = { type: block.type };
  if (block.props && typeof block.props === 'object') out.props = block.props;
  if (block.type === 'table') {
    if (block.content && typeof block.content === 'object') out.content = block.content;
  } else if (!contentless.has(block.type)) {
    if (block.content !== undefined && block.content !== null) out.content = block.content;
    else out.content = '';
  }
  if (Array.isArray(block.children) && block.children.length) {
    const children = block.children.map(toEditorBlock).filter(Boolean);
    if (children.length) out.children = children;
  }
  return out;
}
