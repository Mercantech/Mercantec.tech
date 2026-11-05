// Loopback-only browser fixture. Never shipped as application code or included in Docker.
import http from 'node:http';
import net from 'node:net';
import { readFile } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCollaborationServer } from '../../collaboration/src/server.mjs';
const root=resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const port=9098, collaborationPort=9099;
const secret='qa-only-service-secret-32-characters-long';
const pageId='ba72e62b-e57e-43af-b655-f8cdd2aeacbe';
const holdId='eda7caef-508e-46a7-97c5-b0fe1b643769';
const state=new Map();
const token='eyJhbGciOiJIUzI1NiJ9.'+Buffer.from(JSON.stringify({sub:'qa-teacher',exp:Math.floor(Date.now()/1000)+3600})).toString('base64url')+'.qa-fixture-only';
const context={pageId,holdId,title:'Introduktion til spiludvikling',canEdit:true,members:[{id:'5c48e52c-558b-47f1-b110-e58078417c7e',displayName:'QA Elev Anna'},{id:'8a84d18b-2b66-4411-a57a-56df353f59dc',displayName:'QA Elev Emil'},{id:'2eb0e699-59a1-4471-953c-ed2a096b08ac',displayName:'QA Elev Noor'}],polls:[{id:'988c8e53-83b2-431f-ae1e-f2133623525e',title:'Hvilken spilgenre vil I arbejde med?',state:'open'}],updatedAtUtc:new Date().toISOString(),userName:'QA Underviser'};
const json=(res,status,data)=>{res.writeHead(status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(data));};
const server=http.createServer(async(req,res)=>{
  try{
    const url=new URL(req.url,'http://127.0.0.1:'+port);
    if(req.method==='POST' && url.pathname==='/api/notion/collaboration/authorize'){
      let body='';for await(const chunk of req)body+=chunk;
      if(req.headers.authorization!=='Bearer '+token || JSON.parse(body).documentName!=='notion:'+pageId)return json(res,403,{error:'forbidden'});
      return json(res,200,{documentId:pageId,canEdit:true});
    }
    if(url.pathname==='/api/internal/notion/documents/'+pageId+'/state'){
      if(req.headers['x-notion-service-key']!==secret)return json(res,403,{error:'forbidden'});
      if(req.method==='GET'){const value=state.get(pageId);res.writeHead(value?200:204,{'Content-Type':'application/octet-stream'});return res.end(value);}
      if(req.method==='PUT'){const chunks=[];for await(const chunk of req)chunks.push(chunk);state.set(pageId,Buffer.concat(chunks));res.writeHead(204);return res.end();}
    }
    if(url.pathname==='/' || url.pathname==='/notion'){
      res.writeHead(200,{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store'});
      return res.end('<!doctype html><html lang="da" style="color-scheme:light"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>LUP editorprøve</title><link rel="stylesheet" href="/lup/lup.css"><link rel="stylesheet" href="/notion-editor/editor.css"><body class="lup"><main style="max-width:1080px;margin:36px auto;padding:0 20px"><h1>Introduktion til spiludvikling</h1><p style="margin:10px 0 22px">Browserprøve med fiktivt hold. <strong id="status">Forbinder…</strong></p><section id="editor" style="border:1px solid var(--rule);border-radius:10px;overflow:hidden;background:var(--paper)"></section></main><script type="module">import {mount} from "/notion-editor/editor.js"; await mount(document.getElementById("editor"),'+JSON.stringify(context)+','+JSON.stringify(token)+',{invokeMethodAsync:async(method,status,detail)=>{if(method==="GetNotionToken")return '+JSON.stringify(token)+';document.getElementById("status").textContent=detail;}});</script></body></html>');
    }
    let file;if(url.pathname.startsWith('/notion-editor/'))file=resolve(root,'web/wwwroot', '.'+url.pathname);
    else if(url.pathname==='/lup/lup.css')file=resolve(root,'web/wwwroot/lup/lup.css');
    if(!file || !file.startsWith(resolve(root,'web/wwwroot')+sep))return json(res,404,{error:'not_found'});
    const body=await readFile(file);res.writeHead(200,{'Content-Type':({'.js':'text/javascript','.css':'text/css','.txt':'text/plain'}[extname(file)] || 'application/octet-stream'),'Cache-Control':'no-store'});res.end(body);
  }catch{json(res,404,{error:'fixture_request_failed'});}
});
server.on('upgrade',(request,socket,head)=>{
  if(!request.url.startsWith('/collaboration'))return socket.destroy();
  const upstream=net.connect(collaborationPort,'127.0.0.1',()=>{
    const headers=Object.entries(request.headers).map(([key,value])=>key+': '+value).join('\r\n');
    upstream.write('GET '+request.url+' HTTP/1.1\r\n'+headers+'\r\n\r\n');if(head.length)upstream.write(head);socket.pipe(upstream).pipe(socket);
  });
  upstream.on('error',()=>socket.destroy());socket.on('error',()=>upstream.destroy());
});
await new Promise(resolve=>server.listen(port,'127.0.0.1',resolve));
const collaboration=createCollaborationServer({address:'127.0.0.1',port:collaborationPort,apiBaseUrl:'http://127.0.0.1:'+port,serviceSecret:secret,allowedOrigins:['http://127.0.0.1:'+port]});
await collaboration.listen();
console.log('Browser QA fixture ready: http://127.0.0.1:'+port+'/notion');
for(const signal of ['SIGINT','SIGTERM'])process.once(signal,async()=>{await collaboration.destroy();server.close(()=>process.exit(0));});
