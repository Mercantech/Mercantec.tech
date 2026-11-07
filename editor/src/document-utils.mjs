/** @typedef {{ workspaceId?: string|null, courseId?: string|null, holdId?: string|null, domain?: 'notion'|'notebook' }} NavigationContext */

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

/** @param {NavigationContext|null} [context] */
export function pollUrl(id, present = false, context = null) {
  if (!validId(id)) return null;
  return navigationUrl(present ? '/teacher/polls/' + id + '/present' : '/polls/' + id, context);
}
/** @param {NavigationContext|null} [context] */
export function pollCreateUrl(context = null) {
  return navigationUrl('/teacher/polls/new', context);
}
/** @param {NavigationContext|'notion'|'notebook'|null} [context] */
export function pageUrl(id, published = false, context = null) {
  if (!validId(id)) return null;
  const domain = typeof context === 'string' ? context : context?.domain;
  if (domain === 'notebook') return '/notebook/' + id;
  return navigationUrl('/notion/' + id, typeof context === 'string' ? null : context, published);
}
/** @param {NavigationContext|null} [context] */
export function studentUrl(holdId, membershipId, context = null) {
  return validId(holdId) && validId(membershipId)
    ? navigationUrl('/notion/holds/' + holdId + '/students/' + membershipId, context) : null;
}
/** @param {NavigationContext|null} context */
function navigationUrl(path, context, published = false) {
  const query = new URLSearchParams();
  const safeId = nonEmptyId;
  if (safeId(context?.workspaceId)) query.set('workspace', context.workspaceId);
  if ((safeId(context?.workspaceId) || safeId(context?.holdId)) && safeId(context?.courseId)) query.set('course', context.courseId);
  if (safeId(context?.holdId)) query.set('hold', context.holdId);
  if (published) query.set('published', 'true');
  return path + (query.size ? '?' + query.toString() : '');
}
function nonEmptyId(id) { return validId(id) && id !== '00000000-0000-0000-0000-000000000000'; }
function validId(id) { return typeof id === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id); }

/** Publication grants are identifiers, never expiring URLs or caller-provided filenames. */
export function collectPublicationAssetIds(blocks, pageId) {
  const ids=new Set();
  const visit=block=>{
    if(block.type==='pageMedia' && block.props?.assetId) {
      if(block.props.pageId!==pageId || !validId(block.props.assetId))throw new Error('En vedhæftet fil tilhører en anden side. Upload den igen på denne side.');
      ids.add(block.props.assetId);
    }
    for(const child of block.children || [])visit(child);
  };
  for(const block of blocks)visit(block);
  return [...ids].sort();
}
