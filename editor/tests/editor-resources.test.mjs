import test from 'node:test';
import assert from 'node:assert/strict';
import { ensureEditorResources } from '../src/editor-resources.mjs';

test('a plain document and notebooks do not request hold resources', async () => {
  let requests=0;
  const callbacks={invokeMethodAsync:async()=>{requests++;}};
  await ensureEditorResources({context:{resourcesLoaded:true},callbacks});
  await ensureEditorResources({context:{domain:'notebook',resourcesLoaded:false},callbacks});
  await ensureEditorResources({destroyed:true,context:{resourcesLoaded:false},callbacks});
  assert.equal(requests,0);
});

test('several blocks or menu actions share one in-flight resource request', async () => {
  let requests=0, complete;
  const session={context:{resourcesLoaded:false},callbacks:{invokeMethodAsync:async method=>{
    assert.equal(method,'EnsureNotionResources');requests++;
    await new Promise(resolve=>{complete=resolve;});
    session.context={resourcesLoaded:true};
    return true;
  }}};
  const pending=Array.from({length:12},()=>ensureEditorResources(session));
  assert.equal(requests,1);
  complete();
  await Promise.all(pending);
  await ensureEditorResources(session);
  assert.equal(requests,1);
  assert.equal(session.resourcesTask,undefined);
});

test('a failed resource request can be retried without leaving a poisoned promise', async () => {
  let requests=0;
  const session={context:{resourcesLoaded:false},callbacks:{invokeMethodAsync:async()=>{
    if(++requests===1)return false;
    session.context={resourcesLoaded:true};
    return true;
  }}};
  await assert.rejects(ensureEditorResources(session),/Prøv igen/);
  assert.equal(session.resourcesTask,undefined);
  await ensureEditorResources(session);
  assert.equal(requests,2);
});
