// Trusted Ctrl+C/X/V and the real browser clipboard. A Windows STA guard preserves OS clipboard formats in RAM.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import * as Y from '../../collaboration/node_modules/yjs/dist/yjs.mjs';
import { projectNotebookDocument } from '../../collaboration/src/notebook-documents.mjs';

const apiOrigin = 'http://127.0.0.1:8088', origin = 'http://localhost:9098';
assert.equal((await (await fetch(apiOrigin + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const identities = await (await fetch(apiOrigin + '/_qa/identities')).json(), identity = identities.find(item => item.key === 'teacher');
const output = resolve('.tools/drawing-clipboard-qa'); await mkdir(output, { recursive: true });
const runId = 'qa-clipboard-' + crypto.randomUUID();
const marker = `QA parent note ${runId} 😀 – skal blive i notesbogen.`, oldError = 'QA gammel AI-tekst: Jeg kan desværre ikke indsætte jeres custom emoji her.';
const fieldText = 'QA tekst inde i canvas ' + runId;
const results = [], clipboard = [], states = [], errors = [];
let browser, guard, guardClosed, page, drawingId, noteId;
const until = async (check, description, timeout = 25000) => {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { const value = await check(); if (value) return value; await new Promise(accept => setTimeout(accept, 100)); }
  throw new Error('Timed out: ' + description);
};
async function preserveClipboard() {
  assert.equal(process.platform, 'win32', 'Use a platform clipboard guard before running native clipboard acceptance');
  const code = String.raw`
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$original = [System.Windows.Forms.Clipboard]::GetDataObject()
$snapshot = New-Object System.Windows.Forms.DataObject
$formats = @()
try {
  if ($null -ne $original) {
    foreach ($format in $original.GetFormats($false)) {
      $value = $original.GetData($format, $false)
      if ($null -eq $value) { continue }
      if ($value -is [System.IO.Stream]) {
        $copy = New-Object System.IO.MemoryStream
        if ($value.CanSeek) { $value.Position = 0 }
        $value.CopyTo($copy); $copy.Position = 0; $value = $copy
      } elseif ($value -is [System.Drawing.Image]) { $value = $value.Clone() }
      elseif ($value -is [System.Collections.Specialized.StringCollection]) { $copy = New-Object System.Collections.Specialized.StringCollection; foreach ($item in $value) { [void]$copy.Add($item) }; $value = $copy }
      elseif ($value -is [System.ICloneable]) { $value = $value.Clone() }
      elseif ($value -isnot [string] -and -not $value.GetType().IsValueType) { throw 'Unsupported clipboard format: preservation aborted before any write.' }
      $snapshot.SetData($format, $false, $value); $formats += $format
    }
  }
} catch { [Console]::WriteLine('GUARD_ERROR'); exit 2 }
[Console]::WriteLine('GUARD_READY'); [Console]::Out.Flush()
$sentinels = New-Object System.Collections.Generic.List[string]
try { while ($null -ne ($command = [Console]::ReadLine())) { if ($command.StartsWith('sentinel:')) { $sentinels.Add($command.Substring(9)) } else { break } } }
finally {
  $ours = $false
  try {
    $current = [System.Windows.Forms.Clipboard]::GetDataObject()
    if ($null -ne $current) { foreach ($format in $current.GetFormats($false)) { $value = $current.GetData($format, $false); if ($value -is [string]) { foreach ($sentinel in $sentinels) { if ($sentinel.Length -gt 10 -and $value.Contains($sentinel)) { $ours = $true } } } } }
  } catch { $ours = $false }
  if (-not $ours) { [Console]::WriteLine('GUARD_SKIPPED_UNCHANGED_OR_USER_COPY'); exit 0 }
  for ($attempt = 0; $attempt -lt 10; $attempt++) {
    try {
      if ($formats.Count -eq 0) { [System.Windows.Forms.Clipboard]::Clear() } else { [System.Windows.Forms.Clipboard]::SetDataObject($snapshot, $true) }
      [Console]::WriteLine('GUARD_RESTORED'); break
    } catch { if ($attempt -eq 9) { [Console]::WriteLine('GUARD_RESTORE_FAILED'); exit 3 }; Start-Sleep -Milliseconds 100 }
  }
}
`;
  guard = spawn('powershell.exe', ['-NoProfile', '-Sta', '-EncodedCommand', Buffer.from(code, 'utf16le').toString('base64')], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  let log = '';
  guard.stdout.on('data', chunk => { log += chunk.toString(); });
  guardClosed = new Promise(accept => guard.once('exit', code => accept(code)));
  await until(() => { if (log.includes('GUARD_ERROR')) throw new Error('OS clipboard cannot be preserved without losing a format'); return log.includes('GUARD_READY'); }, 'clipboard preservation guard');
  guard.stdin.write('sentinel:' + runId + '\n');
  return async () => {
    guard.stdin.end('restore\n'); const code = await guardClosed;
    assert.ok(code === 0 && (log.includes('GUARD_RESTORED') || log.includes('GUARD_SKIPPED')), 'OS clipboard preserved or conditionally restored');
    process.stdout.write('PASS OS clipboard ' + (log.includes('GUARD_RESTORED') ? 'conditionally restored' : 'untouched or user-copy preserved') + '; no original values logged or written to disk\n');
  };
}
async function api(path, body) {
  const response = await fetch(apiOrigin + path, { method: body ? 'POST' : 'GET', headers: { Authorization: 'Bearer ' + identity.token, ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  assert.ok(response.ok, `${path}: ${response.status}`); return response.json();
}
async function parent() {
  const context = await api(`/api/notebooks/pages/${noteId}/context`), doc = new Y.Doc();
  try { Y.applyUpdate(doc, Buffer.from(context.stateBase64, 'base64')); const projection = projectNotebookDocument(doc); return { revision: context.revision, fingerprint: context.fingerprint, plainText: projection.plainText, drawingIds: projection.drawingIds }; }
  finally { doc.destroy(); }
}
const dialog = () => page.locator('dialog.notion-drawing-dialog[open]');
async function scene() { const context = await api(`/api/drawings/${drawingId}/context`); return { revision: context.revision, elements: JSON.parse(context.sceneJson).elements.filter(element => !element.isDeleted) }; }
async function summary(label) {
  const value = await page.evaluate(async () => {
    const items = await navigator.clipboard.read(), types = [...new Set(items.flatMap(item => item.types))];
    let text = '', html = '';
    for (const item of items) { if (item.types.includes('text/plain')) text = await (await item.getType('text/plain')).text(); if (item.types.includes('text/html')) html = await (await item.getType('text/html')).text(); }
    let data; try { data = JSON.parse(text); } catch { }
    return { types, textBytes: text.length, htmlBytes: html.length, hasParentMarkerInHtml: html.includes('QA parent note'), hasParentMarkerInText: text.includes('QA parent note'), payloadType: data?.type, elementIds: data?.elements?.map(item => item.id), elementTypes: data?.elements?.map(item => item.type), fileIds: data?.files ? Object.keys(data.files) : [] };
  }); for (const id of value.elementIds || []) guard.stdin.write('sentinel:' + id + '\n'); clipboard.push({ label, ...value }); return value;
}
async function step(name, work) {
  try { await work(); results.push({ name, passed: true }); console.log('PASS', name); }
  catch (error) { results.push({ name, passed: false, message: error.message }); throw error; }
}
let restore;
try {
  restore = await preserveClipboard();
  const { chromium } = await import(pathToFileURL(resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ permissions: ['clipboard-read', 'clipboard-write'], viewport: { width: 1400, height: 950 }, serviceWorkers: 'block' });
  await context.route('**/*', route => { const url = new URL(route.request().url()); return ['localhost', '127.0.0.1'].includes(url.hostname) || ['blob:', 'data:'].includes(url.protocol) ? route.continue() : route.abort(); });
  page = await context.newPage(); page.on('pageerror', error => errors.push(error.message)); page.on('dialog', value => { void value.accept(); });
  await page.goto(origin + '/_qa/login?user=teacher'); await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  const notebook = await api('/api/notebooks/mine', {}), note = await api(`/api/notebooks/${notebook.id}/pages`, { title: 'QA native clipboard isolation' }); noteId = note.id;
  await page.goto(origin + '/notebook/' + noteId); const editor = page.locator('.bn-editor[contenteditable=true]').first(); await editor.waitFor();
  await editor.click(); await page.keyboard.insertText(marker); await page.keyboard.press('Enter'); await page.keyboard.insertText(oldError); await page.keyboard.press('Enter');
  const creating = page.waitForResponse(response => new URL(response.url()).pathname === '/api/drawings' && response.request().method() === 'POST');
  await page.keyboard.type('/tegning'); await page.locator('.bn-suggestion-menu').getByText('Tegning', { exact: true }).click(); drawingId = (await (await creating).json()).id;
  await dialog().getByTestId('toolbar-ellipse').waitFor(); await dialog().getByTestId('toolbar-ellipse').locator('..').click();
  const bounds = await dialog().locator('.notion-drawing-canvas').boundingBox();
  await page.mouse.move(bounds.x + bounds.width * .52, bounds.y + bounds.height * .4); await page.mouse.down();
  await page.mouse.move(bounds.x + bounds.width * .52 + 120, bounds.y + bounds.height * .4 + 85, { steps: 10 }); await page.mouse.up(); await page.keyboard.press('Escape');
  await until(async () => (await scene()).elements.length === 1, 'initial ellipse persisted');
  await until(async () => (await parent()).plainText.includes(marker), 'parent note marker persisted');
  await dialog().getByRole('button', { name: 'Luk tegning' }).click(); await dialog().waitFor({ state: 'hidden' });
  await editor.getByText(marker, { exact: true }).click(); await page.keyboard.press('Control+a');
  await page.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await dialog().getByTestId('toolbar-ellipse').waitFor();
  const before = await parent(); states.push({ label: 'before clipboard', parent: before, sceneIds: (await scene()).elements.map(item => item.id) });
  await step('Native ellipse Ctrl+C/V creates a second ellipse and leaves parent note unchanged', async () => {
    await page.keyboard.press('Control+a'); await page.keyboard.press('Control+c'); await new Promise(accept => setTimeout(accept, 250));
    const data = await summary('ellipse copy');
    await page.mouse.move(bounds.x + bounds.width * .7, bounds.y + bounds.height * .6); await page.keyboard.press('Control+v');
    await new Promise(accept => setTimeout(accept, 1500));
    const after = await parent(), drawing = await scene(); states.push({ label: 'after native paste', parent: after, scene: drawing.elements.map(item => ({ id: item.id, type: item.type, text: item.text })) });
    assert.equal(data.payloadType, 'excalidraw/clipboard'); assert.equal(data.hasParentMarkerInHtml, false, 'copy must not contain background note HTML');
    assert.equal(after.fingerprint, before.fingerprint, 'native drawing paste must not replace the parent Y.Doc selection'); assert.equal(after.plainText, before.plainText);
    assert.equal(drawing.elements.length, 2); assert.ok(drawing.elements.every(item => item.type === 'ellipse'));
  });
  await step('Native Ctrl+X/V cuts and restores the selected drawing shapes without cutting the note', async () => {
    await page.keyboard.press('Control+a'); await page.keyboard.press('Control+x'); await summary('ellipse cut');
    await until(async () => (await scene()).elements.length === 0, 'native shapes cut');
    assert.equal((await parent()).plainText, before.plainText);
    await page.keyboard.press('Control+v'); await until(async () => (await scene()).elements.length === 2, 'native shapes pasted after cut');
    assert.equal((await parent()).fingerprint, before.fingerprint);
  });
  await step('Text textarea copy/paste remains native and does not paste into the note', async () => {
    await page.keyboard.press('Escape'); await dialog().getByTestId('toolbar-text').locator('..').click();
    await page.mouse.click(bounds.x + bounds.width * .48, bounds.y + bounds.height * .75); await page.keyboard.insertText(fieldText);
    await page.keyboard.press('Control+a'); await page.keyboard.press('Control+c'); await summary('native text textarea copy');
    await page.keyboard.press('End'); await page.keyboard.press('Control+v'); await page.keyboard.press('Escape');
    await until(async () => (await scene()).elements.some(item => item.type === 'text' && item.text === fieldText + fieldText), 'native textarea text pasted');
    assert.equal((await parent()).fingerprint, before.fingerprint);
  });
  await step('Native platform emoji image copy/paste carries files and leaves the note unchanged', async () => {
    await page.keyboard.press('Escape'); await dialog().getByRole('button', { name: 'Emojis', exact: true }).click();
    const picker = dialog().locator('.notion-drawing-emoji-picker');
    await picker.getByRole('searchbox', { name: 'Søg efter emoji' }).fill('qa-1f680');
    await picker.getByRole('button', { name: ':qa-1f680:', exact: true }).click();
    await until(async () => (await scene()).elements.some(item => item.type === 'image'), 'platform emoji image stored');
    await page.keyboard.press('v'); await page.mouse.click(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
    await page.keyboard.press('Control+c'); await new Promise(accept => setTimeout(accept, 200));
    const data = await summary('native platform emoji image copy');
    assert.equal(data.payloadType, 'excalidraw/clipboard'); assert.deepEqual(data.elementTypes, ['image']); assert.ok(data.fileIds.length > 0);
    await page.mouse.move(bounds.x + bounds.width * .85, bounds.y + bounds.height * .72); await page.keyboard.press('Control+v');
    await until(async () => (await scene()).elements.filter(item => item.type === 'image').length === 2, 'native emoji image pasted with files');
    assert.equal((await parent()).fingerprint, before.fingerprint);
  });
  assert.deepEqual(errors, []);
} catch (error) {
  process.exitCode = 1; console.error(error.stack); if (page) await page.screenshot({ path: resolve(output, 'failure.png'), fullPage: true }).catch(() => {});
} finally {
  await browser?.close();
  if (restore) await restore(); else if (guard) { guard.stdin.end(); await guardClosed; }
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ results, clipboard, states, errors }, null, 2));
}
