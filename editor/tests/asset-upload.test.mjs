import test from 'node:test';
import assert from 'node:assert/strict';
import { LARGE_UPLOAD_CONFIRM_BYTES, uploadNotionAsset } from '../src/asset-upload.mjs';

const MiB=1024*1024;
const file=(size)=>({name:'Undervisning.mp4',type:'video/mp4',size,slices:[],slice(start,end){const part={start,end,size:end-start};this.slices.push(part);return part;}});
const response=(body,status=200)=>({ok:status>=200 && status<300,status,json:async()=>body});
function fixture(overrides={}) {
  const calls=[];let tokens=0;
  return {calls,get tokens(){return tokens;},options:{pageId:'page-id',getToken:async()=>String(++tokens),confirmLargeUpload:()=>true,delay:async()=>{},fetchImpl:async(url,options)=>{
    calls.push({url,...options});
    const result=await overrides.fetch?.(url,options,calls);
    if(result)return result;
    if(url.endsWith('/uploads'))return response({id:'upload-id',chunkSizeBytes:overrides.chunkSize || 8*MiB});
    if(url.endsWith('/complete'))return response({id:'asset-id',kind:'video'});
    return response(null,204);
  }}};
}

test('confirmation is strictly above100MiB and rejecting it sends no request or token lookup',async()=>{
  const large=file(LARGE_UPLOAD_CONFIRM_BYTES+1),f=fixture();let asked=0;
  const result=await uploadNotionAsset(large,{...f.options,confirmLargeUpload:selected=>{assert.equal(selected,large);asked++;return false;}});
  assert.equal(result,null);assert.equal(asked,1);assert.equal(f.tokens,0);assert.equal(f.calls.length,0);assert.equal(large.slices.length,0);
  await uploadNotionAsset(file(LARGE_UPLOAD_CONFIRM_BYTES),{...f.options,confirmLargeUpload:()=>assert.fail('100MiB does not require confirmation')});
  assert.equal(f.calls.filter(call=>call.method==='POST' && call.url.endsWith('/uploads')).length,1);
});

test('accepted101MiB is sliced without reading the whole file; progress follows acknowledged bytes',async()=>{
  const selected=file(101*MiB),f=fixture(),progress=[];
  const result=await uploadNotionAsset(selected,{...f.options,onProgress:(sent,total)=>progress.push([sent,total])});
  assert.equal(result.id,'asset-id');
  assert.deepEqual(JSON.parse(f.calls[0].body),{originalName:selected.name,contentType:selected.type,sizeBytes:selected.size});
  const parts=f.calls.filter(call=>call.method==='PUT');
  assert.equal(parts.length,13);
  assert.deepEqual(parts.map(part=>part.body.size),[...Array(12).fill(8*MiB),5*MiB]);
  assert.deepEqual(parts.map(part=>part.body.start),Array.from({length:13},(_,i)=>i*8*MiB));
  assert.equal(parts.at(-1).url.endsWith('/parts/13'),true);
  assert.deepEqual(progress.at(-1),[selected.size,selected.size]);
  assert.equal(progress.length,14);
  assert.equal(f.tokens,f.calls.length);
  assert.deepEqual(f.calls.map(call=>call.headers.Authorization),f.calls.map((_,i)=>'Bearer '+(i+1)));
  assert.equal(f.calls.some(call=>call.method==='DELETE'),false);
});

test('uses the server chunk size for files larger than previous application limits',async()=>{
  // Virtual90GiB file: no allocation, but all actual slice boundaries and request sizes are checked.
  const selected=file(90*1024**3+3),f=fixture({chunkSize:64*MiB});
  await uploadNotionAsset(selected,f.options);
  assert.equal(selected.slices.length,1441);
  assert.equal(selected.slices.at(-1).size,3);
  assert.ok(selected.slices.every(part=>part.size<=64*MiB));
});

test('retries an ambiguous part and completion response with the same session and fresh tokens',async()=>{
  let partAttempts=0,completeAttempts=0;
  const f=fixture({fetch:(url)=>{
    if(url.endsWith('/parts/1') && ++partAttempts===1)throw new TypeError('connection lost after acceptance');
    if(url.endsWith('/complete') && ++completeAttempts===1)throw new TypeError('completion response lost');
  }});
  const selected=file(9*MiB);await uploadNotionAsset(selected,f.options);
  assert.equal(partAttempts,2);assert.equal(completeAttempts,2);
  const attempts=f.calls.filter(call=>call.url.endsWith('/parts/1'));
  assert.equal(attempts[0].body,attempts[1].body);
  assert.equal(selected.slices.length,2);
  assert.equal(f.calls.filter(call=>call.url.endsWith('/uploads')).length,1);
  assert.equal(f.calls.some(call=>call.method==='DELETE'),false);
});

test('permission loss stops immediately and cleans the incomplete session',async()=>{
  const f=fixture({fetch:url=>url.includes('/parts/')?response(null,403):undefined});
  await assert.rejects(uploadNotionAsset(file(20*MiB),f.options),/ikke længere adgang/);
  assert.equal(f.calls.filter(call=>call.method==='PUT').length,1);
  assert.equal(f.calls.at(-1).method,'DELETE');
  assert.equal(f.calls.some(call=>call.url.endsWith('/complete')),false);
});

test('a lost response waits for the original server operation without retrying other conflicts',async()=>{
  let partAttempts=0;const waits=[];
  const f=fixture({fetch:url=>{
    if(!url.includes('/parts/'))return;
    partAttempts++;
    if(partAttempts===1)throw new TypeError('response lost while S3 operation continues');
    if(partAttempts<=5)return {...response({error:'upload_busy'},409),headers:new Headers({'Retry-After':'2'})};
  }});
  await uploadNotionAsset(file(1),{...f.options,delay:async ms=>{waits.push(ms);}});
  assert.equal(partAttempts,6);assert.deepEqual(waits,[750,2000,2000,2000,2000]);
  assert.equal(f.calls.at(-1).url.endsWith('/complete'),true);
  const conflict=fixture({fetch:url=>url.includes('/parts/')?response({error:'part_mismatch'},409):undefined});
  await assert.rejects(uploadNotionAsset(file(1),conflict.options),/ikke længere tilgængelig/);
  assert.equal(conflict.calls.filter(call=>call.method==='PUT').length,1);
});

test('a permanently busy session stops after five minutes of bounded retry delays',async()=>{
  let waited=0;
  const f=fixture({fetch:url=>url.includes('/parts/')?response({error:'upload_busy'},409):undefined});
  await assert.rejects(uploadNotionAsset(file(1),{...f.options,delay:async ms=>{waited+=ms;}}),/ikke længere tilgængelig/);
  assert.equal(waited,5*60*1000);assert.equal(f.calls.at(-1).method,'DELETE');
});

test('cancellation aborts the in-flight part and uses a fresh signal for cleanup',async()=>{
  const controller=new AbortController();
  const f=fixture({fetch:(url,options)=>{
    if(url.includes('/parts/')){controller.abort();options.signal.throwIfAborted();}
    if(options.method==='DELETE')assert.equal(options.signal.aborted,false);
  }});
  await assert.rejects(uploadNotionAsset(file(20*MiB),{...f.options,signal:controller.signal}),{name:'AbortError'});
  assert.equal(f.calls.filter(call=>call.method==='PUT').length,1);
  assert.equal(f.calls.at(-1).method,'DELETE');
  assert.equal(f.calls.some(call=>call.url.endsWith('/complete')),false);
});

test('cancellation while obtaining an access token never starts upload networking',async()=>{
  const controller=new AbortController(),f=fixture();
  await assert.rejects(uploadNotionAsset(file(1),{...f.options,signal:controller.signal,getToken:async()=>{controller.abort();return 'token';}}),{name:'AbortError'});
  assert.equal(f.calls.length,0);
});

test('invalid files and unbounded server chunk sizes do not cause file reads',async()=>{
  const f=fixture({chunkSize:65*MiB});
  await assert.rejects(uploadNotionAsset(file(0),f.options),/med indhold/);
  assert.equal(f.calls.length,0);
  const selected=file(2*MiB);
  await assert.rejects(uploadNotionAsset(selected,f.options),/ugyldig upload/);
  assert.equal(selected.slices.length,0);assert.equal(f.calls.at(-1).method,'DELETE');
});
