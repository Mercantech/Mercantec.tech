const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const source=fs.readFileSync(path.join(__dirname,'../web/wwwroot/service-worker.published.js'),'utf8');
const origin='https://lup.example';
function setup(){
  const events={},network=[],stored=new Map();let offline=false;
  const key=value=>new URL(typeof value==='string'?value:value.url,origin+'/').href;
  const cache={async put(request,response){stored.set(key(request),response.clone());},async match(request){return stored.get(key(request))?.clone();}};
  const self={origin,location:origin+'/service-worker.js',assetsManifest:{version:'avatar-test',assets:[{url:'css/app.css'},{url:'js/auth-storage.js'},{url:'js/lup-avatars.js'},{url:'js/avatar-layer-preview.js'},{url:'assets/avatars/raymond/sprites/idle.png'},{url:'assets/avatars/raymond/manifest.json'},{url:'assets/avatars/raymond/audio/walk.wav'},{url:'assets/avatars/bip-01/face.png'}]},importScripts(){},addEventListener(name,fn){events[name]=fn;},skipWaiting(){},clients:{async claim(){}}};
  self.assetsManifest.assets.push({url:'images/characters/raymond/infographics/01-databasedesign.png'},{url:'images/characters/raymond/infographics/01-databasedesign.webp'});
  const context={self,URL,Request,Response,console:{info(){},warn(){}},caches:{async open(){return cache;},async keys(){return[];},async delete(){return true;}},async fetch(request){if(offline)throw new Error('offline');const url=key(request);network.push(url);return new Response('network:'+url,{status:200});}};
  vm.runInNewContext(source,context);
  return{network,stored,setOffline(value){offline=value;},async install(){let pending;events.install({waitUntil(value){pending=value;}});await pending;},async get(url){let response;events.fetch({request:new Request(new URL(url,origin)),respondWith(value){response=value;}});return await response;}};
}
test('worker installation does not download optional avatar graphics, metadata or runtime modules',async()=>{
  const worker=setup();await worker.install();
  assert.ok(worker.network.includes(origin+'/css/app.css'));
  assert.equal(worker.network.some(url=>url.includes('/assets/avatars/')),false);
  assert.equal(worker.network.some(url=>/lup-avatars|avatar-layer-preview/.test(url)),false);
});
test('an avatar sheet is cached when used and remains available offline without downloading other actions',async()=>{
  const worker=setup(),url='/assets/avatars/raymond/sprites/idle.png';
  assert.equal((await worker.get(url)).status,200);
  assert.equal(worker.network.length,1);
  worker.setOffline(true);
  assert.equal(await (await worker.get(url)).text(),'network:'+origin+url);
  assert.equal(worker.network.length,1);
  assert.equal(worker.stored.size,1);
});

test('installing the app does not download the infographic collection or its large PNG originals',async()=>{
  const worker=setup();await worker.install();
  assert.equal(worker.network.some(url=>url.includes('/infographics/')),false);
});

for(const extension of ['png','webp']) test(`an opened infographic ${extension} is cached without fetching the rest of the collection`,async()=>{
  const worker=setup(),url='/images/characters/raymond/infographics/01-databasedesign.'+extension;
  assert.equal((await worker.get(url)).status,200);
  assert.equal(worker.network.length,1);
  assert.equal(worker.stored.size,1);
  worker.setOffline(true);
  assert.equal(await(await worker.get(url)).text(),'network:'+origin+url);
  assert.equal((await worker.get('/images/characters/raymond/infographics/02-relationer-og-noegler.'+extension)).status,504);
  assert.equal(worker.network.length,1);
});
test('avatar runtime modules use the network first and preserve the exact deployed version for offline fallback',async()=>{
  const worker=setup(),url='/js/avatar-layer-preview.js?v=1';
  await worker.get(url);await worker.get(url);
  assert.equal(worker.network.length,2);
  worker.setOffline(true);
  assert.equal(await (await worker.get(url)).text(),'network:'+origin+url);
  assert.equal((await worker.get('/js/avatar-layer-preview.js?v=2')).status,504);
});
test('personal avatar API responses are never put in the public asset cache',async()=>{
  const worker=setup();await worker.get('/api/me/avatars');
  assert.equal(worker.stored.size,0);
  worker.setOffline(true);
  const response=await worker.get('/api/me/avatars');
  assert.equal(response.status,503);
  assert.deepEqual(await response.json(),{error:'offline'});
});
