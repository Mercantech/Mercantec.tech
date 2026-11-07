// Real Blazor/.NET/SQLite/object-storage/Hocuspocus browser acceptance. No production hosts or SDK test hooks.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as Y from '../../collaboration/node_modules/yjs/dist/yjs.mjs';
import { projectNotebookDocument } from '../../collaboration/src/notebook-documents.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const base = new URL(process.env.DRAWING_QA_URL || 'http://localhost:9098');
assert.ok(['localhost', '127.0.0.1'].includes(base.hostname) && base.protocol === 'http:', 'QA must use an explicit loopback fixture');
const apiBase = 'http://127.0.0.1:8088';
const fixture = await (await fetch(apiBase + '/_qa/health')).json();
assert.equal(fixture.fixture, 'fictional-notebook-browser-qa');
const identities = Object.fromEntries((await (await fetch(apiBase + '/_qa/identities')).json()).map(item => [item.key, item]));
const output = resolve(root, process.env.DRAWING_QA_OUTPUT || '.tools/drawing-browser-qa');
await mkdir(output, { recursive: true });
const modulePath = process.env.PLAYWRIGHT_MODULE || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs');
const { chromium } = await import(pathToFileURL(modulePath).href);
const browser = await chromium.launch({ headless: process.env.DRAWING_QA_HEADED !== '1' });
const results = [], errors = [], consoleErrors = [], requests = [], responses = [], httpErrors = [], workers = [], clients = [];
const until = async (check, description, timeout = 45000) => {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const result = await check();
    if (result) return result;
    await new Promise(accept => setTimeout(accept, 150));
  }
  throw new Error('Timed out: ' + description);
};
async function api(identity, path, options = {}) {
  const response = await fetch(apiBase + path, { ...options, headers: { Authorization: 'Bearer ' + identities[identity].token, ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers } });
  const text = await response.text();
  assert.ok(response.ok, `${identity} ${options.method || 'GET'} ${path}: ${response.status} ${text.slice(0, 500)}`);
  return text ? JSON.parse(text) : null;
}
async function client(identity, viewport = { width: 1365, height: 950 }, mobile = false) {
  const context = await browser.newContext({ viewport, acceptDownloads: true, ...(mobile ? { isMobile: true, hasTouch: true } : {}) });
  // Playwright cannot drive OS File System Access dialogs; exercise the SDK's real portable file-input path.
  await context.addInitScript(() => { delete window.showOpenFilePicker; delete window.showSaveFilePicker; delete window.showDirectoryPicker; });
  // No outbound integrations, AI providers or real authentication are reachable in this fixture.
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    if (['data:', 'blob:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname)) return route.continue();
    return route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  page.on('worker', worker => workers.push({ identity, url: worker.url() }));
  page.on('request', request => { if (/font|excalidraw/i.test(request.url())) requests.push({ identity, url: request.url(), type: request.resourceType() }); });
  page.on('response', response => { if (/font|excalidraw/i.test(response.url())) responses.push({ identity, url: response.url(), status: response.status() }); });
  page.on('response', response => { if (response.status() >= 400) { const item = { identity, path: new URL(response.url()).pathname, method: response.request().method(), status: response.status() }; httpErrors.push(item); process.stdout.write('HTTP ' + JSON.stringify(item) + '\n'); } });
  page.on('pageerror', error => errors.push({ identity, message: error.message }));
  page.on('console', message => { if (message.type() === 'error') { consoleErrors.push({ identity, message: message.text() }); process.stdout.write('BROWSER ' + identity + ': ' + message.text() + '\n'); } });
  page.on('dialog', value => { void (value.type() === 'beforeunload' ? value.accept() : value.dismiss()); });
  await page.goto(new URL('/_qa/login?user=' + identity, base).href);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'), { timeout: 60000 });
  clients.push({ context, page, identity });
  return page;
}
const editor = page => page.locator('.notion-blocknote-host .bn-editor[contenteditable=true]').first();
const dialog = page => page.locator('dialog.notion-drawing-dialog[open]');
async function navigate(page, path, editable = true) {
  await page.goto(new URL(path, base).href);
  await page.locator('.notion-blocknote-host .bn-editor').first().waitFor({ timeout: 60000 });
  if (editable) await editor(page).waitFor();
}
async function insertDrawing(page) {
  const created = page.waitForResponse(response => new URL(response.url()).pathname === '/api/drawings' && response.request().method() === 'POST' && response.ok());
  await editor(page).click();
  await page.keyboard.press('Control+End');
  await page.keyboard.type('/tegning', { delay: 35 });
  await page.locator('.bn-suggestion-menu').getByText('Tegning', { exact: true }).click();
  const context = await (await created).json();
  await ready(page, true);
  return context;
}
async function ready(page, editable) {
  await dialog(page).waitFor({ timeout: 60000 });
  await until(async () => {
    const failure = dialog(page).locator('.notion-drawing-error[role=alert]');
    if (await failure.count()) throw new Error(await failure.first().innerText());
    if (!(await dialog(page).locator('.excalidraw canvas').first().isVisible())) return false;
    return !editable || dialog(page).getByTestId('toolbar-rectangle').isVisible();
  }, 'drawing canvas and correct editing mode', 60000);
}
async function shape(page, type, shift = 0) {
  await dialog(page).getByTestId('toolbar-' + type).locator('..').click();
  const canvas = await dialog(page).locator('.notion-drawing-canvas').boundingBox();
  assert.ok(canvas && canvas.width > 300 && canvas.height > 200);
  const x = canvas.x + canvas.width * .56 + shift, y = canvas.y + canvas.height * .45;
  await page.mouse.move(x, y); await page.mouse.down();
  await page.mouse.move(x + 110, y + 75, { steps: 12 }); await page.mouse.up();
  await page.keyboard.press('Escape');
}
async function uploadImage(page) {
  const png = Buffer.from(await page.evaluate(() => { const c = document.createElement('canvas'); c.width = c.height = 24; const ctx = c.getContext('2d'); ctx.fillStyle = '#a855f7'; ctx.fillRect(0, 0, 24, 24); return c.toDataURL('image/png').split(',')[1]; }), 'base64');
  const choosing = page.waitForEvent('filechooser');
  await dialog(page).getByTestId('toolbar-image').locator('..').click();
  await (await choosing).setFiles({ name: 'qa-upload.png', mimeType: 'image/png', buffer: png });
  const area = await dialog(page).locator('.notion-drawing-canvas').boundingBox();
  await page.mouse.click(area.x + area.width * .73, area.y + area.height * .65);
}
async function drawing(identity, id, published = false) {
  return api(identity, `/api/drawings/${id}/context${published ? '?published=true' : ''}`);
}
const scene = context => JSON.parse(context.sceneJson || '{"elements":[],"files":{}}');
const liveElements = context => scene(context).elements.filter(item => !item.isDeleted);
async function stored(identity, id, minimum = 1) {
  return until(async () => {
    const context = await drawing(identity, id);
    return context.revision > 0 && context.fingerprint && context.previewLightUrl && context.previewDarkUrl && liveElements(context).length >= minimum && context;
  }, 'drawing content and both previews durably stored');
}
async function close(page) {
  await dialog(page).getByRole('button', { name: 'Luk tegning' }).click();
  await dialog(page).waitFor({ state: 'hidden' });
}
async function publish(page) {
  const button = page.getByRole('button', { name: /^(Udgiv til holdet|Opdatér udgivelsen)$/ });
  await button.click();
  await until(async () => !(await button.isDisabled()) && await page.getByRole('link', { name: 'Se udgivet', exact: true }).count(), 'real document publication', 60000);
  assert.equal(await page.locator('.notion-error[role=alert]').count(), 0);
}
async function step(name, operation) {
  const started = Date.now();
  try { await operation(); results.push({ name, passed: true, milliseconds: Date.now() - started }); process.stdout.write('PASS ' + name + '\n'); }
  catch (error) {
    results.push({ name, passed: false, message: error.message });
    for (let index = 0; index < clients.length; index++) await clients[index].page.screenshot({ path: resolve(output, `failure-${index}.png`), fullPage: true }).catch(() => {});
    throw error;
  }
}

try {
  const teacher = await client('teacher'), pupil = await client('pupil'), peer = await client('peer');
  const [hold] = await api('teacher', '/api/notion/holds');
  assert.ok(hold?.id);
  let page, canvas;
  await step('Create, close and reopen the actual Excalidraw component', async () => {
    page = await api('teacher', '/api/notion/pages', { method: 'POST', body: JSON.stringify({ holdId: hold.id, title: 'QA: Live tegning ' + randomUUID().slice(0, 8) }) });
    await navigate(teacher, `/notion/${page.id}?hold=${hold.id}`);
    canvas = await insertDrawing(teacher);
    await until(async () => /rektangl/i.test(await dialog(teacher).getByTestId('toolbar-rectangle').getAttribute('aria-label')), 'native Danish SDK tool labels', 10000);
    await shape(teacher, 'rectangle');
    const saved = await stored('teacher', canvas.id);
    assert.equal(liveElements(saved)[0].type, 'rectangle');
    await close(teacher);
    const focused = await until(() => editor(teacher).evaluate(element => element.contains(document.activeElement)), 'editor focus restored', 2000).then(() => true, () => false);
    assert.ok(focused, 'closing a drawing restores the parent editor caret');
    await teacher.getByRole('button', { name: 'Åbn tegning', exact: true }).click();
    await ready(teacher, true);
    assert.equal(liveElements(await drawing('teacher', canvas.id)).length, 1);
  });
  await step('Standard and custom emoji stickers become stored drawing files', async () => {
    const before = liveElements(await drawing('teacher', canvas.id)).length;
    const catalog = await api('teacher', '/api/emojis');
    assert.ok(catalog.filter(item => item.isStandard).length >= 40);
    for (const name of ['qa-1f680', 'qa-custom-sticker']) {
      await dialog(teacher).getByRole('button', { name: 'Emojis', exact: true }).click();
      const picker = dialog(teacher).locator('.notion-drawing-emoji-picker');
      await picker.getByRole('searchbox', { name: 'Søg efter emoji' }).fill(name);
      await picker.getByRole('button', { name: ':' + name + ':', exact: true }).click();
      await picker.waitFor({ state: 'hidden' });
    }
    const saved = await stored('teacher', canvas.id, before + 2);
    assert.equal(liveElements(saved).filter(item => item.type === 'image').length, 2);
    assert.ok(Object.values(scene(saved).files).every(file => /^[0-9a-f-]{36}$/i.test(file.assetId) && Object.keys(file).length === 1));
    assert.ok(!Buffer.from(saved.stateBase64, 'base64').includes(Buffer.from('data:image')));
  });
  await step('Native text tool loads self-hosted Excalidraw fonts and survives reopening', async () => {
    const before = liveElements(await drawing('teacher', canvas.id)).length;
    await dialog(teacher).getByTestId('toolbar-text').locator('..').click();
    const area = await dialog(teacher).locator('.notion-drawing-canvas').boundingBox();
    await teacher.mouse.click(area.x + area.width * .48, area.y + area.height * .7);
    await teacher.keyboard.insertText('QA tegning – æøå ✓'); await teacher.keyboard.press('Escape');
    const saved = await stored('teacher', canvas.id, before + 1);
    assert.ok(liveElements(saved).some(element => element.type === 'text' && element.text.includes('æøå')));
    await until(() => responses.some(item => new URL(item.url).pathname.startsWith('/notion-editor/excalidraw/fonts/') && item.status === 200), 'self-hosted font200');
    assert.ok(!requests.some(item => new URL(item.url).hostname.endsWith('excalidraw.com')), 'no Excalidraw CDN request');
    assert.ok(!requests.some(item => item.url.startsWith('file:')), 'no local-file font URL');
    assert.ok(!responses.some(item => /\/notion-editor\/excalidraw\//.test(item.url) && item.status >= 400), 'all SDK font/assets load successfully');
    await close(teacher); await teacher.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher, true);
  });
  await step('Native image upload and PNG/SVG/editable Excalidraw exports', async () => {
    const before = liveElements(await drawing('teacher', canvas.id)).length;
    await uploadImage(teacher); await stored('teacher', canvas.id, before + 1);
    const exports = dialog(teacher).locator('.notion-drawing-actions details');
    for (const [label, file] of [['PNG', 'tegning.png'], ['SVG', 'tegning.svg'], ['Excalidraw-fil', 'tegning.excalidraw']]) {
      if (!(await exports.evaluate(element => element.open))) await exports.locator('summary').click();
      const downloading = teacher.waitForEvent('download');
      await dialog(teacher).getByRole('button', { name: label, exact: true }).click();
      const download = await downloading; assert.equal(download.suggestedFilename(), file);
      await download.saveAs(resolve(output, file));
    }
    if (await exports.evaluate(element => element.open)) await exports.locator('summary').click();
    const pngBytes = await readFile(resolve(output, 'tegning.png'));
    assert.deepEqual([...pngBytes.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
    assert.match(await readFile(resolve(output, 'tegning.svg'), 'utf8'), /<svg/);
    const editable = JSON.parse(await readFile(resolve(output, 'tegning.excalidraw'), 'utf8'));
    assert.equal(editable.type, 'excalidraw'); assert.ok(editable.elements.length >= 4); assert.ok(Object.keys(editable.files).length >= 3);
  });
  await step('Drawing-only pupil grant leaves the parent document read-only; live peers converge', async () => {
    await dialog(teacher).getByRole('button', { name: 'Adgang', exact: true }).click();
    await dialog(teacher).locator('.notion-drawing-settings select').selectOption('live');
    await until(async () => (await drawing('teacher', canvas.id)).mode === 'live' && await dialog(teacher).locator('.notion-drawing-settings select').isEnabled(), 'live mode settings saved and refreshed');
    await close(teacher); await publish(teacher);
    await teacher.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher, true);
    await dialog(teacher).getByRole('button', { name: 'Adgang', exact: true }).click();
    await dialog(teacher).getByRole('searchbox', { name: 'Giv skriveadgang til tegningen' }).fill('Anna');
    await dialog(teacher).getByRole('button', { name: 'QA Elev Anna · Giv adgang', exact: true }).click();
    await until(async () => (await api('teacher', `/api/drawings/${canvas.id}/grants`)).some(grant => grant.userSub === identities.pupil.sub), 'explicit drawing grant saved');
    await close(teacher);
    const published = await api('pupil', `/api/notion/pages/${page.id}/published-context`);
    await navigate(pupil, `/notion/${page.id}?published=true&hold=${hold.id}`, false);
    await navigate(peer, `/notion/${page.id}?published=true&hold=${hold.id}`, false);
    assert.equal(await pupil.locator('.bn-editor[contenteditable=true]').count(), 0);
    const parentAuth = await fetch(apiBase + '/api/notion/collaboration/authorize', { method: 'POST', headers: { Authorization: 'Bearer ' + identities.pupil.token, 'Content-Type': 'application/json' }, body: JSON.stringify({ documentName: 'notion:' + page.id }) });
    assert.ok([403, 404].includes(parentAuth.status));
    await pupil.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(pupil, true);
    await peer.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(peer, false);
    assert.equal((await drawing('peer', canvas.id, true)).canEditDrawing, false);
    const before = liveElements(await drawing('teacher', canvas.id)).length;
    await shape(pupil, 'ellipse', 30); await stored('teacher', canvas.id, before + 1);
    const a = scene(await drawing('pupil', canvas.id, true)), b = scene(await drawing('peer', canvas.id, true));
    assert.deepEqual(a, b);
    assert.equal((await api('pupil', `/api/notion/pages/${page.id}/published-context`)).publishedStateBase64, published.publishedStateBase64);
    await close(peer); await close(pupil);
  });
  await step('Revoking a drawing grant removes writes on the existing live session', async () => {
    await pupil.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(pupil, true);
    await teacher.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher, true);
    await dialog(teacher).getByRole('button', { name: 'Adgang', exact: true }).click();
    const row = dialog(teacher).locator('.notion-drawing-grant').filter({ hasText: 'QA Elev Anna' });
    await row.getByRole('button', { name: 'Fjern adgang' }).click();
    await until(async () => (await drawing('pupil', canvas.id, true)).canEditDrawing === false, 'drawing grant revoked at real authorization API');
    await until(async () => !(await dialog(pupil).getByTestId('toolbar-rectangle').count()), 'existing drawing editor becomes read-only');
    await close(pupil); await close(teacher);
  });
  await step('All seven appearance variants and 390/320px dialogs remain bounded', async () => {
    const variants = [{ theme: 'system', scheme: 'light' }, { theme: 'system', scheme: 'dark' }, ...['light', 'dark', 'christmas', 'newyear', 'hacker'].map(theme => ({ theme, scheme: 'light' }))];
    for (const variant of variants) {
      await teacher.emulateMedia({ colorScheme: variant.scheme });
      await teacher.evaluate(theme => window.lupTheme.set(theme), variant.theme);
      await teacher.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher, true);
      const bounds = await dialog(teacher).boundingBox(), viewport = teacher.viewportSize();
      assert.ok(bounds.x >= -1 && bounds.y >= -1 && bounds.x + bounds.width <= viewport.width + 1 && bounds.y + bounds.height <= viewport.height + 1);
      await teacher.screenshot({ path: resolve(output, `theme-${variant.theme}-${variant.scheme}.png`) }); await close(teacher);
    }
    const mobile = await client('teacher', { width: 390, height: 844 }, true);
    await navigate(mobile, `/notion/${page.id}?hold=${hold.id}`);
    for (const width of [390, 320]) {
      await mobile.setViewportSize({ width, height: 844 });
      await mobile.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(mobile, true);
      const bounds = await dialog(mobile).boundingBox(); assert.ok(bounds.width <= width + 1 && bounds.x >= -1);
      const buttons = await dialog(mobile).locator('.notion-drawing-actions > button').evaluateAll(items => items.map(item => ({ width: item.getBoundingClientRect().width, height: item.getBoundingClientRect().height })));
      assert.ok(buttons.every(item => item.width >= 43.5 && item.height >= 43.5), `touch actions at ${width}px are at least44px: ${JSON.stringify(buttons)}`);
      await mobile.screenshot({ path: resolve(output, `mobile-${width}.png`) }); await close(mobile);
    }
    await mobile.context().close();
  });
  await step('Presentation and print use real stored previews and a read-only nested canvas', async () => {
    await teacher.getByRole('button', { name: 'Præsenter', exact: true }).click();
    const slides = teacher.locator('dialog.notion-slide-deck[open]');
    await slides.waitFor();
    const preview = slides.locator('.notion-drawing-preview img').first();
    const context = await drawing('teacher', canvas.id);
    process.stdout.write('PRESENTATION ' + JSON.stringify({ id: context.id, revision: context.revision, fingerprint: context.fingerprint, light: !!context.previewLightUrl, dark: !!context.previewDarkUrl }) + '\n');
    await until(async () => await preview.count() && preview.evaluate(image => image.complete && image.naturalWidth > 0), 'presentation preview loaded', 20000);
    const frame = await slides.locator('.notion-drawing-preview').first().evaluate(element => ({ client: element.clientHeight, scroll: element.scrollHeight }));
    assert.ok(frame.scroll <= frame.client + 1, 'drawing preview has no intrinsic scrolling');
    await slides.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher, false);
    assert.equal(await dialog(teacher).getByTestId('toolbar-rectangle').count(), 0);
    await close(teacher); await slides.getByRole('button', { name: 'Luk præsentation', exact: true }).click();
    await teacher.emulateMedia({ media: 'print' });
    const inlinePreview = teacher.locator('.notion-editor-root .notion-drawing-preview img').first();
    assert.ok(await inlinePreview.isVisible(), 'stored drawing preview renders in print');
    assert.equal(await dialog(teacher).count(), 0);
    await teacher.screenshot({ path: resolve(output, 'drawing-print.png'), fullPage: true });
    await teacher.pdf({ path: resolve(output, 'drawing-print.pdf'), format: 'A4', printBackground: true });
    assert.ok((await readFile(resolve(output, 'drawing-print.pdf'))).subarray(0, 5).toString().startsWith('%PDF-'));
    await teacher.emulateMedia({ media: 'screen' });
  });
  await step('Published snapshot stays immutable after its draft drawing changes', async () => {
    const snapshotPage = await api('teacher', '/api/notion/pages', { method: 'POST', body: JSON.stringify({ holdId: hold.id, title: 'QA: Fast tegning' }) });
    await navigate(teacher, `/notion/${snapshotPage.id}?hold=${hold.id}`);
    const snapshotDrawing = await insertDrawing(teacher); await shape(teacher, 'rectangle'); await stored('teacher', snapshotDrawing.id); await close(teacher); await publish(teacher);
    const published = await drawing('pupil', snapshotDrawing.id, true); assert.ok(published.snapshotId);
    await teacher.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher, true);
    await shape(teacher, 'ellipse', 45); await stored('teacher', snapshotDrawing.id, 2); await close(teacher);
    const frozen = await drawing('pupil', snapshotDrawing.id, true);
    assert.equal(frozen.snapshotId, published.snapshotId); assert.equal(frozen.sceneJson, published.sceneJson); assert.equal(liveElements(frozen).length, 1);
    await navigate(pupil, `/notion/${snapshotPage.id}?published=true&hold=${hold.id}`, false);
    await pupil.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(pupil, false);
    assert.equal(await dialog(pupil).getByTestId('toolbar-rectangle').count(), 0);
    await pupil.screenshot({ path: resolve(output, 'immutable-snapshot.png') }); await close(pupil);
  });
  await step('Template journal entries clone drawings, files and block identities independently', async () => {
    const template = await api('teacher', '/api/notebooks/templates', { method: 'POST', body: JSON.stringify({ title: 'QA: Tegneskabelon', cadence: 'free' }) });
    await navigate(teacher, '/teacher/notebook-templates/' + template.id);
    const source = await insertDrawing(teacher); await shape(teacher, 'rectangle'); await uploadImage(teacher); await stored('teacher', source.id, 2); await close(teacher);
    await teacher.getByRole('button', { name: 'Aktivér skabelon', exact: true }).click();
    await until(async () => (await api('pupil', '/api/notebooks/templates')).some(item => item.id === template.id && item.isActive), 'template drawing snapshot activated');
    await api('pupil', '/api/notebooks/mine', { method: 'POST' });
    const first = await api('pupil', '/api/notebooks/entries', { method: 'POST', body: JSON.stringify({ templateId: template.id, requestId: randomUUID() }) });
    const second = await api('pupil', '/api/notebooks/entries', { method: 'POST', body: JSON.stringify({ templateId: template.id, requestId: randomUUID() }) });
    const ids = async pageId => {
      const context = await api('pupil', `/api/notebooks/pages/${pageId}/context`), doc = new Y.Doc();
      try { Y.applyUpdate(doc, Buffer.from(context.stateBase64, 'base64')); return projectNotebookDocument(doc).drawingIds; } finally { doc.destroy(); }
    };
    const [firstId] = await ids(first.id), [secondId] = await ids(second.id);
    assert.ok(firstId && secondId && firstId !== secondId && firstId !== source.id && secondId !== source.id);
    await navigate(pupil, '/notebook/' + first.id);
    await pupil.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(pupil, true);
    await shape(pupil, 'ellipse', 30); await stored('pupil', firstId, 3); await close(pupil);
    const a = await drawing('pupil', firstId), b = await drawing('pupil', secondId), original = await drawing('teacher', source.id);
    assert.equal(liveElements(b).length, 2); assert.equal(liveElements(original).length, 2);
    assert.ok(Object.values(scene(a).files).every(file => !Object.values(scene(b).files).some(other => other.assetId === file.assetId)));
  });
  assert.deepEqual(errors, [], 'no uncaught browser errors');
  assert.ok(workers.length > 0, 'the real font subsetting worker starts');
  assert.ok(!consoleErrors.some(item => /Failed to use workers for subsetting/i.test(item.message)), 'font subsetting does not fall back after a worker error');
  if (results.some(item => !item.passed)) process.exitCode = 1;
} catch (error) {
  process.exitCode = 1;
  process.stderr.write(error.stack + '\n');
} finally {
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ fixture: fixture.fixture, results, errors, consoleErrors, requests, responses, httpErrors, workers }, null, 2));
  await browser.close();
  process.stdout.write('QA report: ' + resolve(output, 'report.json') + '\n');
}
