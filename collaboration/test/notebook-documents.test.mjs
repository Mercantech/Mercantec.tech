import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as Y from 'yjs';
import { cloneNotebookDocument, defaultNotebookTemplate, notebookStateProjection, projectNotebookDocument } from '../src/notebook-documents.mjs';

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
  assert.deepEqual(notebookStateProjection(Buffer.from(fixture.stateBase64, 'base64')), fixture);
  doc.destroy();
});
