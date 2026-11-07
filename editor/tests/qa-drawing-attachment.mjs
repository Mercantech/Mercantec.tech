// Provided attachment, real native paste, isolated loopback fixtures; never print image/clipboard values.
import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import * as Y from '../../collaboration/node_modules/yjs/dist/yjs.mjs';
import { fingerprintHash } from '../../collaboration/src/fingerprint.mjs';
import { projectNotebookDocument } from '../../collaboration/src/notebook-documents.mjs';
import { preserveClipboard } from './qa-clipboard-preservation.mjs';

const usage = `Usage: node editor/tests/qa-drawing-attachment.mjs <clipboard-json-file>
Or set DRAWING_ATTACHMENT to the clipboard JSON file path.
Optional: PLAYWRIGHT_MODULE points to Playwright's index.mjs.
Reports are written to .tools/drawing-attachment-qa/report.json without clipboard values.`;
const attachmentPath = process.argv[2] || process.env.DRAWING_ATTACHMENT;
if (['--help', '-h'].includes(process.argv[2])) { console.log(usage); process.exit(0); }
if (!attachmentPath || process.argv.length > 3) { console.error(usage); process.exit(2); }

const apiOrigin = 'http://127.0.0.1:8088', origin = 'http://localhost:9098';
assert.equal((await (await fetch(apiOrigin + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const identity = (await (await fetch(apiOrigin + '/_qa/identities')).json()).find(item => item.key === 'teacher');
const raw = await readFile(attachmentPath, 'utf8');
const payload = JSON.parse(raw), sourceFile = Object.values(payload.files)[0];
assert.equal(payload.type, 'excalidraw/clipboard'); assert.equal(sourceFile.mimeType, 'image/png');
const png = sourceFile.dataURL.split(',')[1], runId = 'qa-attachment-' + crypto.randomUUID();
const output = resolve('.tools/drawing-attachment-qa'); await mkdir(output, { recursive: true });
const results = [], errors = [];
let browser, restore;
const until = async (check, timeout = 25000) => { const end = Date.now() + timeout; while (Date.now() < end) { const value = await check(); if (value) return value; await new Promise(accept => setTimeout(accept, 150)); } return false; };
async function api(path, body, internal = false) {
  const response = await fetch(apiOrigin + path, { method: body ? 'POST' : 'GET', headers: internal ? { 'X-Notion-Service-Key': 'qa-notebooks-only-service-key-32-characters' } : { Authorization: 'Bearer ' + identity.token, ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  assert.ok(response.ok, `${path}: ${response.status}`); return internal ? new Uint8Array(await response.arrayBuffer()) : response.json();
}
async function parent(domain, id, marker) {
  const state = await api(`/api/internal/${domain === 'notion' ? 'notion' : 'notebooks'}/documents/${id}/state`, null, true);
  const doc = new Y.Doc();
  try { Y.applyUpdate(doc, state); const projection = projectNotebookDocument(doc); return { fingerprint: fingerprintHash(state, Y.encodeStateVector(doc)), textHash: createHash('sha256').update(projection.plainText).digest('hex'), textBytes: projection.plainText.length, containsMarker: projection.plainText.includes(marker), containsClipboardJson: projection.plainText.includes('excalidraw/clipboard'), containsImageData: projection.plainText.includes('data:image/'), drawingIds: projection.drawingIds }; }
  finally { doc.destroy(); }
}
const dialog = page => page.locator('dialog.notion-drawing-dialog[open]');
async function frame(page) {
  return page.evaluate(() => {
    const d = document.querySelector('dialog.notion-drawing-dialog[open]'), a = document.activeElement;
    const resources = performance.getEntriesByType('resource').map(item => new URL(item.name));
    const entry = resources.findLast(url => url.pathname === '/notion-editor/editor.js');
    const stylesheet = [...document.querySelectorAll('link[rel="stylesheet"]')].map(link => new URL(link.href)).findLast(url => url.pathname === '/notion-editor/editor.css');
    return { dialogOpen: !!d, dialogInsideParentEditor: !!d?.closest('.bn-editor'), activeTag: a?.tagName, activeInParentEditor: !!a?.closest('.bn-editor'), activeInCanvas: !!a?.closest('.excalidraw'), entryUrl: entry ? entry.pathname + entry.search : null, stylesheetUrl: stylesheet ? stylesheet.pathname + stylesheet.search : null, runtimeChunks: resources.map(url => url.pathname).filter(path => /\/notion-editor\/editor[^/]*\.js$/.test(path)), serviceWorkerControlled: !!navigator.serviceWorker?.controller, pasteEvents: window.__qaPasteEvents || [] };
  });
}
async function run(domain, type, focus) {
  const required = focus === 'canvas';
  const result = { domain, clipboard: type, focus, required, passed: false };
  const context = await browser.newContext({ permissions: ['clipboard-read', 'clipboard-write'], viewport: { width: 1400, height: 950 }, serviceWorkers: 'block' });
  try {
    await context.addInitScript(() => {
      window.__qaPasteEvents = [];
      window.addEventListener('paste', event => window.__qaPasteEvents.push({ trusted: event.isTrusted, tag: event.target?.tagName, insideParentEditor: !!event.target?.closest?.('.bn-editor'), insideCanvas: !!event.target?.closest?.('.excalidraw'), mimeTypes: [...(event.clipboardData?.types || [])], plainTextBytes: event.clipboardData?.getData('text/plain').length || 0 }), true);
    });
    await context.route('**/*', route => { const url = new URL(route.request().url()); return ['localhost', '127.0.0.1'].includes(url.hostname) || ['blob:', 'data:'].includes(url.protocol) ? route.continue() : route.abort(); });
    const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message)); page.on('dialog', value => { void value.accept(); });
    await page.goto(origin + '/_qa/login?user=teacher'); await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
    let record;
    if (domain === 'notion') { const [hold] = await api('/api/notion/holds'); record = await api('/api/notion/pages', { holdId: hold.id, title: 'QA exact clipboard image ' + type }); await page.goto(`${origin}/notion/${record.id}?hold=${hold.id}`); }
    else { const notebook = await api('/api/notebooks/mine', {}); record = await api(`/api/notebooks/${notebook.id}/pages`, { title: 'QA exact clipboard image ' + type }); await page.goto(`${origin}/notebook/${record.id}`); }
    const marker = `${runId} ${domain} ${type} ${focus} 😀`;
    const editor = page.locator('.bn-editor[contenteditable=true]').first(); await editor.waitFor(); await editor.click(); await page.keyboard.insertText(marker); await page.keyboard.press('Enter');
    const creating = page.waitForResponse(response => new URL(response.url()).pathname === '/api/drawings' && response.request().method() === 'POST');
    await page.keyboard.type('/tegning'); await page.locator('.bn-suggestion-menu').getByText('Tegning', { exact: true }).click(); const drawingId = (await (await creating).json()).id; result.drawingId = drawingId;
    await dialog(page).getByTestId('toolbar-image').waitFor();
    await dialog(page).getByRole('button', { name: 'Luk tegning' }).click(); await dialog(page).waitFor({ state: 'hidden' });
    await until(async () => { try { return (await parent(domain, record.id, marker)).drawingIds.includes(drawingId); } catch { return false; } });
    await editor.getByText(marker, { exact: true }).click(); await page.keyboard.press('Control+a');
    await page.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await dialog(page).getByTestId('toolbar-image').waitFor();
    if (focus === 'canvas') { const area = await dialog(page).locator('.notion-drawing-canvas').boundingBox(); await page.mouse.click(area.x + area.width * .6, area.y + area.height * .55); }
    result.beforeFrame = await frame(page); result.beforeParent = await parent(domain, record.id, marker);
    await page.evaluate(async ({ raw, png, type }) => {
      if (type === 'json') await navigator.clipboard.writeText(raw);
      else { const bytes = Uint8Array.from(atob(png), character => character.charCodeAt(0)); await navigator.clipboard.write([new ClipboardItem({ 'image/png': new Blob([bytes], { type: 'image/png' }) })]); }
    }, { raw, png, type });
    result.nativeMimeTypes = await page.evaluate(async () => [...new Set((await navigator.clipboard.read()).flatMap(item => item.types))]);
    await page.keyboard.press('Control+v');
    await until(async () => {
      const state = await api(`/api/drawings/${drawingId}/context`), after = await parent(domain, record.id, marker);
      return JSON.parse(state.sceneJson).elements.some(element => !element.isDeleted && element.type === 'image') || after.fingerprint !== result.beforeParent.fingerprint;
    }, 7000);
    const stored = await api(`/api/drawings/${drawingId}/context`);
    result.afterParent = await parent(domain, record.id, marker); result.afterFrame = await frame(page);
    result.scene = JSON.parse(stored.sceneJson).elements.filter(element => !element.isDeleted).map(element => ({ id: element.id, type: element.type, fileId: element.fileId, x: element.x, y: element.y, width: element.width, height: element.height }));
    assert.equal(result.afterParent.fingerprint, result.beforeParent.fingerprint, 'native image paste must not alter the selected parent note');
    assert.equal(result.afterParent.containsMarker, true); assert.equal(result.afterParent.containsClipboardJson, false); assert.equal(result.afterParent.containsImageData, false);
    assert.equal(result.beforeFrame.dialogInsideParentEditor, false);
    assert.equal(result.afterFrame.dialogOpen, true, 'native image paste must keep the drawing dialog open');
    assert.equal(result.afterFrame.dialogInsideParentEditor, false, 'drawing dialog must stay outside the parent editor');
    for (const publicFrame of [result.beforeFrame, result.afterFrame]) {
      const version = /^\/notion-editor\/editor\.js\?v=([a-f0-9]{64}-[a-f0-9]{64})$/i.exec(publicFrame.entryUrl)?.[1];
      assert.ok(version, 'published Blazor must import the editor using the two asset hashes');
      assert.equal(publicFrame.stylesheetUrl, `/notion-editor/editor.css?v=${version}`, 'published editor JS and CSS must use the same asset version');
    }
    result.imagePasted = result.scene.some(element => element.type === 'image');
    // The pinned SDK only handles paste while its last pointer position is over the canvas.
    // Opening the dialog and immediately pasting is retained as a parent-isolation diagnostic.
    if (required) assert.ok(result.imagePasted, 'image should be pasted into the canvas');
    else result.diagnostic = result.imagePasted ? 'image-pasted' : 'SDK-pointer-gate-no-op';
    result.passed = true;
  } catch (error) { result.error = error.message; process.exitCode = 1; }
  finally { results.push(result); console.log(JSON.stringify({ domain, type, focus, required, passed: result.passed, diagnostic: result.diagnostic, parentChanged: result.afterParent && result.afterParent.fingerprint !== result.beforeParent?.fingerprint, imageCount: result.scene?.filter(item => item.type === 'image').length, dialogInsideParentEditor: result.beforeFrame?.dialogInsideParentEditor, error: result.error })); await context.close(); }
}
try {
  restore = await preserveClipboard(runId, async (check) => { const value = await until(check); if (!value) throw new Error('Clipboard preservation guard unavailable'); return value; });
  for (const element of payload.elements) restore.sentinel(element.id); restore.png(png);
  const modulePath = process.env.PLAYWRIGHT_MODULE || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs');
  const { chromium } = await import(pathToFileURL(modulePath).href); browser = await chromium.launch({ headless: true });
  for (const domain of ['notion', 'notebook']) { await run(domain, 'json', 'direct-open'); await run(domain, 'json', 'canvas'); await run(domain, 'png', 'canvas'); }
  assert.deepEqual(errors, [], 'native attachment acceptance must not emit browser page errors');
} finally {
  await browser?.close(); if (restore) await restore(); await writeFile(resolve(output, 'report.json'), JSON.stringify({ results, errors }, null, 2));
}
