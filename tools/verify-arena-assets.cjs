// Verify production HTTP delivery of the authored Arena assets.
const fs=require('node:fs');const path=require('node:path');
const root=path.resolve(__dirname,'..');const base=process.argv[2]||'http://127.0.0.1:3082';
const files=fs.readdirSync(path.join(root,'web/wwwroot/assets/arena/v2')).filter(n=>n.endsWith('.webp'));
(async()=>{const results=await Promise.all(['/arena','/js/arena-client.js','/js/arena-renderer.js',...files.map(n=>'/assets/arena/v2/'+n)].map(async name=>{const response=await fetch(base+name);if(!response.ok)throw new Error(name+': '+response.status);const bytes=(await response.arrayBuffer()).byteLength;if(!bytes)throw new Error('Empty asset '+name);return {path:name,status:response.status,bytes};}));console.log(JSON.stringify({verified:results.length,assets:results},null,2));})().catch(error=>{console.error(error.message);process.exitCode=1;});
