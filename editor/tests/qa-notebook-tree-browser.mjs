// Run after the fictional notebook browser fixture has started (see tests/Notebook.BrowserQa/README.md).
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const base = new URL('http://localhost:9098');
const apiBase = 'http://127.0.0.1:8088';
const health = await fetch(apiBase + '/_qa/health');
assert.equal((await health.json()).fixture, 'fictional-notebook-browser-qa');
const identities = Object.fromEntries((await (await fetch(apiBase + '/_qa/identities')).json())
  .map(identity => [identity.key, identity]));
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const suffix = randomUUID().slice(0, 8);
const errors = [];

function api(who, path, body) {
  return fetch(apiBase + path, {
    method: body ? 'POST' : 'GET',
    headers: { Authorization: 'Bearer ' + identities[who].token,
      ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
}

async function json(who, path, body) {
  const response = await api(who, path, body);
  assert.ok(response.ok, `${who} ${path}: ${response.status} ${response.ok ? '' : await response.text()}`);
  return response.json();
}

async function client(who, options = {}) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 960 }, serviceWorkers: 'block', ...options });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['blob:', 'data:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname)
      ? route.continue() : route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  page.setDefaultTimeout(15000);
  page.on('pageerror', error => errors.push(error.message));
  page.on('dialog', dialog => void (dialog.type() === 'beforeunload' ? dialog.accept() : dialog.dismiss()));
  await page.goto(new URL('/_qa/login?user=' + who, base).href);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'), { timeout: 60000 });
  return page;
}

async function until(read, matches, description) {
  const deadline = Date.now() + 15000;
  while (Date.now() < deadline) {
    const value = await read();
    if (matches(value)) return value;
    await new Promise(accept => setTimeout(accept, 100));
  }
  throw new Error('Timed out: ' + description);
}

const sidebar = page => page.getByRole('complementary', { name: 'Notesbogens sider' });
const actions = (page, title) => page.getByRole('button', { name: 'Sidehandlinger: ' + title, exact: true });
const row = (page, title) => sidebar(page).locator('.nb-tree-row[data-page-id]').filter({
  has: actions(page, title),
});

async function openSidebar(page, path) {
  await page.goto(new URL(path, base).href);
  if (path.startsWith('/notion')) {
    // The desktop sidebar is collapsible and its state persists across pages.
    const toggle = page.locator('.notion-sidebar-toggle');
    await toggle.waitFor();
    if (!await sidebar(page).isVisible()) await toggle.click();
  }
  await sidebar(page).waitFor();
}

async function create(page, title, openForm) {
  await openForm();
  const form = sidebar(page).locator('form').filter({ has: page.getByRole('button', { name: 'Opret', exact: true }) });
  const input = form.getByRole('textbox');
  await input.fill(title);
  await input.press('Tab');
  await form.getByRole('button', { name: 'Opret', exact: true }).click();
  const result = row(page, title);
  await result.waitFor();
  return result.getAttribute('data-page-id');
}

async function dragToRoot(page, source) {
  assert.equal(await source.getAttribute('draggable'), 'true');
  const start = await source.boundingBox();
  assert.ok(start, 'source row should be visible');
  const x = start.x + start.width / 2, y = start.y + start.height / 2;
  await page.mouse.move(x, y);
  await page.mouse.down();
  try {
    await page.mouse.move(x + 12, y, { steps: 4 });
    const target = sidebar(page).locator('[data-tree-drop=root]');
    await target.waitFor();
    const drop = await target.boundingBox();
    assert.ok(drop, 'drop target should be visible while dragging');
    await page.mouse.move(drop.x + drop.width / 2, drop.y + drop.height / 2, { steps: 8 });
    // Chromium may require another move over the target to emit dragover before drop.
    await page.mouse.move(drop.x + drop.width / 2 + 1, drop.y + drop.height / 2);
  } finally {
    await page.mouse.up();
  }
}

async function moveInto(page, title, destination) {
  await actions(page, title).press('Enter');
  await page.getByRole('button', { name: 'Flyt til', exact: true }).press('Enter');
  const dialog = page.getByRole('dialog', { name: 'Flyt side', exact: true });
  await dialog.getByRole('combobox', { name: 'Placering' }).selectOption('inside');
  await dialog.getByRole('combobox', { name: /^Side/ }).selectOption(destination);
  await dialog.getByRole('button', { name: 'Flyt side', exact: true }).press('Enter');
  await dialog.waitFor({ state: 'hidden' });
}

async function trashAndRestore(page, title, readPages, who, pagePath, id, childId) {
  const item = row(page, title);
  await actions(page, title).click();
  await page.getByRole('button', { name: 'Slet', exact: true }).click();
  await page.getByText(/2 sider flyttes til papirkurven, inklusive undersider/).waitFor();
  await page.getByRole('button', { name: 'Flyt til papirkurv', exact: true }).click();
  await item.waitFor({ state: 'detached' });
  for (const removed of [id, childId]) {
    assert.equal((await api(who, pagePath(removed))).status, 404, 'direct route must not expose a trashed page');
  }
  assert.ok(!(await readPages()).some(page => page.id === id || page.id === childId));

  await sidebar(page).getByRole('button', { name: 'Papirkurv', exact: true }).click();
  const trash = page.getByRole('dialog', { name: 'Papirkurv', exact: true });
  const restore = trash.getByRole('button', { name: 'Gendan: ' + title, exact: true });
  await restore.click();
  await restore.waitFor({ state: 'detached' });
  await until(readPages, pages => pages.some(page => page.id === id && page.parentPageId === null)
    && pages.some(page => page.id === childId && page.parentPageId === id),
  'restored branch is readable at the same IDs and in its original hierarchy');
  assert.ok((await api(who, pagePath(id))).ok);
}

async function treeFlow({ page, who, path, rootButton, readPages, apiPagePath, label }) {
  await openSidebar(page, path);
  const rootTitle = `${label} QA rod ${suffix}`, childTitle = `${label} QA underside ${suffix}`;
  const rootId = await create(page, rootTitle,
    () => sidebar(page).getByRole('button', { name: rootButton, exact: true }).click());
  const childId = await create(page, childTitle, async () => {
    await row(page, rootTitle).click({ button: 'right' });
    await page.getByRole('dialog', { name: 'Sidehandlinger', exact: true })
      .getByRole('button', { name: 'Ny underside', exact: true }).click();
  });
  await until(readPages, pages => pages.some(page => page.id === rootId && page.parentPageId === null)
    && pages.some(page => page.id === childId && page.parentPageId === rootId), 'child created from the selected row');
  await dragToRoot(page, row(page, childTitle));
  await until(readPages, pages => [rootId, childId].every(id => pages.some(page => page.id === id && page.parentPageId === null)),
    'drag to root preserves both root pages after the server reloads');
  await openSidebar(page, path);
  await row(page, rootTitle).waitFor();
  await row(page, childTitle).waitFor();
  await moveInto(page, childTitle, rootId);
  await until(readPages, pages => pages.some(page => page.id === childId && page.parentPageId === rootId),
    'accessible move action returns the page to the chosen branch');
  await trashAndRestore(page, rootTitle, readPages, who, apiPagePath, rootId, childId);
  process.stdout.write(`PASS ${label}: right-click, drag, independent roots, keyboard move, branch trash and restore\n`);
  return { rootId, rootTitle };
}

async function editRestoredPage(page, path, text) {
  await page.goto(new URL(path, base).href);
  const editor = page.locator('.notion-blocknote-host .bn-editor[contenteditable=true]').first();
  await editor.waitFor({ timeout: 60000 });
  await editor.click();
  await page.keyboard.insertText(text);
  await editor.getByText(text, { exact: true }).waitFor();
  // Reload until the collaboration service has durably persisted the new revision.
  await page.getByRole('status').filter({ hasText: 'Gemt i skyen' }).first().waitFor();
  await page.reload();
  await editor.getByText(text, { exact: true }).waitFor({ timeout: 45000 });
}

async function mobileActions(who, id, title) {
  const page = await client(who, { viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  await page.goto(new URL('/notebook/' + id, base).href);
  const toggle = page.getByRole('button', { name: 'Sider og værktøjer', exact: true });
  await toggle.waitFor();
  if (!await sidebar(page).isVisible()) await toggle.tap();
  await actions(page, title).tap();
  const dialog = page.getByRole('dialog', { name: 'Sidehandlinger', exact: true });
  await dialog.getByRole('button', { name: 'Flyt til', exact: true }).waitFor();
  await dialog.getByRole('button', { name: 'Luk', exact: true }).tap();
  await dialog.waitFor({ state: 'hidden' });
}

try {
  const teacher = await client('teacher');
  const pupil = await client('pupil');
  const collection = await json('teacher', '/api/notion/collections',
    { title: 'QA forløb ' + suffix, holdId: '22222222-aaaa-4222-8222-222222222222' });
  const lesson = await treeFlow({
    page: teacher, who: 'teacher', path: '/notion/collections/' + collection.id, rootButton: 'Ny side',
    readPages: () => json('teacher', '/api/notion/pages?collectionId=' + collection.id),
    apiPagePath: id => '/api/notion/pages/' + id + '/context', label: 'Forløb',
  });
  assert.equal(await teacher.getByRole('combobox', { name: 'Vælg hold', exact: true }).count(), 0);
  await editRestoredPage(teacher, '/notion/' + lesson.rootId, 'QA restaureret undervisningsside ' + suffix);
  process.stdout.write('PASS restored teaching page accepts fresh content\n');

  const personal = await json('pupil', '/api/notebooks/mine', {});
  const note = await treeFlow({
    page: pupil, who: 'pupil', path: '/notebook', rootButton: 'Ny note',
    readPages: async () => (await json('pupil', '/api/notebooks/' + personal.id + '/pages')).items,
    apiPagePath: id => '/api/notebooks/pages/' + id + '/context', label: 'Personlig notesbog',
  });
  await editRestoredPage(pupil, '/notebook/' + note.rootId, 'QA restaureret note ' + suffix);
  await mobileActions('pupil', note.rootId, note.rootTitle);
  process.stdout.write('PASS restored personal note accepts fresh content and exposes touch actions\n');
  assert.deepEqual(errors, []);
} finally {
  await browser.close();
}
