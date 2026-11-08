// Preview the actual published Blazor artifact without a database or login.
// node tools/arena-preview.cjs [port]
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const base = path.resolve(__dirname, '../web/bin/Release/net10.0/publish/wwwroot');
if (!fs.existsSync(path.join(base, 'index.html'))) throw new Error('Run dotnet publish web -c Release first.');
const types = {'.html':'text/html; charset=utf-8','.js':'application/javascript','.mjs':'application/javascript','.css':'text/css','.json':'application/json','.wasm':'application/wasm','.png':'image/png','.jpg':'image/jpeg','.jpeg':'image/jpeg','.svg':'image/svg+xml','.webp':'image/webp','.woff2':'font/woff2','.dat':'application/octet-stream','.bin':'application/octet-stream','.wav':'audio/wav','.webmanifest':'application/manifest+json'};
const server=http.createServer((req,res)=>{
  let name;try{name=decodeURIComponent(new URL(req.url,'http://localhost').pathname);}catch{res.writeHead(400).end();return;}
  let file=path.resolve(base,'.'+name);
  if(file!==base&&!file.startsWith(base+path.sep)){res.writeHead(403).end();return;}
  if(req.method!=='GET'&&req.method!=='HEAD'){res.writeHead(405).end();return;}
  if(name.startsWith('/api/')){res.writeHead(503,{'Content-Type':'application/json'}).end(JSON.stringify({detail:'Denne forhåndsvisning har kun lokal træning. Onlinekampe bruger LUPs API.'}));return;}
  if(!fs.existsSync(file)||!fs.statSync(file).isFile()){if(path.extname(name)){res.writeHead(404).end();return;}file=path.join(base,'index.html');}
  res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'});
  if(req.method==='HEAD')res.end();else fs.createReadStream(file).pipe(res);
});
const port=Number(process.argv[2]||3080);server.listen(port,'127.0.0.1',()=>console.log('Published LUP Arena preview: http://127.0.0.1:'+port+'/arena'));
