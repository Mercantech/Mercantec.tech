import test from 'node:test';
import assert from 'node:assert/strict';
import * as Y from 'yjs';
import { publishedDocumentFromBase64 } from '../src/persistence.mjs';
import { pageUrl, studentUrl, collectPublicationAssetIds } from '../src/document-utils.mjs';

const page='ba72e62b-e57e-43af-b655-f8cdd2aeacbe';
const hold='eda7caef-508e-46a7-97c5-b0fe1b643769';
const pupil='5c48e52c-558b-47f1-b110-e58078417c7e';
const asset='3edb6a2d-f6ba-4a47-a9ad-4198e4317a7f';

test('published document stays independent while teacher continues the collaborative draft',()=>{
  const draft=new Y.Doc();
  const text=new Y.XmlText();text.insert(0,'Udgivet teori');
  draft.getXmlFragment('document-store').insert(0,[text]);
  const encoded=Buffer.from(Y.encodeStateAsUpdate(draft)).toString('base64');
  const firstReader=publishedDocumentFromBase64(encoded);
  text.delete(0,text.length);text.insert(0,'Lærerens næste kladde');
  const secondReader=publishedDocumentFromBase64(encoded);
  assert.equal(firstReader.getXmlFragment('document-store').toString(),'Udgivet teori');
  assert.equal(secondReader.getXmlFragment('document-store').toString(),'Udgivet teori');
  assert.equal(draft.getXmlFragment('document-store').toString(),'Lærerens næste kladde');
  // Changes in one local reader also cannot mutate the publication or another reader.
  firstReader.getXmlFragment('document-store').delete(0,1);
  assert.equal(secondReader.getXmlFragment('document-store').toString(),'Udgivet teori');
  draft.destroy();firstReader.destroy();secondReader.destroy();
});

test('invalid publication bytes are rejected without creating an editable blank document',()=>{
  assert.throws(()=>publishedDocumentFromBase64(''));
  assert.throws(()=>publishedDocumentFromBase64('not valid base64!'));
});

test('page and single pupil links contain stable identifiers and reject executable URLs',()=>{
  assert.equal(pageUrl(page),'/notion/'+page);
  assert.equal(pageUrl(page,true),'/notion/'+page+'?published=true');
  assert.equal(studentUrl(hold,pupil),'/notion/holds/'+hold+'/students/'+pupil);
  assert.equal(pageUrl('javascript:alert(1)'),null);
  assert.equal(studentUrl(hold,'../admin'),null);
});

test('publication collects only actual nested media references and deduplicates IDs',()=>{
  const blocks=[{type:'paragraph',children:[{type:'pageMedia',props:{pageId:page,assetId:asset,kind:'video'}}]}, {type:'pageMedia',props:{pageId:page,assetId:asset}}, {type:'pageMedia',props:{pageId:page,assetId:''}}];
  assert.deepEqual(collectPublicationAssetIds(blocks,page),[asset]);
  assert.throws(()=>collectPublicationAssetIds([{type:'pageMedia',props:{pageId:hold,assetId:asset}}],page));
  assert.throws(()=>collectPublicationAssetIds([{type:'pageMedia',props:{pageId:page,assetId:'unsafe'}}],page));
});
