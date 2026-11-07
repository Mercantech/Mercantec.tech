// Focused visual/private-viewport check after CSS or initial-fit changes. Requires the real loopback QA fixture.
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const origin = 'http://localhost:9098', apiOrigin = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(apiOrigin + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const teacher = (await (await fetch(apiOrigin + '/_qa/identities')).json()).find(item => item.key === 'teacher');
const auth = { Authorization: 'Bearer ' + teacher.token };
const output = resolve('.tools/drawing-visual-qa'); await mkdir(output, { recursive: true });
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch({ headless: true }), results = [], contexts = [], states = [];
async function request(path, body) {
  const response = await fetch(apiOrigin + path, { method: body ? 'POST' : 'GET', headers: { ...auth, ...(body ? { 'Content-Type': 'application/json' } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
  assert.ok(response.ok, `${path}: ${response.status}`); return response.json();
}
async function until(check, description, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await check()) return; await new Promise(accept => setTimeout(accept, 100)); }
  throw new Error('Timed out: ' + description);
}
async function client(width = 1365, mobile = false) {
  const context = await browser.newContext({ viewport: { width, height: 950 }, ...(mobile ? { isMobile: true, hasTouch: true } : {}) }); contexts.push(context);
  await context.route('**/*', route => { const url = new URL(route.request().url()); return ['localhost', '127.0.0.1'].includes(url.hostname) || ['data:', 'blob:'].includes(url.protocol) ? route.continue() : route.abort(); });
  const page = await context.newPage(); page.on('dialog', value => { void value.accept(); });
  await page.goto(origin + '/_qa/login?user=teacher'); await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  return page;
}
const drawing = page => page.locator('dialog.notion-drawing-dialog[open]');
async function ready(page) {
  await drawing(page).waitFor();
  await until(() => drawing(page).getByTestId('toolbar-rectangle').isVisible(), 'SDK toolbar');
}
async function close(page) { await drawing(page).getByRole('button', { name: 'Luk tegning' }).click(); await drawing(page).waitFor({ state: 'hidden' }); }
async function open(page) { await page.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(page); }
try {
  const page = await client(), [hold] = await request('/api/notion/holds');
  const document = await request('/api/notion/pages', { holdId: hold.id, title: 'QA: Tema og privat viewport' });
  const route = `/notion/${document.id}?hold=${hold.id}`;
  await page.goto(origin + route); await page.locator('.bn-editor[contenteditable=true]').waitFor();
  const creating = page.waitForResponse(response => new URL(response.url()).pathname === '/api/drawings' && response.request().method() === 'POST');
  await page.locator('.bn-editor[contenteditable=true]').click(); await page.keyboard.type('/tegning');
  await page.locator('.bn-suggestion-menu').getByText('Tegning', { exact: true }).click();
  const canvas = await (await creating).json(); await ready(page);
  await drawing(page).getByTestId('toolbar-rectangle').locator('..').click();
  const area = await drawing(page).locator('.notion-drawing-canvas').boundingBox();
  await page.mouse.move(area.x + area.width * .58, area.y + area.height * .45); await page.mouse.down();
  await page.mouse.move(area.x + area.width * .58 + 120, area.y + area.height * .45 + 90, { steps: 12 }); await page.mouse.up(); await page.keyboard.press('Escape');
  let baseline;
  let lastSave = '', stableSince = Date.now();
  await until(async () => {
    const value = await request(`/api/drawings/${canvas.id}/context`), signature = `${value.revision}:${value.fingerprint}`;
    if (signature !== lastSave) { lastSave = signature; stableSince = Date.now(); }
    if (JSON.parse(value.sceneJson).elements.length && value.previewLightUrl && value.previewDarkUrl
      && (await drawing(page).locator('.notion-drawing-status').innerText()) === 'Gemt' && Date.now() - stableSince >= 900) { baseline = value; return true; }
    return false;
  }, 'last-generation saved status and stable drawing/previews');
  const checkpoint = async label => { const value = await request(`/api/drawings/${canvas.id}/context`); states.push({ label, revision: value.revision, fingerprint: value.fingerprint, scene: JSON.parse(value.sceneJson) }); return value; };
  await checkpoint('baseline before close');
  await close(page);
  await checkpoint('after first close');
  assert.ok(await page.locator('.notion-drawing-preview img').first().isVisible());
  for (const [theme, scheme] of [['system', 'light'], ['system', 'dark'], ['light', 'light'], ['dark', 'light'], ['christmas', 'light'], ['newyear', 'light'], ['hacker', 'light']]) {
    await page.emulateMedia({ colorScheme: scheme }); await page.evaluate(value => window.lupTheme.set(value), theme); await open(page);
    const colors = await drawing(page).locator('.excalidraw').evaluate(element => {
      const style = getComputedStyle(element), icon = element.querySelector('input[data-testid="toolbar-selection"]')?.parentElement?.querySelector('.ToolIcon__icon');
      return { paper: style.getPropertyValue('--color-surface-lowest'), tint: style.getPropertyValue('--color-surface-primary-container'), selected: icon ? getComputedStyle(icon).backgroundColor : null };
    });
    const frame = await drawing(page).boundingBox(); assert.ok(frame.width <= 1366 && frame.x >= -1);
    await page.screenshot({ path: resolve(output, `${theme}-${scheme}.png`) }); results.push({ theme, scheme, colors }); await close(page); await checkpoint(`after ${theme}-${scheme}`);
  }
  await open(page); await drawing(page).locator('.zoom-in-button').click(); await drawing(page).locator('.zoom-in-button').click();
  const zoom = await drawing(page).locator('.reset-zoom-button').innerText();
  await checkpoint('after zoom in');
  const hand = drawing(page).getByTestId('toolbar-hand');
  if (await hand.count()) await hand.locator('..').click(); else await page.keyboard.press('h');
  const view = await drawing(page).locator('.notion-drawing-canvas').boundingBox();
  await page.mouse.move(view.x + view.width * .5, view.y + view.height * .55); await page.mouse.down(); await page.mouse.move(view.x + view.width * .5 + 35, view.y + view.height * .55 + 20, { steps: 6 }); await page.mouse.up();
  await checkpoint('after pan');
  await close(page); await open(page); assert.equal(await drawing(page).locator('.reset-zoom-button').innerText(), zoom, 'private zoom preserved when reopening'); await close(page); await checkpoint('after zoom pan reopen');
  const mobile = await client(320, true); await mobile.goto(origin + route); await mobile.locator('.bn-editor').waitFor(); await open(mobile);
  await until(async () => !(await drawing(mobile).getByRole('button', { name: /Scroll tilbage til indhold|Scroll back to content/i }).count()), 'first mobile opening fits drawing content');
  await mobile.screenshot({ path: resolve(output, 'mobile-320-first-fit.png') }); await close(mobile);
  await checkpoint('after fresh mobile first-fit');
  const final = await request(`/api/drawings/${canvas.id}/context`);
  assert.equal(final.fingerprint, baseline.fingerprint, 'theme, zoom, pan and first-fit do not alter shared drawing state');
  assert.equal(final.revision, baseline.revision, 'private viewport does not create document revisions');
  results.push({ privateViewport: 'zoom preserved; first mobile view fits; fingerprint and revision unchanged' });
  process.stdout.write('PASS visual themes, preview, mobile first-fit and private zoom/pan\n');
} catch (error) {
  process.exitCode = 1; results.push({ failure: error.message }); process.stderr.write(error.stack + '\n');
} finally {
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ results, states }, null, 2)); await browser.close();
}
