import { createHash, createHmac, timingSafeEqual } from 'node:crypto';
import * as Y from 'yjs';

export const MCP_RECEIPTS = 'mcp-operation-receipts';
const HASH = /^[0-9a-f]{64}$/;
const ID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const TEXT_TYPES = new Set(['paragraph', 'heading', 'bulletListItem', 'numberedListItem', 'checkListItem', 'toggleListItem', 'quote', 'codeBlock', 'callout', 'learningGoal']);
const CONTENT_TYPES = new Set([...TEXT_TYPES, 'table', 'mermaidDiagram']);
const STRUCTURAL = new Set(['blockContainer', 'blockGroup', 'tableRow', 'tableCell', 'tableHeader', 'tableParagraph']);
const INLINE_TYPES = new Set(['customEmoji', 'hardBreak']);
const MARKS = new Set(['bold', 'italic', 'underline', 'strike', 'code', 'link', 'textColor', 'backgroundColor']);
const COMMON_ATTRIBUTES = new Set(['backgroundColor', 'textColor', 'textAlignment']);
const NODE_ATTRIBUTES = {
  blockContainer: ['id'], blockGroup: [], heading: ['level', 'isToggleable'], numberedListItem: ['start'], checkListItem: ['checked'],
  toggleListItem: ['isToggleable'], codeBlock: ['language'], callout: ['schemaVersion', 'tone', 'emojiId', 'emojiName'],
  mermaidDiagram: ['schemaVersion', 'source'], learningGoal: ['schemaVersion'], tableRow: [],
  tableCell: ['colspan', 'rowspan', 'colwidth'], tableHeader: ['colspan', 'rowspan', 'colwidth'], tableParagraph: [],
  customEmoji: ['emojiId', 'name'], hardBreak: [],
};
const MAX_NODES = 250000, MAX_DEPTH = 128, MAX_BYTES = 12 * 1024 * 1024;
export const MAX_MCP_READ_BYTES = 256 * 1024;

export class McpDocumentError extends Error {
  constructor(code, detail, status = 400) { super(detail || code); this.code = code; this.status = status; }
}
const fail = (code, detail, status) => { throw new McpDocumentError(code, detail, status); };
export function canonicalMcpJson(value) {
  if (Array.isArray(value)) return '[' + value.map(canonicalMcpJson).join(',') + ']';
  if (value && typeof value === 'object') return '{' + Object.keys(value).sort().filter(k => value[k] !== undefined).map(k => JSON.stringify(k) + ':' + canonicalMcpJson(value[k])).join(',') + '}';
  return JSON.stringify(value);
}
export const mcpHash = value => createHash('sha256').update(canonicalMcpJson(value)).digest('hex');
const cloneJson = value => JSON.parse(JSON.stringify(value));
const validObject = value => value && typeof value === 'object' && !Array.isArray(value);

/** A lossless live XML projection. Deleted CRDT history and private side maps are never returned. */
export function serializeMcpXml(node, budget = { count: 0 }, depth = 0) {
  if (++budget.count > MAX_NODES || depth > MAX_DEPTH) fail('document_too_complex', 'Dokumentet er for komplekst.', 413);
  if (node instanceof Y.XmlText) return { kind: 'text', delta: cloneJson(node.toDelta()) };
  if (!(node instanceof Y.XmlElement)) fail('unsupported_node', 'Dokumentet indeholder en ukendt XML-node.');
  return { kind: 'element', name: node.nodeName, attributes: cloneJson(node.getAttributes()), children: node.toArray().map(child => serializeMcpXml(child, budget, depth + 1)) };
}

export function deserializeMcpXml(node, budget = { count: 0 }, depth = 0) {
  if (++budget.count > MAX_NODES || depth > MAX_DEPTH) fail('document_too_complex', 'Ændringen er for kompleks.', 413);
  if (!validObject(node)) fail('invalid_block', 'En XML-node skal være et objekt.');
  if (node.kind === 'text') {
    if (!Array.isArray(node.delta) || node.delta.some(part => !validObject(part) || typeof part.insert !== 'string' || !part.insert.isWellFormed() || part.delete !== undefined || part.retain !== undefined || (part.attributes !== undefined && !validObject(part.attributes)))) fail('invalid_block', 'Tekst skal være en XML-delta med gyldige Unicode insert-strenge.');
    const result = new Y.XmlText(); result.applyDelta(cloneJson(node.delta)); return result;
  }
  if (node.kind !== 'element' || typeof node.name !== 'string' || !/^[A-Za-z][A-Za-z0-9]{0,63}$/.test(node.name) || !validObject(node.attributes) || !Array.isArray(node.children)) fail('invalid_block', 'Ugyldig XML-struktur.');
  if (Object.keys(node.attributes).some(key => !/^[A-Za-z][A-Za-z0-9_]{0,63}$/.test(key))) fail('invalid_block', 'Ugyldig XML-attribut.');
  const result = new Y.XmlElement(node.name);
  for (const [key, value] of Object.entries(node.attributes)) result.setAttribute(key, cloneJson(value));
  result.insert(0, node.children.map(child => deserializeMcpXml(child, budget, depth + 1)));
  return result;
}

function contentOf(block) { return block.toArray().find(node => node instanceof Y.XmlElement && node.nodeName !== 'blockGroup'); }
function groupOf(block) { return block.toArray().find(node => node instanceof Y.XmlElement && node.nodeName === 'blockGroup'); }
function nodeId(node) { return node instanceof Y.XmlElement && node.nodeName === 'blockContainer' ? node.getAttribute('id') : null; }
function ownText(node) {
  if (node instanceof Y.XmlText) return node.toDelta().map(part => typeof part.insert === 'string' ? part.insert : '\uFFFC').join('');
  if (!(node instanceof Y.XmlElement)) return '';
  if (node.nodeName === 'mermaidDiagram') return String(node.getAttribute('source') || '');
  if (!CONTENT_TYPES.has(node.nodeName) && !STRUCTURAL.has(node.nodeName)) return '\uFFFC';
  return node.toArray().filter(child => !(child instanceof Y.XmlElement && child.nodeName === 'blockGroup')).map(ownText).join(node.nodeName === 'tableRow' ? '\t' : node.nodeName === 'table' ? '\n' : '');
}

function documentIndex(doc) {
  const root = doc.getXmlFragment('document-store'), blocks = new Map(), ordered = [];
  const groups = root.toArray();
  if (groups.length > 1 || (groups.length && (!(groups[0] instanceof Y.XmlElement) || groups[0].nodeName !== 'blockGroup'))) fail('unsupported_document', 'Dokumentets rodstruktur understøttes ikke.');
  let visited = 0;
  const visit = (group, parentId = null, depth = 0) => {
    if (depth > MAX_DEPTH) fail('document_too_complex', 'Dokumentet er for dybt.', 413);
    const children = group.toArray();
    for (let index = 0; index < children.length; index++) {
      if (++visited > MAX_NODES) fail('document_too_complex', 'Dokumentet er for stort.', 413);
      const node = children[index], id = nodeId(node);
      if (typeof id !== 'string' || !id || id.length > 200 || blocks.has(id)) fail('invalid_document', 'Dokumentet har ugyldige eller gentagne blok-id’er.');
      const content = contentOf(node);
      if (!content) fail('invalid_document', 'En blok mangler indhold.');
      const entry = { id, node, content, group, parentId, index, depth };
      blocks.set(id, entry); ordered.push(entry);
      const nested = groupOf(node); if (nested) visit(nested, id, depth + 1);
    }
  };
  if (groups[0]) visit(groups[0]);
  return { root, group: groups[0], blocks, ordered };
}

function describeEntry(entry, mode = 'block', includeAnchor = true) {
  return { id: entry.id, mode, parentId: entry.parentId,
    anchor: includeAnchor ? Y.relativePositionToJSON(Y.createRelativePositionFromTypeIndex(entry.group, entry.index, 0)) : null,
    fingerprint: mcpHash(serializeMcpXml(entry.node)), block: serializeMcpXml(entry.node) };
}

export function readMcpPage(doc, { pageId, cursor = null, limit = 50 } = {}) {
  if (!ID.test(pageId || '') || !Number.isInteger(limit) || limit < 1 || limit > 100) fail('invalid_request', 'Ugyldigt side-id eller limit (1–100).');
  const index = documentIndex(doc);
  let start = 0;
  if (cursor) {
    let parsed; try { parsed = JSON.parse(Buffer.from(cursor, 'base64url').toString('utf8')); } catch { fail('invalid_cursor', 'Ugyldig fortsættelsesmarkør.'); }
    if (parsed.pageId !== pageId || typeof parsed.after !== 'string') fail('invalid_cursor', 'Markøren tilhører en anden side.');
    const prior = index.ordered.findIndex(entry => entry.id === parsed.after);
    if (prior < 0) fail('cursor_changed', 'Blokken ved markøren er slettet. Læs siden fra begyndelsen.', 409);
    start = prior + 1;
  }
  const selected = [], blocks = []; let bytes = 0;
  for (const entry of index.ordered.slice(start, start + limit)) {
    const block = { id: entry.id, type: entry.content.nodeName, text: ownText(entry.content), parentId: entry.parentId, depth: entry.depth,
      fingerprint: mcpHash(serializeMcpXml(entry.node)), contentFingerprint: mcpHash(serializeMcpXml(entry.content)), editable: CONTENT_TYPES.has(entry.content.nodeName), block: serializeMcpXml(entry.node) };
    const blockBytes = Buffer.byteLength(JSON.stringify(block));
    if (!blocks.length && blockBytes > MAX_MCP_READ_BYTES) fail('block_too_large', `Blokken ${entry.id} fylder ${blockBytes} bytes og overskrider læsebudgettet på ${MAX_MCP_READ_BYTES} bytes. Indholdet er ikke afkortet.`, 413);
    if (bytes + blockBytes > MAX_MCP_READ_BYTES) break;
    blocks.push(block); selected.push(entry); bytes += blockBytes;
  }
  const nextCursor = start + selected.length < index.ordered.length ? Buffer.from(JSON.stringify({ pageId, after: selected.at(-1).id })).toString('base64url') : null;
  return { pageId, blocks, nextCursor, totalBlocks: index.ordered.length, start, count: blocks.length, partial: nextCursor !== null || start > 0 };
}

function protectedParts(node, result = []) {
  if (node.kind === 'text') {
    for (const part of node.delta) for (const [key, value] of Object.entries(part.attributes || {})) if (!MARKS.has(key)) result.push(canonicalMcpJson({ mark: key, value }));
  } else if (!CONTENT_TYPES.has(node.name) && !STRUCTURAL.has(node.name) && !INLINE_TYPES.has(node.name)) result.push(canonicalMcpJson(node));
  else {
    for (const [key, value] of Object.entries(node.attributes)) if (!COMMON_ATTRIBUTES.has(key) && !(NODE_ATTRIBUTES[node.name] || []).includes(key)) result.push(canonicalMcpJson({ attribute: key, value, node: node.name }));
    for (const child of node.children) protectedParts(child, result);
  }
  return result.sort();
}

function validateXmlSchema(node, original = null) {
  // Unknown inline widgets can only be carried through byte-for-byte; Codex cannot create them or delete them implicitly.
  if (canonicalMcpJson(protectedParts(node)) !== canonicalMcpJson(original ? protectedParts(original) : [])) fail('protected_content', 'Medier, referencer og ukendte noder skal bevares uændret.');
  const known = value => value.kind === 'element' && (CONTENT_TYPES.has(value.name) || STRUCTURAL.has(value.name) || INLINE_TYPES.has(value.name));
  const visit = (value, parent = null) => {
    if (value.kind === 'text') {
      for (const part of value.delta) {
        if (typeof part.insert !== 'string') fail('invalid_block', 'Tekst skal være en streng.');
        for (const [mark, attributes] of Object.entries(part.attributes || {})) {
          if (MARKS.has(mark) && !validObject(attributes)) fail('invalid_formatting', 'XML-formatering skal have mark-attributter som objekter.');
          if (['textColor', 'backgroundColor'].includes(mark) && typeof attributes.stringValue !== 'string') fail('invalid_formatting', 'Farveformatering kræver stringValue.');
        }
        const link = part.attributes?.link;
        if (link && (typeof link.href !== 'string' || !/^(https?:\/\/|mailto:|\/(?!\/)|#)/i.test(link.href))) fail('invalid_link', 'Links skal bruge HTTP(S), mailto eller en lokal sti.');
      }
      return;
    }
    if (!known(value)) return;
    if (INLINE_TYPES.has(value.name)) {
      if (!(TEXT_TYPES.has(parent) && parent !== 'codeBlock' || parent === 'tableParagraph') || value.children.length) fail('invalid_inline_node', 'Emoji og linjeskift må kun stå som tomme inline-noder i tekstindhold.');
      const keys = Object.keys(value.attributes);
      if (value.name === 'hardBreak') {
        if (keys.length) fail('invalid_inline_node', 'Et linjeskift må ikke have attributter.');
      } else if (keys.length !== 2 || !keys.includes('emojiId') || !keys.includes('name') || !ID.test(value.attributes.emojiId || '') || value.attributes.emojiId === '00000000-0000-0000-0000-000000000000'
        || typeof value.attributes.name !== 'string' || !/^[\p{L}\p{N}][\p{L}\p{N}_-]{0,99}$/u.test(value.attributes.name)) {
        fail('invalid_inline_node', 'En emoji kræver et gyldigt emojiId og et sikkert navn uden ekstra attributter.');
      }
      return;
    }
    for (const key of ['backgroundColor', 'textColor']) if (value.attributes[key] !== undefined && typeof value.attributes[key] !== 'string') fail('invalid_formatting', 'Blokfarver skal være strenge.');
    if (value.attributes.textAlignment !== undefined && !['left', 'center', 'right', 'justify'].includes(value.attributes.textAlignment)) fail('invalid_formatting', 'Ugyldig tekstjustering.');
    for (const key of ['checked', 'isToggleable']) if (value.attributes[key] !== undefined && typeof value.attributes[key] !== 'boolean') fail('invalid_block', 'Ugyldig boolean-prop.');
    const elements = value.children.filter(child => child.kind === 'element');
    if (value.name === 'blockContainer') {
      if (typeof value.attributes.id !== 'string' || !value.attributes.id || value.attributes.id.length > 200 || value.children.some(child => child.kind !== 'element') || elements.length < 1 || elements.length > 2 || !CONTENT_TYPES.has(elements[0].name) || (elements[1] && elements[1].name !== 'blockGroup')) fail('invalid_block', 'En blok skal indeholde ét understøttet indholdsfelt og valgfri underblokke.');
    } else if (value.name === 'blockGroup') {
      if (value.children.some(child => child.kind !== 'element' || child.name !== 'blockContainer')) fail('invalid_block', 'En blokgruppe må kun indeholde blokke.');
    } else if (value.name === 'table') {
      if (!elements.length || value.children.some(child => child.kind !== 'element' || child.name !== 'tableRow')) fail('invalid_table', 'En tabel skal have rækker.');
      const occupied = []; let width = null, area = 0;
      for (let row = 0; row < elements.length; row++) {
        occupied[row] ||= [];
        let column = 0;
        for (const cell of elements[row].children) {
          while (occupied[row][column]) column++;
          const colspan = cell.attributes?.colspan ?? 1, rowspan = cell.attributes?.rowspan ?? 1;
          if (!Number.isInteger(colspan) || !Number.isInteger(rowspan) || colspan < 1 || rowspan < 1 || colspan > 1000 || row + rowspan > elements.length) fail('invalid_table', 'Ugyldige sammenflettede celler.');
          area += colspan * rowspan;
          if (column + colspan > 2048 || area > 2000000) fail('table_too_complex', 'Tabellen overskrider strukturens sikkerhedsbudget. Opdel den i flere tabeller.', 413);
          for (let y = row; y < row + rowspan; y++) {
            occupied[y] ||= [];
            for (let x = column; x < column + colspan; x++) { if (occupied[y][x]) fail('invalid_table', 'Tabelceller overlapper.'); occupied[y][x] = true; }
          }
          column += colspan;
        }
        width ??= occupied[row].length;
        if (occupied[row].length !== width || Array.from({ length: occupied[row].length }, (_, column) => occupied[row][column]).some(cell => !cell)) fail('invalid_table', 'Tabelrækkerne skal have samme bredde.');
      }
    } else if (value.name === 'tableRow') {
      if (!elements.length || value.children.some(child => child.kind !== 'element' || !['tableCell', 'tableHeader'].includes(child.name))) fail('invalid_table', 'En tabelrække skal have celler.');
    } else if (['tableCell', 'tableHeader'].includes(value.name)) {
      if (!elements.length || value.children.some(child => child.kind !== 'element' || child.name !== 'tableParagraph')) fail('invalid_table', 'Tabelceller skal indeholde tabelafsnit.');
      for (const key of ['colspan', 'rowspan']) if (value.attributes[key] !== undefined && (!Number.isInteger(value.attributes[key]) || value.attributes[key] < 1 || value.attributes[key] > 1000)) fail('invalid_table', 'Ugyldigt celle-spænd.');
      const widths = value.attributes.colwidth;
      if (widths !== undefined && widths !== null && (!Array.isArray(widths) || widths.length !== (value.attributes.colspan ?? 1) || widths.some(width => !Number.isInteger(width) || width < 1 || width > 100000))) fail('invalid_table', 'Ugyldige kolonnebredder.');
    } else if (value.name === 'mermaidDiagram') {
      if (value.children.length || typeof value.attributes.source !== 'string') fail('invalid_block', 'Mermaid skal ligge i source-attributten.');
    } else if (value.children.some(child => child.kind === 'element' && known(child) && !INLINE_TYPES.has(child.name))) fail('invalid_block', 'Tekstblokke må kun have inlineindhold.');
    if (value.name === 'heading' && value.attributes.level !== undefined && ![1, 2, 3, 4, 5, 6].includes(value.attributes.level)) fail('invalid_block', 'Overskriftsniveau skal være 1–6.');
    if (value.name === 'codeBlock' && value.children.some(child => child.kind !== 'text' || child.delta.some(part => Object.keys(part.attributes || {}).some(mark => MARKS.has(mark))))) fail('invalid_code_block', 'Kodeblokke skal indeholde ren tekst uden formateringsmarkeringer.');
    if (value.name === 'callout' && value.attributes.tone !== undefined && !['info', 'tip', 'warning', 'important'].includes(value.attributes.tone)) fail('invalid_block', 'Ugyldig callout-tone.');
    for (const child of value.children) visit(child, value.name);
  };
  visit(node);
}

function deterministicId(operationId, order, suffix = '') {
  const hash = createHash('sha256').update(operationId + ':' + order + ':' + suffix).digest('hex');
  return `${hash.slice(0, 8)}-${hash.slice(8, 12)}-4${hash.slice(13, 16)}-a${hash.slice(17, 20)}-${hash.slice(20, 32)}`;
}

function normalizeBlock(value, operationId, order, existingId = null, path = '') {
  if (!validObject(value)) fail('invalid_block', 'En ny blok skal være et objekt.');
  if (value.kind === 'element') {
    const result = cloneJson(value); result.attributes ||= {};
    result.attributes.id ||= existingId || deterministicId(operationId, order, path);
    if (existingId && result.attributes.id !== existingId) fail('invalid_block', 'Opdatering må ikke skifte blok-id.');
    return result;
  }
  if (!CONTENT_TYPES.has(value.type)) fail('unsupported_block', 'Denne bloktype kan ikke oprettes af Codex.');
  const id = existingId || value.id || deterministicId(operationId, order, path);
  const props = cloneJson(value.props || {});
  let children;
  if (typeof value.content === 'string' || value.content === undefined) children = value.type === 'mermaidDiagram' ? [] : [{ kind: 'text', delta: [{ insert: value.content || '' }] }];
  else if (Array.isArray(value.content)) children = [{ kind: 'text', delta: value.content.map(part => typeof part === 'string' ? { insert: part } : { insert: part.text, ...(part.styles ? { attributes: Object.fromEntries(Object.entries(part.styles).filter(([, style]) => style !== false).map(([key, style]) => [key, style === true ? {} : { stringValue: style }])) } : {}) }) }];
  else fail('invalid_block', 'Brug tekst eller XML for denne blok.');
  if (value.type === 'mermaidDiagram') props.source = props.source ?? value.content ?? '';
  const result = { kind: 'element', name: 'blockContainer', attributes: { id }, children: [{ kind: 'element', name: value.type, attributes: props, children }] };
  if (value.children !== undefined) {
    if (!Array.isArray(value.children)) fail('invalid_block', 'Underblokke skal være en liste.');
    if (value.children.length) result.children.push({ kind: 'element', name: 'blockGroup', attributes: {}, children: value.children.map((child, i) => normalizeBlock(child, operationId, order, null, path + '.' + i)) });
  }
  return result;
}

function textSegments(content) {
  const segments = []; let offset = 0;
  for (const child of content.toArray()) {
    const text = child instanceof Y.XmlText ? child.toDelta().map(part => typeof part.insert === 'string' ? part.insert : '\uFFFC').join('') : '\uFFFC';
    segments.push({ node: child, from: offset, to: offset + text.length, text }); offset += text.length;
  }
  return { segments, length: offset };
}

function replaceText(entry, change) {
  if (!TEXT_TYPES.has(entry.content.nodeName)) fail('unsupported_text_target', 'Teksterstatning kræver en tekstblok. Brug update_block til tabeller og Mermaid.');
  if (typeof change.text !== 'string' || !change.text.isWellFormed()) fail('invalid_change', 'Den nye tekst skal være en gyldig Unicode-streng.');
  const { segments, length } = textSegments(entry.content);
  const from = change.from ?? 0, to = change.to ?? length;
  if (!Number.isInteger(from) || !Number.isInteger(to) || from < 0 || to < from || to > length) fail('invalid_range', 'Tekstområdet ligger uden for blokken.');
  const sourceText = segments.map(segment => segment.text).join('');
  const splitsSurrogate = offset => offset > 0 && offset < sourceText.length
    && sourceText.charCodeAt(offset - 1) >= 0xD800 && sourceText.charCodeAt(offset - 1) <= 0xDBFF
    && sourceText.charCodeAt(offset) >= 0xDC00 && sourceText.charCodeAt(offset) <= 0xDFFF;
  if (splitsSurrogate(from) || splitsSurrogate(to)) fail('invalid_range', 'Tekstområdet må ikke dele et Unicode-tegn midt i et surrogatepar.');
  const overlapping = segments.filter(segment => segment.from < to && segment.to > from);
  if (overlapping.some(segment => !(segment.node instanceof Y.XmlText))) fail('protected_content', 'Markeringen indeholder en reference eller widget. Opdatér teksten omkring den.');
  for (const segment of overlapping) {
    let runOffset = segment.from;
    for (const run of segment.node.toDelta()) {
      const runLength = typeof run.insert === 'string' ? run.insert.length : 1;
      if (typeof run.insert !== 'string' && runOffset < to && runOffset + runLength > from) fail('protected_content', 'Markeringen indeholder en indlejret XML-reference.');
      runOffset += runLength;
    }
  }
  if (change.expectedText !== undefined && sourceText.slice(from, to) !== change.expectedText) fail('target_changed', 'Målteksten er ændret.', 409);
  const first = segments.find(segment => segment.node instanceof Y.XmlText && segment.from <= from && segment.to > from)
    || segments.findLast(segment => segment.node instanceof Y.XmlText && segment.to === from);
  if (!first) {
    if (length !== 0 || from !== 0 || to !== 0) fail('invalid_range', 'Indsættelsesstedet er en reference.');
    entry.content.insert(0, [new Y.XmlText(change.text)]); return;
  }
  // Delete only the selected text; boundary formatting and every adjacent atom retain their CRDT identities.
  const insertionOffset = from - first.from;
  let runOffset = 0, insertionAttributes = {};
  for (const run of first.node.toDelta()) {
    insertionAttributes = run.attributes || {};
    const runLength = typeof run.insert === 'string' ? run.insert.length : 1;
    if (runOffset + runLength > insertionOffset) break;
    runOffset += runLength;
  }
  for (const segment of overlapping.toReversed()) {
    const start = Math.max(from, segment.from) - segment.from, end = Math.min(to, segment.to) - segment.from;
    segment.node.delete(start, end - start);
  }
  first.node.insert(insertionOffset, change.text, cloneJson(insertionAttributes));
}

function assertFingerprint(entry, change, contentOnly = false) {
  const fingerprint = contentOnly ? change.contentFingerprint || change.fingerprint : change.fingerprint;
  if (!HASH.test(fingerprint || '')) fail('fingerprint_required', 'Brug fingerprint fra read_page for hver berørt blok.');
  const full = mcpHash(serializeMcpXml(entry.node));
  const own = contentOnly ? mcpHash(serializeMcpXml(entry.content)) : null;
  if (fingerprint !== full && fingerprint !== own) fail('target_changed', `Blokken ${entry.id} er ændret. Læs den igen.`, 409);
  return { id: entry.id, parentId: entry.parentId, fingerprint, contentOnly: own === fingerprint };
}

function assertNoOverlap(index, targets) {
  const set = new Set(targets);
  for (const id of targets) {
    let parent = index.blocks.get(id)?.parentId;
    while (parent) { if (set.has(parent)) fail('overlapping_changes', 'En ændringsbatch må ikke ramme både en blok og dens underblok.'); parent = index.blocks.get(parent)?.parentId; }
  }
}

function scratchOf(doc) { const scratch = new Y.Doc(); Y.applyUpdate(scratch, Y.encodeStateAsUpdate(doc)); return scratch; }
function captureEntries(index, ids, modes = new Map(), includeAnchor = true) { return index.ordered.filter(entry => ids.has(entry.id)).map(entry => describeEntry(entry, modes.get(entry.id), includeAnchor)); }

function setMediaTitle(content, title) {
  if (title === undefined) content.removeAttribute('displayTitle');
  else content.setAttribute('displayTitle', title);
}

function updateImageTitle(entry, proposed) {
  const assetId = entry.content.getAttribute('assetId');
  if (entry.content.getAttribute('kind') !== 'image' || typeof assetId !== 'string' || !ID.test(assetId)) {
    fail('protected_content', 'Kun visningstitlen på et uploadet billede kan ændres.');
  }
  if (!validObject(proposed)) fail('invalid_block', 'Brug den eksisterende billedbloks XML fra read_page.');

  const current = serializeMcpXml(entry.node), replacement = cloneJson(proposed);
  const contentIndex = entry.node.toArray().indexOf(entry.content);
  const attributes = replacement.children?.[contentIndex]?.attributes;
  if (!validObject(attributes)) fail('invalid_block', 'Brug den eksisterende billedbloks XML fra read_page.');
  const title = attributes.displayTitle;
  delete attributes.displayTitle;
  delete current.children[contentIndex].attributes.displayTitle;
  if (canonicalMcpJson(replacement) !== canonicalMcpJson(current)) {
    fail('protected_content', 'Billedet, blok-id og øvrigt indhold skal bevares uændret.');
  }
  if (title !== undefined && (typeof title !== 'string' || !title.isWellFormed() || /[\p{Cc}\p{Zl}\p{Zp}]/u.test(title) || title.trim().length > 240)) {
    fail('invalid_media_title', 'Billedets visningstitel må højst være 240 tegn og må ikke indeholde linjeskift eller kontroltegn.');
  }
  setMediaTitle(entry.content, title?.trim() || undefined);
}

export function prepareMcpApply(doc, request) {
  if (!ID.test(request.operationId || '') || !Array.isArray(request.changes) || !request.changes.length || request.changes.length > 500) fail('invalid_changes', 'En batch skal have 1–500 ændringer og et stabilt operations-id.');
  if (Buffer.byteLength(JSON.stringify(request.changes)) > MAX_BYTES) fail('changes_too_large', 'Ændringen er for stor til én batch. Fortsæt ved en blokgrænse.', 413);
  const original = documentIndex(doc), expected = [], beforeIds = new Set(), afterIds = new Set(), modes = new Map(), affected = [], anchors = new Set();
  const seenTargets = new Set();
  for (const change of request.changes) {
    if (!validObject(change) || !['replace_text', 'insert_before', 'insert_after', 'update_block', 'delete_block', 'append_block'].includes(change.type)) fail('invalid_change', 'Ukendt ændringstype.');
    if (change.type === 'append_block' && !change.blockId) {
      if (original.ordered.length) fail('anchor_required', 'Brug insert_after med en eksisterende blok som anker.');
      continue;
    }
    const entry = original.blocks.get(change.blockId);
    if (!entry) fail('target_missing', 'Målblokken findes ikke længere.', 409);
    expected.push(assertFingerprint(entry, change, change.type === 'replace_text'));
    const insertion = change.type.startsWith('insert_');
    if (insertion) anchors.add(entry.id);
    else {
      if (seenTargets.has(entry.id)) fail('overlapping_changes', 'Saml ændringer af samme blok i én operation.');
      seenTargets.add(entry.id); affected.push(entry.id); beforeIds.add(entry.id);
    }
  }
  assertNoOverlap(original, affected);
  for (const id of anchors) {
    let current = id;
    while (current) { if (seenTargets.has(current)) fail('overlapping_changes', 'Et indsættelsesanker må ikke samtidig ændres eller slettes.'); current = original.blocks.get(current)?.parentId; }
  }
  const scratch = scratchOf(doc), vector = Y.encodeStateVector(doc), afterInsertion = new Map();
  try {
    scratch.transact(() => {
      for (let order = 0; order < request.changes.length; order++) {
        const change = request.changes[order], index = documentIndex(scratch), entry = index.blocks.get(change.blockId);
        if (change.type === 'replace_text') { replaceText(entry, change); afterIds.add(entry.id); modes.set(entry.id, 'content'); }
        else if (change.type === 'update_block' && entry.content.nodeName === 'pageMedia') {
          updateImageTitle(entry, change.block);
          afterIds.add(entry.id); modes.set(entry.id, 'mediaTitle');
        } else if (change.type === 'update_block') {
          if (!CONTENT_TYPES.has(entry.content.nodeName)) fail('protected_content', 'Denne bloktype er beskyttet mod Codex-redigering.');
          const old = serializeMcpXml(entry.node), block = normalizeBlock(change.block, request.operationId, order, entry.id);
          block.attributes = { ...old.attributes, ...block.attributes };
          if (canonicalMcpJson(block.attributes) !== canonicalMcpJson(old.attributes)) fail('protected_container', 'Opdatér indholdets props; blokcontainerens metadata skal bevares.');
          // Updating the parent never discards unselected child blocks. Supplying children requires exact preservation.
          const oldChildren = old.children.find(node => node.name === 'blockGroup');
          const newChildren = block.children.find(node => node.name === 'blockGroup');
          if (oldChildren && !newChildren) block.children.push(oldChildren);
          else if (canonicalMcpJson(newChildren || null) !== canonicalMcpJson(oldChildren || null)) fail('protected_children', 'Opdatér underblokke særskilt; en forældreopdatering skal bevare dem.');
          validateXmlSchema(block, old);
          const contentIndex = entry.node.toArray().indexOf(entry.content);
          entry.node.delete(contentIndex, 1); entry.node.insert(contentIndex, [deserializeMcpXml(block.children[0])]);
          afterIds.add(entry.id); modes.set(entry.id, 'content');
        } else if (change.type === 'delete_block') {
          const old = serializeMcpXml(entry.node);
          if (!CONTENT_TYPES.has(entry.content.nodeName) || protectedParts(old).length) fail('protected_content', 'Blokken indeholder medier, referencer eller ukendte noder og kan ikke slettes af Codex.');
          entry.group.delete(entry.index, 1);
        } else {
          const block = normalizeBlock(change.block, request.operationId, order); validateXmlSchema(block);
          const inserted = deserializeMcpXml(block), id = block.attributes.id;
          if (index.blocks.has(id)) fail('duplicate_block_id', 'Et nyt blok-id findes allerede.');
          let group, at;
          if (!entry) {
            if (original.ordered.length) fail('target_changed', 'Dokumentet er ikke længere tomt.', 409);
            group = index.group;
            if (!group) { group = new Y.XmlElement('blockGroup'); index.root.insert(0, [group]); }
            at = group.length;
          } else {
            group = entry.group; at = entry.index + (change.type === 'insert_after' ? 1 : 0);
            const prior = afterInsertion.get(entry.id);
            if (change.type === 'insert_after' && prior) at = index.blocks.get(prior).index + 1;
          }
          group.insert(at, [inserted]); if (change.type === 'insert_after') afterInsertion.set(entry.id, id); afterIds.add(id);
        }
      }
    });
    const afterIndex = documentIndex(scratch);
    if (!afterIndex.ordered.length) fail('empty_document', 'Bevar mindst én tom tekstblok i dokumentet.');
    const before = { version: 1, blocks: captureEntries(original, beforeIds, modes) };
    const after = { version: 1, blocks: captureEntries(afterIndex, afterIds, modes, false) };
    const update = Y.encodeStateAsUpdate(scratch, vector);
    if (Y.encodeStateAsUpdate(scratch).length > 16 * 1024 * 1024) fail('document_too_large', 'Ændringen overskrider dokumentets lagringsgrænse.', 413);
    return { update, expected, empty: original.ordered.length === 0, before, after };
  } finally { scratch.destroy(); }
}

function auditEntries(audit) {
  if (audit?.version !== 1 || !Array.isArray(audit.blocks)) fail('invalid_history', 'Ændringshistorikken er ugyldig.');
  return audit.blocks;
}

export function prepareMcpUndo(doc, request) {
  const before = auditEntries(request.originalChange?.before), after = auditEntries(request.originalChange?.after);
  const original = documentIndex(doc), expected = [];
  for (const entry of after) {
    const live = original.blocks.get(entry.id);
    if (!live || live.parentId !== entry.parentId || mcpHash(serializeMcpXml(live.node)) !== entry.fingerprint) fail('undo_conflict', 'Indholdet er redigeret efter Codex-ændringen. Fortrydelse er ikke gennemført.', 409);
    expected.push({ id: live.id, parentId: live.parentId, fingerprint: entry.fingerprint, contentOnly: false });
  }
  const afterIds = new Set(after.map(entry => entry.id));
  for (const entry of before) if (!afterIds.has(entry.id) && original.blocks.has(entry.id)) fail('undo_conflict', 'Et tidligere slettet blok-id er genopstået.', 409);
  const scratch = scratchOf(doc), vector = Y.encodeStateVector(doc);
  try {
    scratch.transact(() => {
      for (const entry of after.toReversed()) {
        if (entry.mode === 'content' || entry.mode === 'mediaTitle') continue;
        const live = documentIndex(scratch).blocks.get(entry.id); live.group.delete(live.index, 1);
      }
      for (const entry of before) {
        const index = documentIndex(scratch);
        if (entry.mode === 'mediaTitle') {
          const live = index.blocks.get(entry.id), image = entry.block.children.find(node => node.name === 'pageMedia');
          if (live?.content.nodeName !== 'pageMedia' || !image) fail('undo_conflict', 'Billedblokken findes ikke længere.', 409);
          setMediaTitle(live.content, image.attributes.displayTitle);
          continue;
        }
        if (entry.mode === 'content') {
          const live = index.blocks.get(entry.id);
          if (!live) fail('undo_conflict', 'Målblokken findes ikke længere.', 409);
          const contentIndex = live.node.toArray().indexOf(live.content);
          live.node.delete(contentIndex, 1); live.node.insert(contentIndex, [deserializeMcpXml(entry.block.children[0])]);
          continue;
        }
        let group = entry.parentId ? groupOf(index.blocks.get(entry.parentId)?.node ?? new Y.XmlElement('empty')) : index.group;
        if (!group) fail('undo_conflict', 'Blokkens oprindelige placering findes ikke længere.', 409);
        const anchor = Y.createAbsolutePositionFromRelativePosition(Y.createRelativePositionFromJSON(entry.anchor), scratch);
        if (!anchor || anchor.type !== group) fail('undo_conflict', 'Blokkens oprindelige placering er ændret.', 409);
        group.insert(anchor.index, [deserializeMcpXml(entry.block)]);
      }
    });
    const restored = documentIndex(scratch), ids = new Set(before.map(entry => entry.id));
    return { update: Y.encodeStateAsUpdate(scratch, vector), expected, missing: before.filter(entry => !afterIds.has(entry.id)).map(entry => entry.id),
      before: { version: 1, blocks: captureEntries(original, new Set(after.map(entry => entry.id)), new Map(after.map(entry => [entry.id, entry.mode]))) },
      after: { version: 1, blocks: captureEntries(restored, ids, new Map(before.map(entry => [entry.id, entry.mode])), false) } };
  } finally { scratch.destroy(); }
}

export function revalidateMcpPlan(doc, plan) {
  const index = documentIndex(doc);
  if (plan.empty && index.ordered.length) fail('target_changed', 'Dokumentet er ikke længere tomt.', 409);
  for (const expected of plan.expected) {
    const entry = index.blocks.get(expected.id);
    if (!entry || entry.parentId !== expected.parentId || mcpHash(serializeMcpXml(expected.contentOnly ? entry.content : entry.node)) !== expected.fingerprint) fail('target_changed', 'En berørt blok er ændret. Ingen ændringer er gennemført.', 409);
  }
  for (const id of plan.missing || []) if (index.blocks.has(id)) fail('undo_conflict', 'Et tidligere blok-id er genopstået.', 409);
}

export function makeMcpReceipt(secret, request, plan) {
  const data = { version: 1, pageId: request.pageId, operationId: request.operationId, actorSub: request.actorSub,
    requestHash: request.requestHash, payloadHash: mcpHash(request.originalChange || request.changes), beforeHash: mcpHash(plan.before), afterHash: mcpHash(plan.after) };
  return { ...data, signature: createHmac('sha256', secret).update(canonicalMcpJson(data)).digest('hex') };
}

export function verifyMcpReceipt(secret, request, receipt) {
  if (!receipt || typeof receipt.signature !== 'string' || !HASH.test(receipt.signature)) return false;
  const { signature, ...data } = receipt;
  const expected = createHmac('sha256', secret).update(canonicalMcpJson(data)).digest('hex');
  return timingSafeEqual(Buffer.from(signature, 'hex'), Buffer.from(expected, 'hex')) && data.version === 1 && data.pageId === request.pageId
    && data.operationId === request.operationId && data.actorSub === request.actorSub && data.requestHash === request.requestHash
    && data.payloadHash === mcpHash(request.originalChange || request.changes);
}

export function applyPreparedMcpPlan(doc, plan, receipt, editorName) {
  revalidateMcpPlan(doc, plan);
  // Put the signed receipt in the same update before applying it. Mixing a remote applyUpdate
  // with local writes inside one Yjs transaction can reuse the live client's clock range.
  const candidate = scratchOf(doc);
  try {
    Y.applyUpdate(candidate, plan.update);
    candidate.getMap(MCP_RECEIPTS).set(receipt.operationId, receipt);
    Y.applyUpdate(doc, Y.encodeStateAsUpdate(candidate, Y.encodeStateVector(doc)), { source: 'mcp', operationId: receipt.operationId, editorName });
  } finally { candidate.destroy(); }
}
