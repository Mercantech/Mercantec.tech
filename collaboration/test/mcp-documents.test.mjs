import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { MCP_RECEIPTS, readMcpPage, serializeMcpXml, deserializeMcpXml, prepareMcpApply, prepareMcpUndo, revalidateMcpPlan, applyPreparedMcpPlan, makeMcpReceipt, verifyMcpReceipt, mcpHash } from '../src/mcp-documents.mjs';

const pageId = '11111111-2222-4333-8444-555555555555', actorSub = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const operationId = '88888888-2222-4333-8444-555555555555', secret = 'not-real-secret-for-test-only-123456';
const el = (name, children = [], attributes = {}) => ({ kind: 'element', name, attributes, children });
const txt = (text, attributes) => ({ kind: 'text', delta: [{ insert: text, ...(attributes ? { attributes } : {}) }] });
const block = (id, type, children = [], attrs = {}, nested = []) => el('blockContainer', [el(type, children, attrs), ...(nested.length ? [el('blockGroup', nested)] : [])], { id });
function document(blocks) { const doc = new Y.Doc(); doc.getXmlFragment('document-store').insert(0, [deserializeMcpXml(el('blockGroup', blocks))]); return doc; }
const snapshot = doc => readMcpPage(doc, { pageId, limit: 100 });
const find = (doc, id) => {
  let cursor;
  do { const page = readMcpPage(doc, { pageId, limit: 100, cursor }); const found = page.blocks.find(b => b.id === id); if (found) return found; cursor = page.nextCursor; } while (cursor);
};
const request = changes => ({ pageId, actorSub, operationId, requestHash: 'a'.repeat(64), changes });
const apply = (doc, req) => { const plan = prepareMcpApply(doc, req); const receipt = makeMcpReceipt(secret, req, plan); applyPreparedMcpPlan(doc, plan, receipt); return { plan, receipt }; };

test('live lossless snapshots retain marks, nested nodes and opaque widgets; cursor pages do not clip text', () => {
  const original = block('parent', 'paragraph', [txt('Start ', { bold: {} }), el('entityMention', [], { kind: 'notion-page', id: pageId }), txt(' slut')], {}, [block('nested', 'codeBlock', [txt('x'.repeat(100000))], { language: 'csharp' })]);
  const doc = document([original, block('media', 'pageMedia', [], { assetId: pageId, kind: 'image' })]);
  const first = readMcpPage(doc, { pageId, limit: 1 }); assert.equal(first.count, 1); assert.ok(first.nextCursor); assert.equal(first.totalBlocks, 3);
  assert.deepEqual(first.blocks[0].block, original);
  const next = readMcpPage(doc, { pageId, cursor: first.nextCursor, limit: 1 }); assert.equal(next.blocks[0].text.length, 100000);
  const media = readMcpPage(doc, { pageId, cursor: next.nextCursor, limit: 1 }); assert.equal(media.blocks[0].editable, false); assert.equal(media.nextCursor, null);
  doc.destroy();
});

test('partial rich text replacement preserves marks, references and all unselected child content', () => {
  const doc = document([block('a', 'paragraph', [txt('Bevar ', { bold: {} }), txt('ændr mig'), el('entityMention', [], { kind: 'notion-page', id: pageId }), txt(' slut', { italic: {} })], {}, [block('child', 'paragraph', [txt('Barn')])])]);
  const prior = find(doc, 'a');
  const req = request([{ type: 'replace_text', blockId: 'a', contentFingerprint: prior.contentFingerprint, from: 6, to: 14, expectedText: 'ændr mig', text: 'ny tekst' }]);
  const plan = prepareMcpApply(doc, req);
  // Editing a child is outside the parent's text target and remains valid.
  const child = doc.getXmlFragment('document-store').get(0).get(0).get(1).get(0).get(0).get(0);
  child.insert(child.length, ' udenfor');
  applyPreparedMcpPlan(doc, plan, makeMcpReceipt(secret, req, plan));
  const current = find(doc, 'a').block.children[0].children;
  assert.deepEqual(current[0], txt('Bevar ', { bold: {} }));
  assert.equal(current[1].delta.map(p => p.insert).join(''), 'ny tekst');
  assert.deepEqual(current[2], el('entityMention', [], { kind: 'notion-page', id: pageId }));
  assert.deepEqual(current[3], txt(' slut', { italic: {} }));
  assert.equal(find(doc, 'child').text, 'Barn udenfor');
  assert.throws(() => prepareMcpApply(doc, request([{ type: 'replace_text', blockId: 'a', contentFingerprint: find(doc, 'a').contentFingerprint, from: 14, to: 15, text: '' }])), /reference|widget/);
  doc.destroy();
});

test('UTF-16 offsets after marked text and an inline atom follow read_page plain text and retain insertion marks', () => {
  const doc = document([block('a', 'paragraph', [txt('Før😀 ', { bold: {} }), el('entityMention', [], { kind: 'notion-page', id: pageId }), txt(' efter', { italic: {} })])]);
  const prior = find(doc, 'a'); assert.equal(prior.text, 'Før😀 \uFFFC efter');
  const text = doc.getXmlFragment('document-store').get(0).get(0).get(0), prefix = text.get(0), atom = text.get(1);
  apply(doc, request([{ type: 'replace_text', blockId: 'a', contentFingerprint: prior.contentFingerprint, from: 8, to: 13, expectedText: 'efter', text: 'ny🦊tekst' }]));
  assert.equal(find(doc, 'a').text, 'Før😀 \uFFFC ny🦊tekst');
  assert.equal(text.get(0), prefix); assert.equal(text.get(1), atom);
  assert.deepEqual(serializeMcpXml(text.get(2)), txt(' ny🦊tekst', { italic: {} }));
  doc.destroy();
});

test('surrogate-splitting ranges and malformed inserted Unicode fail before every live Yjs mutation', () => {
  const doc = document([block('a', 'paragraph', [txt('A😀B', { bold: {} })])]);
  const before = Y.encodeStateAsUpdate(doc); let updates = 0; doc.on('update', () => updates++);
  const fingerprint = find(doc, 'a').contentFingerprint;
  for (const range of [{ from: 2, to: 3 }, { from: 1, to: 2 }, { from: 2, to: 2 }]) {
    assert.throws(() => prepareMcpApply(doc, request([{ type: 'replace_text', blockId: 'a', fingerprint, ...range, text: 'x' }])), error => error.code === 'invalid_range');
  }
  for (const text of ['\uD83D', '\uDE00', 'x\uD83Dy']) {
    assert.throws(() => prepareMcpApply(doc, request([{ type: 'replace_text', blockId: 'a', fingerprint, from: 1, to: 3, text }])), error => error.code === 'invalid_change');
  }
  assert.equal(updates, 0); assert.deepEqual(Y.encodeStateAsUpdate(doc), before);
  apply(doc, request([{ type: 'replace_text', blockId: 'a', fingerprint, from: 1, to: 3, expectedText: '😀', text: '🦊' }]));
  assert.equal(find(doc, 'a').text, 'A🦊B'); doc.destroy();
});

test('target changes conflict atomically while unrelated blocks and media keep their CRDT identities', () => {
  const doc = document([block('a', 'paragraph', [txt('A')]), block('b', 'paragraph', [txt('B')]), block('media', 'drawing', [], { drawingId: pageId })]);
  const root = doc.getXmlFragment('document-store').get(0), media = root.get(2);
  const req = request([{ type: 'replace_text', blockId: 'a', fingerprint: find(doc, 'a').contentFingerprint, text: 'C' }]);
  const plan = prepareMcpApply(doc, req);
  root.get(1).get(0).get(0).insert(1, ' mere');
  applyPreparedMcpPlan(doc, plan, makeMcpReceipt(secret, req, plan));
  assert.equal(find(doc, 'b').text, 'B mere'); assert.equal(root.get(2), media);
  const conflicting = prepareMcpApply(doc, request([{ type: 'replace_text', blockId: 'a', fingerprint: find(doc, 'a').contentFingerprint, text: 'D' }]));
  root.get(0).get(0).get(0).insert(0, '!');
  const before = snapshot(doc); assert.throws(() => applyPreparedMcpPlan(doc, conflicting, makeMcpReceipt(secret, req, conflicting)), /ændret/); assert.deepEqual(snapshot(doc), before);
  doc.destroy();
});

test('updating and undoing a parent preserve child CRDT identities and delayed concurrent child edits', () => {
  const doc = document([block('parent', 'paragraph', [txt('Forælder')], {}, [block('child', 'paragraph', [txt('Barn')])])]);
  const parent = doc.getXmlFragment('document-store').get(0).get(0), childGroup = parent.get(1), child = childGroup.get(0), childText = child.get(0).get(0);
  const relative = Y.createRelativePositionFromTypeIndex(childText, 2);
  const peer = new Y.Doc(); Y.applyUpdate(peer, Y.encodeStateAsUpdate(doc));
  const req = request([{ type: 'update_block', blockId: 'parent', fingerprint: find(doc, 'parent').fingerprint, block: { type: 'heading', props: { level: 2 }, content: 'Ny overskrift' } }]);
  const { plan } = apply(doc, req);
  assert.equal(doc.getXmlFragment('document-store').get(0).get(0), parent); assert.equal(parent.get(1), childGroup); assert.equal(childGroup.get(0), child);
  assert.equal(Y.createAbsolutePositionFromRelativePosition(relative, doc).type, childText);
  const undoReq = { ...request([]), operationId: randomOperationId(), originalChange: { before: plan.before, after: plan.after } };
  const undo = prepareMcpUndo(doc, undoReq); applyPreparedMcpPlan(doc, undo, makeMcpReceipt(secret, undoReq, undo));
  assert.equal(parent.get(1), childGroup); assert.equal(childGroup.get(0), child);
  peer.getXmlFragment('document-store').get(0).get(0).get(1).get(0).get(0).get(0).insert(4, ' fra en offline klient');
  Y.applyUpdate(doc, Y.encodeStateAsUpdate(peer, Y.encodeStateVector(doc)));
  assert.equal(find(doc, 'child').text, 'Barn fra en offline klient');
  peer.destroy(); doc.destroy();
});

const randomOperationId = () => '77777777-2222-4333-8444-555555555555';

test('unknown block types and rich atoms cannot be silently deleted, replaced or newly introduced', () => {
  const doc = document([block('a', 'paragraph', [txt('A'), el('entityMention', [], { kind: 'notion-page', id: pageId })]), block('opaque', 'futureWidget', [], { arbitrary: ['retained'] })]);
  const before = snapshot(doc);
  for (const change of [
    { type: 'delete_block', blockId: 'opaque', fingerprint: find(doc, 'opaque').fingerprint },
    { type: 'update_block', blockId: 'opaque', fingerprint: find(doc, 'opaque').fingerprint, block: { type: 'paragraph', content: 'lost' } },
    { type: 'update_block', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: { type: 'paragraph', content: 'lost emoji' } },
    { type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: block('new', 'paragraph', [el('entityMention', [], { kind: 'notion-page', id: pageId })]) },
  ]) assert.throws(() => prepareMcpApply(doc, request([change])), /beskyttet|bevares|medier|referencer|widget/i);
  assert.deepEqual(snapshot(doc), before); doc.destroy();
});

test('known inline Raymond emojis and hard breaks can be inserted and updated without losing rich text, references or media, and undo restores them', () => {
  const reference = el('entityMention', [], { kind: 'notion-page', id: pageId });
  const original = block('a', 'paragraph', [txt('SQL ', { bold: {} }), reference, txt(' beskytter data.', { italic: {} })]);
  const media = block('media', 'pageMedia', [], { assetId: pageId, kind: 'image' });
  const doc = document([original, media]), root = doc.getXmlFragment('document-store').get(0), mediaNode = root.get(1);
  const emoji = el('customEmoji', [], { emojiId: actorSub, name: 'raymond_applause' });
  const updated = block('a', 'paragraph', [txt('SQL ', { bold: {} }), emoji, txt(' '), reference, el('hardBreak'), txt('beskytter data.', { italic: {} })]);
  const req = request([{ type: 'update_block', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: updated }]);
  const { plan } = apply(doc, req);
  assert.deepEqual(find(doc, 'a').block, updated); assert.equal(root.get(1), mediaNode); assert.deepEqual(find(doc, 'media').block, media);
  assert.equal(find(doc, 'a').text, 'SQL \uFFFC \uFFFC\uFFFCbeskytter data.');
  const undoReq = { ...request([]), operationId: '99999999-2222-4333-8444-555555555555', originalChange: { before: plan.before, after: plan.after } };
  const undo = prepareMcpUndo(doc, undoReq); applyPreparedMcpPlan(doc, undo, makeMcpReceipt(secret, undoReq, undo));
  assert.deepEqual(find(doc, 'a').block, original); assert.equal(root.get(1), mediaNode);
  const insertion = block('emoji-block', 'paragraph', [emoji, el('hardBreak'), txt('Godt arbejde!', { bold: {} })]);
  apply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: insertion }]));
  assert.deepEqual(find(doc, 'emoji-block').block, insertion); assert.deepEqual(find(doc, 'media').block, media); doc.destroy();
});

test('inline emoji schema rejects invalid IDs, names, extra props, child content, block placements and code atoms before mutation', () => {
  const doc = document([block('a', 'paragraph', [txt('Bevar')])]), before = Y.encodeStateAsUpdate(doc);
  const emoji = { emojiId: actorSub, name: 'raymond_applause' };
  const invalid = [
    el('customEmoji', [], { ...emoji, emojiId: 'not-a-uuid' }), el('customEmoji', [], { ...emoji, emojiId: 123 }), el('customEmoji', [], { ...emoji, emojiId: '00000000-0000-0000-0000-000000000000' }),
    el('customEmoji', [], { emojiId: actorSub }), el('customEmoji', [], { ...emoji, name: '' }),
    el('customEmoji', [], { ...emoji, name: 'x'.repeat(101) }), el('customEmoji', [], { ...emoji, name: 'raymond/../image' }),
    el('customEmoji', [], { ...emoji, name: 123 }), el('customEmoji', [], { ...emoji, href: 'https://example.com' }),
    el('customEmoji', [txt('hidden')], emoji), el('hardBreak', [], { hidden: true }), el('hardBreak', [txt('hidden')]),
  ];
  for (const atom of invalid) assert.throws(() => prepareMcpApply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: block('bad', 'paragraph', [atom]) }])));
  for (const atom of [el('customEmoji', [], emoji), el('hardBreak')]) {
    assert.throws(() => prepareMcpApply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: block('bad-code', 'codeBlock', [atom]) }])));
    assert.throws(() => prepareMcpApply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: el('blockContainer', [atom], { id: 'bad-root' }) }])));
    assert.throws(() => prepareMcpApply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: block('bad-mermaid', 'mermaidDiagram', [atom], { source: 'flowchart LR;A-->B' }) }])));
  }
  assert.deepEqual(Y.encodeStateAsUpdate(doc), before); doc.destroy();
});

test('large structured updates retain tables, long code and Mermaid, and repeated sibling inserts keep order', () => {
  const table = block('table', 'table', Array.from({ length: 60 }, (_, row) => el('tableRow', Array.from({ length: 12 }, (_, col) => el('tableCell', [el('tableParagraph', [txt(`${row}:${col}`)])], { colspan: 1, rowspan: 1, colwidth: null })))));
  const doc = document([block('a', 'paragraph', [txt('Anker')]), table, block('code', 'codeBlock', [txt('line\n'.repeat(8000))], { language: 'sql' }), block('diagram', 'mermaidDiagram', [], { source: 'flowchart LR\nA-->B', schemaVersion: 1 })]);
  const anchor = find(doc, 'a');
  const changes = Array.from({ length: 45 }, (_, i) => ({ type: 'insert_after', blockId: 'a', fingerprint: anchor.fingerprint, block: { id: 'insert-' + i, type: 'paragraph', content: 'Afsnit ' + i } }));
  apply(doc, request(changes));
  assert.deepEqual(snapshot(doc).blocks.slice(1, 46).map(b => b.text), Array.from({ length: 45 }, (_, i) => 'Afsnit ' + i));
  assert.deepEqual(find(doc, 'table').block, table); assert.equal(find(doc, 'code').text.length, 40000); assert.equal(find(doc, 'diagram').text, 'flowchart LR\nA-->B');
  doc.destroy();
});

test('undo restores only affected blocks and rejects subsequent target changes', () => {
  const doc = document([block('a', 'paragraph', [txt('A')]), block('b', 'paragraph', [txt('B')]), block('c', 'paragraph', [txt('C')])]);
  const req = request([{ type: 'delete_block', blockId: 'b', fingerprint: find(doc, 'b').fingerprint }, { type: 'insert_after', blockId: 'c', fingerprint: find(doc, 'c').fingerprint, block: { id: 'new', type: 'callout', props: { tone: 'tip', schemaVersion: 1 }, content: 'Tip' } }]);
  const { plan } = apply(doc, req);
  const root = doc.getXmlFragment('document-store').get(0); root.get(0).get(0).get(0).insert(1, '!');
  const undoReq = { ...request([]), operationId: '99999999-2222-4333-8444-555555555555', originalChange: { before: plan.before, after: plan.after } };
  const undo = prepareMcpUndo(doc, undoReq); applyPreparedMcpPlan(doc, undo, makeMcpReceipt(secret, undoReq, undo));
  assert.deepEqual(snapshot(doc).blocks.map(b => b.text), ['A!', 'B', 'C']);
  const changed = apply(doc, request([{ type: 'replace_text', blockId: 'b', fingerprint: find(doc, 'b').fingerprint, text: 'ændret' }]));
  root.get(1).get(0).get(0).insert(0, 'senere ');
  assert.throws(() => prepareMcpUndo(doc, { ...undoReq, originalChange: changed.plan }), /redigeret/);
  doc.destroy();
});

test('receipts are signed, contain no audit text and bind the operation to actor, payload and page', () => {
  const doc = document([block('a', 'paragraph', [txt('Privat gammelt indhold')])]);
  const req = request([{ type: 'replace_text', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, text: 'Nyt indhold' }]);
  const { receipt } = apply(doc, req);
  assert.equal(verifyMcpReceipt(secret, req, receipt), true); assert.equal(verifyMcpReceipt(secret, { ...req, actorSub: operationId }, receipt), false);
  assert.equal(verifyMcpReceipt(secret, { ...req, changes: [...req.changes, { type: 'delete_block' }] }, receipt), false);
  assert.equal(verifyMcpReceipt(secret, req, { ...receipt, afterHash: 'b'.repeat(64) }), false);
  assert.equal(JSON.stringify(doc.getMap(MCP_RECEIPTS).toJSON()).includes('Privat'), false);
  assert.equal(mcpHash(receipt).length, 64); doc.destroy();
});

test('all validation runs before touching live content and empty documents support stable batch insertion', () => {
  const doc = new Y.Doc(); const req = request(Array.from({ length: 3 }, (_, i) => ({ type: 'append_block', block: { type: 'paragraph', content: String(i) } })));
  apply(doc, req); assert.deepEqual(snapshot(doc).blocks.map(b => b.text), ['0', '1', '2']);
  const before = snapshot(doc);
  assert.throws(() => prepareMcpApply(doc, request([{ type: 'update_block', blockId: before.blocks[0].id, fingerprint: before.blocks[0].fingerprint, block: { type: 'heading', props: { level: 9 }, content: 'bad' } }])));
  assert.deepEqual(snapshot(doc), before); doc.destroy();
});

test('read byte budgets continue between whole blocks and oversized single blocks fail explicitly', () => {
  const doc = document(Array.from({ length: 5 }, (_, i) => block(String(i), 'paragraph', [txt('x'.repeat(65000))])));
  let cursor = null; const ids = [];
  do { const page = readMcpPage(doc, { pageId, cursor }); assert.ok(page.count > 0); ids.push(...page.blocks.map(b => b.id)); cursor = page.nextCursor; } while (cursor);
  assert.deepEqual(ids, ['0', '1', '2', '3', '4']); doc.destroy();
  const giant = document([block('giant', 'codeBlock', [txt('x'.repeat(150000))])]);
  assert.throws(() => readMcpPage(giant, { pageId }), error => error.code === 'block_too_large' && error.status === 413 && /giant.*bytes/.test(error.message)); giant.destroy();
});

test('schema validation maps rich color styles correctly and rejects malformed marks and table geometry', () => {
  const doc = document([block('a', 'paragraph', [txt('A')])]);
  const before = find(doc, 'a');
  apply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: before.fingerprint, block: { id: 'colored', type: 'paragraph', content: [{ text: 'Farve', styles: { textColor: 'red', bold: true, italic: false } }] } }]));
  assert.deepEqual(find(doc, 'colored').block.children[0].children[0].delta[0].attributes, { textColor: { stringValue: 'red' }, bold: {} });
  const badTable = block('bad', 'table', [el('tableRow', [el('tableCell', [el('tableParagraph', [txt('A')])]), el('tableCell', [el('tableParagraph', [txt('B')])])]), el('tableRow', [el('tableCell', [el('tableParagraph', [txt('C')])])])]);
  for (const value of [badTable, block('bad', 'paragraph', [txt('A', { bold: 'not an object' })]), block('bad', 'codeBlock', [txt('code', { bold: {} })])]) {
    assert.throws(() => prepareMcpApply(doc, request([{ type: 'insert_after', blockId: 'a', fingerprint: find(doc, 'a').fingerprint, block: value }])));
  }
  doc.destroy();
});

test('undo of adjacent deletions restores their document order even when request order is reversed', () => {
  const doc = document(['a', 'b', 'c', 'd'].map(id => block(id, 'paragraph', [txt(id)])));
  const req = request(['c', 'b'].map(id => ({ type: 'delete_block', blockId: id, fingerprint: find(doc, id).fingerprint })));
  const { plan } = apply(doc, req);
  const undoReq = { ...request([]), operationId: randomOperationId(), originalChange: { before: plan.before, after: plan.after } };
  const undo = prepareMcpUndo(doc, undoReq); applyPreparedMcpPlan(doc, undo, makeMcpReceipt(secret, undoReq, undo));
  assert.deepEqual(snapshot(doc).blocks.map(b => b.text), ['a', 'b', 'c', 'd']); doc.destroy();
});
