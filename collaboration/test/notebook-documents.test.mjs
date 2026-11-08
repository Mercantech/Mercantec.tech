import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as Y from 'yjs';
import { cloneNotebookDocument, defaultNotebookTemplate, notebookStateProjection, projectNotebookDocument, projectNotionContext } from '../src/notebook-documents.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const targetId = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const oldAsset = '22222222-2222-4222-8222-222222222222';
const newAsset = '33333333-3333-4333-8333-333333333333';
const encode = doc => Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64');
const decode = encoded => { const doc = new Y.Doc(); Y.applyUpdate(doc, Buffer.from(encoded, 'base64')); return doc; };
const element = (name, attrs, children = []) => {
  const node = new Y.XmlElement(name);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
  node.insert(0, children);
  return node;
};

test('AI projection preserves block boundaries, heading context and code without reading deleted history or side maps', () => {
  const doc = new Y.Doc({ gc: false });
  const nested = element('blockContainer', { id: 'nested' }, [element('codeBlock', { language: 'csharp' }, [new Y.XmlText('var x = 42;')])]);
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'title' }, [element('heading', { level: 2 }, [new Y.XmlText('Relationer')])]),
    element('blockContainer', { id: 'parent' }, [element('paragraph', {}, [new Y.XmlText('Vigtig tekst')]), element('blockGroup', {}, [nested])]),
    element('blockContainer', { id: 'diagram' }, [element('mermaidDiagram', { source: 'graph TD\n A-->B' })]),
    element('blockContainer', { id: 'link' }, [element('pageLink', { pageId: id })]),
    element('blockContainer', { id: 'deleted' }, [element('paragraph', {}, [new Y.XmlText('SLETTET HEMMELIGHED')])]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]);
  doc.getMap('ai-review-receipts').set('private', 'PRIVAT KLADDE');
  group.delete(4, 1);
  const projection = projectNotionContext({ stateBase64: encode(doc) });
  assert.deepEqual(projection.blocks.map(b => [b.id, b.type, b.text, b.heading]), [
    ['title', 'heading', 'Relationer', 'Relationer'], ['parent', 'paragraph', 'Vigtig tekst', 'Relationer'],
    ['nested', 'codeBlock', 'var x = 42;', 'Relationer'], ['diagram', 'mermaidDiagram', 'graph TD\n A-->B', 'Relationer'],
    ['link', 'pageLink', '', 'Relationer'],
  ]);
  assert.deepEqual(projection.references, [{ kind: 'notion-page', id }]);
  assert.doesNotMatch(JSON.stringify(projection), /SLETTET|HEMMELIGHED|PRIVAT/);
  assert.match(projection.plainText, /var x = 42/);
  assert.throws(() => projectNotionContext({ stateBase64: '%%%!' }), /Invalid notebook state/);
  doc.destroy();
});

test('notebook projection searches live content and references, excluding deleted history', () => {
  const doc = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'block-one' }, [element('paragraph', {}, [new Y.XmlText('Læring om C#')])]),
    element('blockContainer', { id: 'block-two' }, [element('pageLink', { pageId: id })]),
    element('blockContainer', { id: 'block-three' }, [element('pageMedia', { pageId: id, assetId: oldAsset })]),
    element('blockContainer', { id: 'removed' }, [element('paragraph', {}, [new Y.XmlText('SKJULT SLETTET')])]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]);
  group.delete(3, 1);
  const projection = projectNotebookDocument(doc);
  assert.match(projection.plainText, /Læring om C#/);
  assert.doesNotMatch(projection.plainText, /SKJULT|SLETTET/);
  assert.deepEqual(projection.linkIds, [id]);
  assert.deepEqual(projection.assetIds, [oldAsset]);
  assert.ok(Buffer.from(encode(doc), 'base64').includes(Buffer.from('SKJULT SLETTET')), 'the deleted source really remains in CRDT history');
  doc.destroy();
});

test('template copies get fresh CRDT/block identities and local asset IDs without deleted source content', () => {
  const source = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'old-id' }, [element('paragraph', {}, [new Y.XmlText('Bevar denne tekst')])]),
    element('blockContainer', { id: 'old-media' }, [element('pageMedia', { pageId: id, assetId: oldAsset, kind: 'image' })]),
    element('blockContainer', { id: 'removed' }, [element('paragraph', {}, [new Y.XmlText('SLETTET FORTID')])]),
  ]);
  source.getXmlFragment('document-store').insert(0, [group]);
  group.delete(2, 1);
  const request = { stateBase64: encode(source), pageId: targetId, assetMap: { [oldAsset]: newAsset } };
  const first = cloneNotebookDocument(request), second = cloneNotebookDocument(request);
  const firstDoc = decode(first.stateBase64), secondDoc = decode(second.stateBase64);
  const firstGroup = firstDoc.getXmlFragment('document-store').get(0);
  const secondGroup = secondDoc.getXmlFragment('document-store').get(0);
  assert.notEqual(firstGroup.get(0).getAttribute('id'), 'old-id');
  assert.notEqual(firstGroup.get(0).getAttribute('id'), secondGroup.get(0).getAttribute('id'));
  assert.equal(firstGroup.get(1).get(0).getAttribute('pageId'), targetId);
  assert.equal(firstGroup.get(1).get(0).getAttribute('assetId'), newAsset);
  assert.deepEqual(first.assetIds, [newAsset]);
  assert.equal(first.plainText, 'Bevar denne tekst');
  assert.equal(Buffer.from(first.stateBase64, 'base64').includes(Buffer.from('SLETTET FORTID')), false);
  assert.deepEqual(notebookStateProjection(Y.encodeStateAsUpdate(firstDoc)), first);
  assert.throws(() => cloneNotebookDocument({ ...request, assetMap: {} }), /Missing notebook asset mapping/);
  source.destroy(); firstDoc.destroy(); secondDoc.destroy();
});

test('default template has three editable H2 prompts and empty answers; blank note starts empty', () => {
  const doc = defaultNotebookTemplate();
  const group = doc.getXmlFragment('document-store').get(0);
  assert.equal(group.length, 6);
  assert.equal(group.get(0).get(0).nodeName, 'heading');
  assert.equal(group.get(0).get(0).getAttribute('level'), 2);
  assert.equal(group.get(1).get(0).nodeName, 'paragraph');
  assert.match(projectNotebookDocument(doc).plainText, /Hvad arbejdede jeg med\?/);
  assert.match(cloneNotebookDocument({ pageId: id, defaultTemplate: true }).plainText, /Hvad er mit næste skridt\?/);
  assert.equal(cloneNotebookDocument({ pageId: id }).plainText, '');
  assert.throws(() => cloneNotebookDocument({ pageId: 'invalid', defaultTemplate: true }));
  assert.throws(() => cloneNotebookDocument({ pageId: id, stateBase64: 'invalid' }));
  const fixture = JSON.parse(readFileSync(new URL('./fixtures/default-notebook-template.json', import.meta.url)));
  assert.deepEqual(notebookStateProjection(Buffer.from(fixture.stateBase64, 'base64')), { ...fixture, drawingIds: [], drawingBlocks: {}, references: [] });
  doc.destroy();
});

test('nested notebook drawing blocks remap to independent drawings and fresh block IDs', () => {
  const source = new Y.Doc({ gc: false });
  const drawing = element('drawing', { drawingId: oldAsset, schemaVersion: 1 });
  const container = element('blockContainer', { id: 'old-drawing-block' }, [drawing]);
  source.getXmlFragment('document-store').insert(0, [element('blockGroup', {}, [element('blockContainer', { id: 'parent' }, [element('paragraph', {}, []), element('blockGroup', {}, [container])])])]);
  assert.deepEqual(projectNotebookDocument(source).drawingIds, [oldAsset]);
  const request = { stateBase64: encode(source), pageId: targetId, drawingMap: { [oldAsset]: newAsset } };
  const result = cloneNotebookDocument(request), copy = decode(result.stateBase64);
  assert.deepEqual(result.drawingIds, [newAsset]);
  const copied = copy.getXmlFragment('document-store').get(0).get(0).get(1).get(0);
  assert.notEqual(copied.getAttribute('id'), 'old-drawing-block');
  assert.equal(result.drawingBlocks[newAsset], copied.getAttribute('id'));
  assert.equal(copied.get(0).getAttribute('drawingId'), newAsset);
  assert.throws(() => cloneNotebookDocument({ ...request, drawingMap: {} }), /Missing notebook drawing/);
  assert.throws(() => cloneNotebookDocument({ ...request, drawingMap: { [oldAsset]: oldAsset } }), /Invalid notebook drawing/);
  source.destroy(); copy.destroy();
});

test('nullable C# drawingMap transport keeps legacy blank template creation working', () => {
  const projection = cloneNotebookDocument({ stateBase64: null, pageId: id, assetMap: {}, defaultTemplate: false, drawingMap: null });
  assert.equal(projection.plainText, '');
  assert.deepEqual(projection.drawingIds, []);
  assert.deepEqual(projection.drawingBlocks, {});
});

test('typed and legacy references project across domains without labels or deleted history', () => {
  const doc = new Y.Doc({ gc: false });
  const group = element('blockGroup', {}, [
    element('blockContainer', { id: 'legacy' }, [element('pageLink', { pageId: targetId })]),
    element('blockContainer', { id: 'typed' }, [element('paragraph', {}, [element('entityMention', { kind: 'person', id })])]),
    element('blockContainer', { id: 'slug' }, [element('entityLink', { kind: 'subject', id: 'database/joins' })]),
    element('blockContainer', { id: 'poll' }, [element('holdPoll', { holdId: id, pollId: oldAsset })]),
    element('blockContainer', { id: 'removed' }, [element('entityMention', { kind: 'person', id: newAsset })]),
  ]);
  doc.getXmlFragment('document-store').insert(0, [group]); group.delete(4, 1);
  assert.deepEqual(projectNotebookDocument(doc).references, [
    { kind: 'notebook-page', id: targetId }, { kind: 'person', id }, { kind: 'poll', id: oldAsset }, { kind: 'subject', id: 'database/joins' },
  ]);
  assert.equal(projectNotebookDocument(doc, 'notion').references[0].kind, 'notion-page');
  assert.deepEqual(projectNotebookDocument(doc).linkIds, [targetId]);
  assert.equal(JSON.stringify(projectNotebookDocument(doc).references).includes(newAsset), false);
  doc.destroy();
});
