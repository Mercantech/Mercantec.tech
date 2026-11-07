import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { pageUrl } from '../src/document-utils.mjs';
import { applyReadOnlySnapshot } from '../src/persistence.mjs';
import { uploadNotebookAsset, LARGE_UPLOAD_CONFIRM_BYTES } from '../src/asset-upload.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const encode = doc => Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64');

test('notebook links retain stable IDs without entering Notion published routes', () => {
  assert.equal(pageUrl(id, true, 'notebook'), '/notebook/' + id);
  assert.equal(pageUrl(id, true, { domain: 'notebook', workspaceId: id, courseId: id, holdId: id }), '/notebook/' + id);
  assert.equal(pageUrl(id, false, { domain: 'notebook' }), '/notebook/' + id);
  assert.equal(pageUrl(id, true), '/notion/' + id + '?published=true');
  assert.equal(pageUrl('not-an-id', false, 'notebook'), null);
});

test('reader refresh applies edits and deletions to the same document instead of replacing it', () => {
  const source = new Y.Doc(), reader = new Y.Doc();
  const text = new Y.XmlText();
  source.getXmlFragment('document-store').insert(0, [text]);
  text.insert(0, 'Første udgave');
  applyReadOnlySnapshot(reader, encode(source));
  const originalFragment = reader.getXmlFragment('document-store');
  text.delete(0, text.length);
  text.insert(0, 'Senest gemte tekst');
  applyReadOnlySnapshot(reader, encode(source));
  assert.equal(reader.getXmlFragment('document-store'), originalFragment);
  assert.equal(originalFragment.toString(), 'Senest gemte tekst');
  assert.throws(() => applyReadOnlySnapshot(reader, 'invalid'));
  assert.equal(originalFragment.toString(), 'Senest gemte tekst');
  source.destroy(); reader.destroy();
});

test('notebook upload sends a page-authorized file and rejects oversize before network activity', async () => {
  let requests = 0;
  const file = new File(['example'], 'note.txt', { type: 'text/plain' });
  const asset = await uploadNotebookAsset(file, {
    pageId: id, getToken: async () => 'test-token',
    fetchImpl: async (url, request) => {
      requests++;
      assert.equal(url, '/api/notebooks/pages/' + id + '/assets');
      assert.equal(request.headers.Authorization, 'Bearer test-token');
      assert.equal(request.body.get('file').name, 'note.txt');
      return Response.json({ id: 'asset-id', kind: 'download' });
    },
  });
  assert.equal(asset.kind, 'download');
  await assert.rejects(uploadNotebookAsset({ size: LARGE_UPLOAD_CONFIRM_BYTES + 1 }, {
    pageId: id, getToken: async () => 'test-token', fetchImpl: async () => { requests++; },
  }), /100 MiB/);
  assert.equal(requests, 1);
});
