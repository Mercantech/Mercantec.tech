// Full Blazor + real notebook controllers + real Yjs service. Explicit loopback fixture, never shipped.
import http from 'node:http';
import net from 'node:net';
import { spawn } from 'node:child_process';
import { readFile, stat } from 'node:fs/promises';
import { resolve, dirname, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createCollaborationServer } from '../../collaboration/src/server.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const webRoot = resolve(root, process.argv[2] ?? '.tools/notebook-qa-web/wwwroot');
const port = 9098, collaborationPort = 9099, apiPort = 8088;
const serviceKey = 'qa-notebooks-only-service-key-32-characters';
await stat(resolve(webRoot, 'index.html')).catch(() => {
  throw new Error('Publish the real web app first: dotnet publish web -c Release -o .tools/notebook-qa-web');
});
// Fail before starting children if one of our loopback ports belongs to another task.
for (const available of [port, collaborationPort, apiPort]) {
  await new Promise((accept, reject) => {
    const probe = net.createServer();
    probe.once('error', () => reject(new Error(`QA port ${available} is already in use; stop the owning fixture first.`)));
    probe.listen(available, '127.0.0.1', () => probe.close(accept));
  });
}

const api = spawn('dotnet', ['run', '--project', 'tests/Notebook.BrowserQa', '--no-launch-profile'], {
  cwd: root, windowsHide: true,
  env: { ...process.env, NOTEBOOK_BROWSER_QA: '1', ASPNETCORE_ENVIRONMENT: 'Development' },
  stdio: ['ignore', 'pipe', 'pipe'],
});
api.stdout.on('data', chunk => process.stdout.write(chunk));
api.stderr.on('data', chunk => process.stderr.write(chunk));
let apiExited = false;
api.once('exit', () => { apiExited = true; });
let collaboration, server;
let stopping = false;
async function shutdown() {
  if (stopping) return;
  stopping = true;
  if (server) await new Promise(accept => { server.closeAllConnections(); server.close(accept); });
  if (collaboration) await collaboration.destroy();
  api.kill();
}
process.once('SIGINT', () => void shutdown().then(() => process.exit(0)));
process.once('SIGTERM', () => void shutdown().then(() => process.exit(0)));
process.once('exit', () => api.kill());

try {
  const deadline = Date.now() + 120000;
  for (;;) {
    if (apiExited) throw new Error('Notebook QA API did not start; inspect the build output above.');
    try { if ((await fetch(`http://127.0.0.1:${apiPort}/_qa/health`)).ok) break; }
    catch { /* The isolated fixture is still building. */ }
    if (Date.now() >= deadline) throw new Error('Notebook QA API startup timed out.');
    await new Promise(accept => setTimeout(accept, 200));
  }
  collaboration = createCollaborationServer({
    apiBaseUrl: `http://127.0.0.1:${apiPort}`, serviceSecret: serviceKey,
    port: collaborationPort, address: '127.0.0.1',
    allowedOrigins: [`http://127.0.0.1:${port}`, `http://localhost:${port}`],
  });
  await collaboration.listen();
  const identities = await (await fetch(`http://127.0.0.1:${apiPort}/_qa/identities`)).json();
  const types = { '.html': 'text/html;charset=utf-8', '.js': 'text/javascript;charset=utf-8', '.css': 'text/css;charset=utf-8',
    '.json': 'application/json', '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg',
    '.webp': 'image/webp', '.woff': 'font/woff', '.woff2': 'font/woff2', '.ico': 'image/x-icon' };
  server = http.createServer(async (request, response) => {
    const url = new URL(request.url, `http://127.0.0.1:${port}`);
    response.setHeader('Cache-Control', 'no-store');
    response.setHeader('Blazor-Environment', 'Production');
    if (url.pathname === '/_qa/login') {
      const identity = identities.find(item => item.key === url.searchParams.get('user'));
      if (!identity) { response.writeHead(400).end('Choose pupil, peer, outsider or teacher.'); return; }
      response.setHeader('Content-Type', 'text/html;charset=utf-8');
      response.end(`<!doctype html><html lang="da"><title>Fiktivt QA-login</title><body><p>Åbner ${identity.name}…</p><script>
        sessionStorage.setItem('mt_access_token',${JSON.stringify(identity.token)});
        sessionStorage.setItem('mt_refresh_token','');
        sessionStorage.setItem('mt_expires_at',${JSON.stringify(String(identity.expiresAt))});
        Promise.all([navigator.serviceWorker?.getRegistrations().then(items=>Promise.all(items.map(item=>item.unregister()))),
          globalThis.caches?.keys().then(keys=>Promise.all(keys.map(key=>caches.delete(key))))]).then(()=>location.replace(${JSON.stringify(identity.home)}));
      </script></body></html>`);
      return;
    }
    if (url.pathname === '/_qa') {
      response.setHeader('Content-Type', 'text/html;charset=utf-8');
      response.end('<!doctype html><html lang="da"><h1>Notesbog: isoleret browser-QA</h1><p>Rigtige Blazor-komponenter og API-controllere, fiktive personer og data i hukommelsen.</p>'
        + identities.map(item => `<p><a href="/_qa/login?user=${item.key}">${item.name}</a></p>`).join('') + '</html>');
      return;
    }
    if (url.pathname.startsWith('/api/internal/') || url.pathname.startsWith('/internal/') || url.pathname.startsWith('/_qa/')) {
      response.writeHead(404).end(); return;
    }
    if (url.pathname.startsWith('/api/')) {
      const proxy = http.request({ hostname: '127.0.0.1', port: apiPort, method: request.method, path: request.url,
        headers: { ...request.headers, host: `127.0.0.1:${apiPort}` } }, upstream => {
        response.writeHead(upstream.statusCode, upstream.headers); upstream.pipe(response);
      });
      proxy.on('error', () => { if (!response.headersSent) response.writeHead(502); response.end(); });
      request.pipe(proxy); return;
    }
    try {
      let path = resolve(webRoot, '.' + decodeURIComponent(url.pathname));
      if (path !== webRoot && !path.startsWith(webRoot + sep)) { response.writeHead(404).end(); return; }
      let info;
      try { info = await stat(path); } catch { /* Route fallback below. */ }
      if (!info?.isFile()) {
        if (extname(url.pathname) && url.pathname !== '/') { response.writeHead(404).end(); return; }
        path = resolve(webRoot, 'index.html');
      }
      response.setHeader('Content-Type', types[extname(path)] ?? 'application/octet-stream');
      response.end(await readFile(path));
    } catch { response.writeHead(404).end(); }
  });
  server.on('upgrade', (request, socket, head) => {
    if (new URL(request.url, 'http://localhost').pathname !== '/collaboration') { socket.destroy(); return; }
    const upstream = net.connect(collaborationPort, '127.0.0.1', () => {
      upstream.write(`${request.method} ${request.url} HTTP/${request.httpVersion}\r\n`
        + request.rawHeaders.reduce((text, value, index) => text + (index % 2 === 0 ? value + ': ' : value + '\r\n'), '') + '\r\n');
      if (head.length) upstream.write(head);
      socket.pipe(upstream).pipe(socket);
    });
    upstream.on('error', () => socket.destroy()); socket.on('error', () => upstream.destroy());
  });
  await new Promise(accept => server.listen(port, '127.0.0.1', accept));
  console.log(`Notebook QA ready: http://localhost:${port}/_qa (pupil, peer, teacher; fictional memory-only data).`);
} catch (error) { await shutdown(); throw error; }
