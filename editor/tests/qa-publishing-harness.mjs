// Loopback-only fixture with fictitious pupils. Never shipped to production.
import http from 'node:http';
import net from 'node:net';
import {readFile} from 'node:fs/promises';
import {resolve,dirname,extname,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createCollaborationServer} from '../../collaboration/src/server.mjs';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const port=9098,collaborationPort=9099,secret='qa-only-service-secret-32-characters-long';
const pageId='ba72e62b-e57e-43af-b655-f8cdd2aeacbe',holdId='eda7caef-508e-46a7-97c5-b0fe1b643769';
const nextPage='a4934c5c-c9a1-4a1e-bfa8-b680ca315680',draftPage='f4566031-ac07-4ef8-a3e2-974d35495aad';
const token=sub=>'eyJhbGciOiJIUzI1NiJ9.'+Buffer.from(JSON.stringify({sub,exp:Math.floor(Date.now()/1000)+3600})).toString('base64url')+'.qa-fixture-only';
const teacherToken=token('qa-teacher'),studentToken=token('qa-student');
let state=null,publication=null,upgradeCount=0,uploadCount=0,uploadBytes=0,abortedUploads=0,completedUploads=0;
const uploads=new Map(),assets=new Map();
const context={pageId,holdId,title:'Introduktion til spiludvikling',canEdit:true,members:[{id:'5c48e52c-558b-47f1-b110-e58078417c7e',displayName:'QA Elev Anna'},{id:'8a84d18b-2b66-4411-a57a-56df353f59dc',displayName:'QA Elev Emil'},{id:'2eb0e699-59a1-4471-953c-ed2a096b08ac',displayName:'QA Elev Noor'}],polls:[],pages:[{id:pageId,title:'Introduktion til spiludvikling'},{id:nextPage,title:'Øvelse: den første prototype',parentPageId:pageId},{id:draftPage,title:'Skjult lærerkladde'}],groups:[],updatedAtUtc:new Date().toISOString(),userName:'QA Underviser'};
const json=(res,status,data)=>{res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(data));};
const body=async req=>{const chunks=[];for await(const chunk of req)chunks.push(chunk);return Buffer.concat(chunks);};
const server=http.createServer(async(req,res)=>{
  try{
    const url=new URL(req.url,'http://127.0.0.1:'+port);
    if(req.method==='POST' && url.pathname==='/api/notion/collaboration/authorize'){
      const input=JSON.parse((await body(req)).toString());return req.headers.authorization==='Bearer '+teacherToken && input.documentName==='notion:'+pageId ? json(res,200,{documentId:pageId,canEdit:true}) : json(res,403,{error:'forbidden'});
    }
    if(url.pathname==='/api/internal/notion/documents/'+pageId+'/state'){
      if(req.headers['x-notion-service-key']!==secret)return json(res,403,{error:'forbidden'});
      if(req.method==='GET'){res.writeHead(state?200:204,{'Content-Type':'application/octet-stream'});return res.end(state);}
      if(req.method==='PUT'){state=await body(req);return json(res,200,{savedAtUtc:new Date().toISOString()});}
    }
    if(req.method==='POST' && url.pathname==='/qa/publish'){if(!state)return json(res,409,{error:'not_saved'});publication={state:Buffer.from(state),at:new Date().toISOString()};return json(res,200,{published:true});}
    if(url.pathname==='/qa/stats')return json(res,200,{upgradeCount,published:Boolean(publication),uploadCount,uploadBytes,abortedUploads,completedUploads});
    const uploadPath='/api/notion/pages/'+pageId+'/assets/uploads';
    if(req.method==='POST' && url.pathname===uploadPath){
      const input=JSON.parse((await body(req)).toString()),id='fixture-upload-'+(++uploadCount);uploads.set(id,{...input,bytes:0});return json(res,200,{id,chunkSizeBytes:8*1024*1024});
    }
    if(url.pathname.startsWith(uploadPath+'/')){
      const [id,operation]=url.pathname.slice(uploadPath.length+1).split('/'),upload=uploads.get(id);
      if(!upload)return json(res,404,{error:'missing_upload'});
      if(req.method==='DELETE'){uploads.delete(id);abortedUploads++;return json(res,204,null);}
      if(req.method==='PUT' && operation==='parts'){
        let bytes=0;for await(const chunk of req)bytes+=chunk.length;
        // Keep progress/cancellation observable without retaining the file in memory.
        await new Promise(resolve=>setTimeout(resolve,350));upload.bytes+=bytes;uploadBytes+=bytes;return json(res,204,null);
      }
      if(req.method==='POST' && operation==='complete'){
        if(upload.bytes!==upload.sizeBytes)return json(res,409,{error:'incomplete_upload'});
        upload.contentType=upload.contentType || ({'.txt':'text/plain','.mp4':'video/mp4','.webm':'video/webm','.pdf':'application/pdf'}[extname(upload.originalName).toLowerCase()] || 'application/octet-stream');
        upload.kind=upload.contentType.startsWith('video/')?'video':upload.contentType.startsWith('audio/')?'audio':upload.contentType.startsWith('image/')?'image':upload.contentType==='application/pdf'?'pdf':'download';
        const id='3edb6a2d-f6ba-4a47-a9ad-4198e4317a7f';assets.set(id,upload);completedUploads++;return json(res,200,{id,kind:upload.kind,originalName:upload.originalName,contentType:upload.contentType,sizeBytes:upload.sizeBytes});
      }
    }
    const accessMatch=url.pathname.match(/^\/api\/notion\/assets\/([^/]+)\/access$/);
    if(accessMatch){const asset=assets.get(accessMatch[1]);return asset?json(res,200,{url:'/qa/asset/'+accessMatch[1],downloadUrl:'/qa/asset/'+accessMatch[1],expiresAtUtc:new Date(Date.now()+300000).toISOString(),originalName:asset.originalName,contentType:asset.contentType,kind:asset.kind,sizeBytes:asset.sizeBytes}):json(res,404,{error:'not_found'});}
    if(url.pathname.startsWith('/qa/asset/')){const asset=assets.get(url.pathname.slice('/qa/asset/'.length));if(!asset)return json(res,404,{error:'not_found'});res.writeHead(200,{'Content-Type':asset.contentType});return res.end(Buffer.from('Fiktiv undervisningsfil til uploadtesten.'));}
    if(req.method==='POST' && url.pathname==='/qa/groups'){const ids=JSON.parse((await body(req)).toString());context.groups.push({id:'11111111-1111-4111-8111-111111111111',name:'Projektgruppe '+(context.groups.length+1),members:context.members.filter(item=>ids.includes(item.id))});return json(res,200,context.groups);}
    if(url.pathname==='/notion/'+nextPage){res.writeHead(200,{'Content-Type':'text/html;charset=utf-8'});return res.end('<h1>Øvelse: den første prototype</h1><p>Undersiden er åbnet fra dokumentet.</p>');}
    if(url.pathname.startsWith('/notion/holds/')){res.writeHead(200,{'Content-Type':'text/html;charset=utf-8'});return res.end('<h1>QA Elev Anna</h1><p>Elevens grundprofil på det fiktive hold.</p>');}
    if(url.pathname==='/' || url.pathname==='/notion' || url.pathname==='/student'){
      const readOnly=url.pathname==='/student';if(readOnly && !publication){res.writeHead(200,{'Content-Type':'text/html;charset=utf-8'});return res.end('<h1>Intet indhold er udgivet endnu</h1>');}
      const ctx=readOnly ? {...context,canEdit:false,userName:'QA Elev',publishedStateBase64:publication.state.toString('base64'),publishedAtUtc:publication.at,pages:context.pages.filter(item=>item.id!==draftPage)} : context;
      const accessToken=readOnly?studentToken:teacherToken;
      const script=`import {mount,mountReadOnly,getPublicationData,setContext} from '/notion-editor/editor.js';const context=${JSON.stringify(ctx)},token=${JSON.stringify(accessToken)},element=document.getElementById('editor');const callbacks={invokeMethodAsync:async(method,status,detail)=>{if(method==='GetNotionToken')return token;if(method==='OnNotionCreateGroup'){const response=await fetch('/qa/groups',{method:'POST',body:status});context.groups=await response.json();setContext(element,{...context,groups:context.groups});return;}document.getElementById('status').textContent=detail;}};await ${readOnly?'mountReadOnly':'mount'}(element,context,token,callbacks);${readOnly?'':`document.getElementById('publish').onclick=async()=>{try{await getPublicationData(element);const response=await fetch('/qa/publish',{method:'POST'});document.getElementById('publish-status').textContent=response.ok?'Udgivet til eleverne':'Kunne ikke udgives';}catch(error){document.getElementById('publish-status').textContent=error.message;}};`}document.getElementById('connections').textContent='WebSocket-forbindelser: '+(await fetch('/qa/stats').then(r=>r.json())).upgradeCount;`;
      res.writeHead(200,{'Content-Type':'text/html;charset=utf-8','Cache-Control':'no-store'});return res.end(`<!doctype html><html lang="da" style="color-scheme:light"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>LUP udgivelsesprøve</title><link rel="stylesheet" href="/lup/lup.css"><link rel="stylesheet" href="/notion-editor/editor.css"><body class="lup"><main style="max-width:1080px;margin:36px auto;padding:0 20px"><h1>${context.title}</h1><p style="margin:10px 0">${readOnly?'Elevens udgivne udgave':'Underviserens kladde'} · Fiktivt hold · <strong id="status">Forbinder…</strong></p><p id="connections"></p>${readOnly?'':`<p><button id="publish">Udgiv til eleverne</button> <span id="publish-status"></span> <a href="/student" target="_blank">Åbn elevudgave</a></p>`}<section id="editor" style="border:1px solid var(--rule);border-radius:10px;overflow:hidden;background:var(--paper);margin-top:20px"></section></main><script type="module">${script}</script></body></html>`);
    }
    let file;if(url.pathname.startsWith('/notion-editor/'))file=resolve(root,'web/wwwroot','.'+url.pathname);else if(url.pathname==='/lup/lup.css')file=resolve(root,'web/wwwroot/lup/lup.css');
    if(!file || !file.startsWith(resolve(root,'web/wwwroot')+sep))return json(res,404,{error:'not_found'});
    const bytes=await readFile(file);res.writeHead(200,{'Content-Type':({'.js':'text/javascript','.css':'text/css','.txt':'text/plain'}[extname(file)] || 'application/octet-stream'),'Cache-Control':'no-store'});res.end(bytes);
  }catch{json(res,404,{error:'fixture_request_failed'});}
});
server.on('upgrade',(request,socket,head)=>{
  if(!request.url.startsWith('/collaboration'))return socket.destroy();upgradeCount++;
  const upstream=net.connect(collaborationPort,'127.0.0.1',()=>{const headers=Object.entries(request.headers).map(([key,value])=>key+': '+value).join('\r\n');upstream.write('GET '+request.url+' HTTP/1.1\r\n'+headers+'\r\n\r\n');if(head.length)upstream.write(head);socket.pipe(upstream).pipe(socket);});upstream.on('error',()=>socket.destroy());socket.on('error',()=>upstream.destroy());
});
await new Promise(resolve=>server.listen(port,'127.0.0.1',resolve));const collaboration=createCollaborationServer({address:'127.0.0.1',port:collaborationPort,apiBaseUrl:'http://127.0.0.1:'+port,serviceSecret:secret,allowedOrigins:['http://127.0.0.1:'+port]});await collaboration.listen();
console.log('Publishing QA fixture: http://127.0.0.1:'+port+'/notion and /student');
for(const signal of ['SIGINT','SIGTERM'])process.once(signal,async()=>{await collaboration.destroy();server.close(()=>process.exit(0));});
