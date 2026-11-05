/** Split the editorial snapshot into slides and omit class rosters at every depth. */
export function splitSlides(blocks) {
  const slides = [[]];
  const clean = block => {
    if (block.type === 'classRoster') return null;
    const result = structuredClone(block);
    if (result.children) result.children = result.children.map(clean).filter(Boolean);
    return result;
  };
  for (const block of blocks) {
    if (block.type === 'slideBreak') { slides.push([]); continue; }
    const safe = clean(block);
    if (safe) slides.at(-1).push(safe);
  }
  const hasVisibleContent = block => {
    if (block.type !== 'paragraph') return true;
    const content = block.content;
    const text = typeof content === 'string' ? content : Array.isArray(content) ? content.map(item => item.text || (item.type !== 'text' ? 'indhold' : '')).join('') : '';
    return Boolean(text.trim()) || Boolean(block.children?.some(hasVisibleContent));
  };
  const visibleSlides = slides.filter(slide => slide.some(hasVisibleContent));
  return visibleSlides.length ? visibleSlides : [[{ type: 'paragraph', content: 'Dette dokument har endnu intet indhold til tavlen.' }]];
}

export function selectedMembers(members, ids) {
  if (!ids) return members;
  const selected = new Set(ids.split(',').filter(Boolean));
  return members.filter(member => selected.has(member.id));
}

export function pollUrl(id, present = false) {
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) return null;
  return present ? '/teacher/polls/' + id + '/present' : '/polls/' + id;
}
