// Full Blazor + real controllers + Yjs acceptance against the explicit, fictional loopback fixture only.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as Y from '../../collaboration/node_modules/yjs/dist/yjs.mjs';
import { projectNotebookDocument } from '../../collaboration/src/notebook-documents.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const base = new URL(process.env.REFERENCES_QA_URL || 'http://localhost:9098');
assert.ok(base.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(base.hostname), 'only explicit loopback fixtures are allowed');
const apiBase = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(apiBase + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const identities = Object.fromEntries((await (await fetch(apiBase + '/_qa/identities')).json()).map(value => [value.key, value]));
assert.ok(identities.outsider, 'restart the fixture with the no-hold test identity');
const output = resolve(root, process.env.REFERENCES_QA_OUTPUT || '.tools/references-browser-qa');
await mkdir(output, { recursive: true });
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch({ headless: process.env.REFERENCES_QA_HEADED !== '1' });
const clients = [], results = [], errors = [];
const suffix = randomUUID().slice(0, 8), title = 'QA fælles teori ' + suffix, noteTitle = 'QA personlig mål ' + suffix;
async function until(check, description, timeout = 45000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { const value = await check(); if (value) return value; await new Promise(accept => setTimeout(accept, 150)); }
  throw new Error('Timed out: ' + description);
}
async function api(who, path, options = {}) {
  const response = await fetch(apiBase + path, { ...options, headers: { Authorization: 'Bearer ' + identities[who].token, ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers } });
  const text = await response.text(); assert.ok(response.ok, `${who} ${path}: ${response.status} ${text.slice(0, 250)}`);
  return text ? JSON.parse(text) : null;
}
async function client(who) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['blob:', 'data:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname) ? route.continue() : route.abort('blockedbyclient');
  });
  const page = await context.newPage(); clients.push({ who, context, page });
  page.on('pageerror', error => errors.push({ who, message: error.message }));
  page.on('dialog', dialog => void (dialog.type() === 'beforeunload' ? dialog.accept() : dialog.dismiss()));
  await page.goto(new URL('/_qa/login?user=' + who, base).href);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'), { timeout: 60000 });
  return page;
}
const editor = page => page.locator('.notion-blocknote-host .bn-editor[contenteditable=true]').first();
async function navigate(page, path, editable = false) {
  await page.goto(new URL(path, base).href);
  await page.locator('.notion-blocknote-host .bn-editor').first().waitFor({ timeout: 60000 });
  if (editable) await editor(page).waitFor();
}
async function saved(domain, id, predicate) {
  return until(async () => {
    const response = await fetch(`${apiBase}/api/internal/${domain}/documents/${id}/state`, { headers: { 'X-Notion-Service-Key': 'qa-notebooks-only-service-key-32-characters' } });
    if (!response.ok || response.status === 204) return false;
    const bytes = new Uint8Array(await response.arrayBuffer()), doc = new Y.Doc();
    try { Y.applyUpdate(doc, bytes); const projection = projectNotebookDocument(doc, domain === 'notion' ? 'notion' : 'notebook'); return predicate(projection) && projection; }
    finally { doc.destroy(); }
  }, 'durably stored editor content');
}
async function mention(page, query, expectedTitle, contextLabel = null) {
  await editor(page).click(); await page.keyboard.press('Control+End'); await page.keyboard.press('Enter');
  await page.keyboard.type('@' + query, { delay: 20 });
  const choices = page.locator('.bn-suggestion-menu').getByText(expectedTitle, { exact: true });
  await (contextLabel ? choices.locator('..').filter({ hasText: contextLabel }) : choices).click({ timeout: 30000 });
}
async function publish(page) {
  const button = page.getByRole('button', { name: /^(Udgiv til LUP|Udgiv til holdet|Opdatér udgivelsen)$/ });
  await until(async () => await button.isEnabled(), 'publication available after durable save');
  await button.click();
  await page.getByRole('link', { name: 'Se udgivet', exact: true }).waitFor({ timeout: 30000 });
  assert.equal(await page.locator('.notion-error[role=alert]').count(), 0);
}
async function step(name, operation) {
  const started = Date.now();
  try { await operation(); results.push({ name, passed: true, milliseconds: Date.now() - started }); process.stdout.write('PASS ' + name + '\n'); }
  catch (error) { results.push({ name, passed: false, message: error.message }); for (const client of clients) await client.page.screenshot({ path: resolve(output, 'failure-' + client.who + '.png'), fullPage: true }).catch(() => {}); throw error; }
}
try {
  const teacher = await client('teacher'), pupil = await client('pupil'), outsider = await client('outsider');
  let theoryId, note;
  await step('Create a personal target with the real notebook editor', async () => {
    const mine = await api('pupil', '/api/notebooks/mine', { method: 'POST' });
    note = await api('pupil', `/api/notebooks/${mine.id}/pages`, { method: 'POST', body: JSON.stringify({ title: noteTitle }) });
    await navigate(pupil, '/notebook/' + note.id, true);
    await editor(pupil).click(); await pupil.keyboard.type('Personligt notat med et link til fælles teori.');
    await saved('notebooks', note.id, value => value.plainText.includes('Personligt notat'));
  });
  await step('Create and edit standalone theory through the new overview', async () => {
    await teacher.goto(new URL('/notion', base).href);
    await teacher.getByRole('button', { name: 'Ny notesbog', exact: true }).click();
    const form = teacher.locator('form.books-create');
    await form.getByLabel('Titel', { exact: true }).fill(title);
    await form.getByLabel('Titel', { exact: true }).press('Tab');
    await form.locator('select').selectOption('');
    await form.getByRole('button', { name: 'Opret', exact: true }).click();
    await teacher.waitForURL(/\/notion\/[0-9a-f-]{36}(?:\?|$)/);
    theoryId = new URL(teacher.url()).pathname.split('/').at(-1);
    await editor(teacher).waitFor({ timeout: 60000 });
    await editor(teacher).click(); await teacher.keyboard.type('Teori uden hold om forbindelser i LUP.');
    await saved('notion', theoryId, value => value.plainText.includes('Teori uden hold'));
    const context = await api('teacher', `/api/notion/pages/${theoryId}/context`);
    assert.equal(context.holdId, null); assert.ok(context.collectionId);
  });
  await step('Global mentions resolve personal notes and people across the source domain', async () => {
    await mention(teacher, noteTitle, noteTitle);
    await mention(teacher, 'QA Elev Anna', 'QA Elev Anna', 'LUP-bruger');
    await saved('notion', theoryId, value => value.references.some(ref => ref.kind === 'notebook-page' && ref.id === note.id) && value.references.some(ref => ref.kind === 'person' && ref.id === identities.pupil.sub));
    const target = teacher.locator('.bn-editor a.notion-mention').filter({ hasText: noteTitle });
    await target.waitFor(); assert.equal(await target.getAttribute('href'), '/notebook/' + note.id);
    const actions = await api('pupil', '/api/me/actions?version=2');
    assert.ok(!actions.items.some(item => item.kind === 'mention' && item.targetId === theoryId), 'draft mention does not notify a pupil');
    await publish(teacher);
  });
  await step('Published theory is readable without any hold and hides inaccessible mention labels', async () => {
    await navigate(outsider, '/notion/' + theoryId);
    await outsider.getByText('Teori uden hold om forbindelser i LUP.', { exact: true }).waitFor();
    assert.equal(await editor(outsider).count(), 0);
    assert.equal(await outsider.locator('.bn-editor').getByText(noteTitle, { exact: true }).count(), 0);
    await outsider.locator('.bn-editor .notion-unavailable').getByText('Indhold er ikke tilgængeligt', { exact: true }).waitFor();
    await outsider.goto(new URL('/notion', base).href);
    await outsider.locator('.books-card').getByRole('link', { name: title, exact: true }).waitFor();
    await outsider.screenshot({ path: resolve(output, 'no-hold-overview.png'), fullPage: true });
  });
  await step('Person mention notifies once and the pupil can follow its cross-domain link', async () => {
    const actions = await until(async () => { const value = await api('pupil', '/api/me/actions?version=2'); return value.items.some(item => item.kind === 'mention' && item.targetId === theoryId) && value; }, 'published person mention appears in existing inbox');
    assert.equal(actions.items.filter(item => item.kind === 'mention' && item.targetId === theoryId).length, 1);
    await navigate(pupil, '/notion/' + theoryId);
    const target = pupil.locator('.bn-editor a.notion-mention').filter({ hasText: noteTitle });
    await target.waitFor(); assert.equal(await target.getAttribute('href'), '/notebook/' + note.id);
    await target.click(); await pupil.waitForURL('**/notebook/' + note.id);
    await editor(pupil).waitFor();
  });
  await step('Personal notes mention published theory and the graph crosses notebook boundaries', async () => {
    await mention(pupil, title, title, 'Side · Teori');
    await saved('notebooks', note.id, value => value.references.some(ref => ref.kind === 'notion-page' && ref.id === theoryId));
    const book = await api('teacher', `/api/notion/pages/${theoryId}/context`);
    await teacher.goto(new URL(`/notion/connections?scopeKind=collection&scopeId=${book.collectionId}`, base).href);
    const list = teacher.locator('details.reference-list'); await list.locator('summary').click();
    await list.getByRole('link', { name: noteTitle, exact: true }).first().waitFor();
    await list.getByRole('link', { name: title, exact: true }).first().waitFor();
    assert.ok((await list.innerText()).includes('→'), 'explicit cross-domain edge is listed');
    await teacher.screenshot({ path: resolve(output, 'cross-notebook-graph.png'), fullPage: true });
    const graph = await api('outsider', '/api/references/graph', { method: 'POST', body: JSON.stringify({ scopeKind: 'collection', scopeId: book.collectionId, limit: 400 }) });
    assert.ok(!graph.nodes.some(node => node.entity.reference.kind === 'notebook-page' && node.entity.reference.id === note.id), 'outsider graph hides the private target');
  });
  assert.equal(errors.length, 0, JSON.stringify(errors));
} finally {
  await writeFile(resolve(output, 'report.json'), JSON.stringify({ generatedAt: new Date().toISOString(), results, errors }, null, 2));
  await browser.close();
}
