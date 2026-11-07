// Loopback-only real SDK concurrency acceptance. Native UI + private test-browser clipboard; no SDK hooks.
import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as Y from '../node_modules/yjs/dist/yjs.mjs';
import { stableDrawingJson } from '../src/drawing-state.mjs';

const { createDecoder, readVarString, readVarUint, readVarUint8Array } = createRequire(new URL('../node_modules/yjs/package.json', import.meta.url))('lib0/decoding');

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const origin = new URL(process.env.DRAWING_QA_URL || 'http://localhost:9098');
assert.ok(origin.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(origin.hostname), 'This test cannot run against production');
const apiOrigin = 'http://127.0.0.1:8088';
assert.equal((await (await fetch(apiOrigin + '/_qa/health')).json()).fixture, 'fictional-notebook-browser-qa');
const identities = Object.fromEntries((await (await fetch(apiOrigin + '/_qa/identities')).json()).map(item => [item.key, item]));
const output = resolve(root, '.tools/drawing-concurrency-qa'); await mkdir(output, { recursive: true });
const focus = process.env.DRAWING_QA_FOCUS || '';
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch({ headless: true });
const results = [], errors = [], clients = [];
let drawingId;
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function until(check, description, timeout = 45000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { const result = await check(); if (result) return result; await delay(150); }
  throw new Error('Timed out: ' + description);
}
async function api(identity, path, options = {}) {
  const response = await fetch(apiOrigin + path, { ...options, headers: { Authorization: 'Bearer ' + identities[identity].token,
    ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers } });
  const body = await response.text(); assert.ok(response.ok, `${identity} ${path}: ${response.status} ${body.slice(0, 300)}`);
  return body ? JSON.parse(body) : null;
}
const dialog = page => page.locator('dialog.notion-drawing-dialog[open]');
const editor = page => page.locator('.notion-blocknote-host .bn-editor[contenteditable=true]').first();
const canvas = page => dialog(page).locator('.notion-drawing-canvas');
const live = items => items.filter(item => !item.isDeleted);
async function saved() { return api('pupil', '/api/drawings/' + drawingId + '/context'); }
async function scene() { return JSON.parse((await saved()).sceneJson); }
async function ready(page) {
  await dialog(page).waitFor({ timeout: 60000 });
  await dialog(page).getByTestId('toolbar-rectangle').waitFor({ timeout: 60000 });
  await dialog(page).locator('.excalidraw canvas').first().waitFor();
}

async function client(identity) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 980 }, serviceWorkers: 'block' });
  // SDK Ctrl+C serializes its actual scene, not DrawingController.elements. Keep it inside this test browser.
  await context.addInitScript(() => {
    let text = '';
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {
      writeText: async value => { text = String(value); }, readText: async () => text,
      write: async items => { for (const item of items) if (item.types.includes('text/plain')) text = await (await item.getType('text/plain')).text(); },
      read: async () => [],
    } });
  });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['data:', 'blob:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname) ? route.continue() : route.abort('blockedbyclient');
  });
  const gate = { paused: false, offline: false, queued: [], sockets: new Set(), observed: new Y.Doc() };
  await context.routeWebSocket(url => url.pathname === '/collaboration', route => {
    if (gate.offline) { void route.close({ code: 1001, reason: 'QA reconnect gate' }); return; }
    const server = route.connectToServer(); const pair = { route, server }; gate.sockets.add(pair);
    const forward = (target, message) => {
      if (gate.paused) gate.queued.push({ target, message });
      else { try { target.send(message); } catch { /* A reconnect can close an old socket. */ } }
    };
    route.onMessage(message => forward(server, message)); server.onMessage(message => {
      try {
        const decoder = createDecoder(typeof message === 'string' ? Buffer.from(message) : message);
        if (readVarString(decoder) === 'drawing:' + drawingId && readVarUint(decoder) === 0) {
          const subtype = readVarUint(decoder);
          if (subtype === 1 || subtype === 2) Y.applyUpdate(gate.observed, readVarUint8Array(decoder));
        }
      } catch { /* Non-drawing or non-sync frames are not relevant to the observation. */ }
      forward(route, message);
    });
    route.onClose((code, reason) => { gate.sockets.delete(pair); void server.close({ code, reason }).catch(() => {}); });
    server.onClose((code, reason) => { gate.sockets.delete(pair); void route.close({ code, reason }).catch(() => {}); });
  });
  const page = await context.newPage(); page.setDefaultTimeout(45000);
  page.on('pageerror', error => errors.push({ identity, message: error.message }));
  page.on('dialog', value => { void value.accept(); });
  await page.goto(new URL('/_qa/login?user=' + identity, origin).href);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'), { timeout: 60000 });
  const result = { page, context, identity, gate }; clients.push(result); return result;
}
async function open(client, path) {
  await client.page.goto(new URL(path, origin).href);
  await client.page.locator('.notion-blocknote-host .bn-editor').first().waitFor({ timeout: 60000 });
}
async function draw(client, type, relative = { x: .36, y: .37 }) {
  const { page } = client; await dialog(page).getByTestId('toolbar-' + type).locator('..').click();
  const bounds = await canvas(page).boundingBox();
  const point = { x: bounds.x + bounds.width * relative.x, y: bounds.y + bounds.height * relative.y };
  await page.mouse.move(point.x, point.y); await page.mouse.down();
  await page.mouse.move(point.x + 115, point.y + 80, { steps: 10 }); await page.mouse.up(); await page.keyboard.press('Escape');
  return { x: point.x - bounds.x, y: point.y - bounds.y };
}
async function copySdk(client) {
  const { page } = client; const bounds = await canvas(page).boundingBox();
  await page.keyboard.press('Escape'); await page.keyboard.press('v');
  await page.mouse.click(bounds.x + bounds.width * .95, bounds.y + bounds.height * .52);
  await page.evaluate(() => navigator.clipboard.writeText(''));
  await page.keyboard.press('Control+a'); await page.keyboard.press('Control+c');
  const contents = await until(async () => {
    const text = await page.evaluate(() => navigator.clipboard.readText());
    try { const data = JSON.parse(text); return data.type === 'excalidraw/clipboard' && Array.isArray(data.elements) && data; } catch { return false; }
  }, 'native Excalidraw clipboard serialization');
  await page.keyboard.press('Escape'); return contents.elements;
}
async function converge(a, b, description) {
  let observed;
  await until(async () => {
    const stored = await scene(); const [first, second] = await Promise.all([copySdk(a), copySdk(b)]);
    observed = { stored: live(stored.elements), first, second };
    return stableDrawingJson(observed.first) === stableDrawingJson(observed.second)
      && stableDrawingJson(observed.first) === stableDrawingJson(observed.stored);
  }, description);
  return observed.stored;
}
function release(client) {
  client.gate.paused = false;
  const frames = client.gate.queued.splice(0);
  for (const { target, message } of frames) { try { target.send(message); } catch { } }
}
async function drag(client, position, delta, steps) {
  const { page } = client; const bounds = await canvas(page).boundingBox();
  await page.keyboard.press('Escape'); await page.keyboard.press('v');
  await page.mouse.click(bounds.x + bounds.width * .95, bounds.y + bounds.height * .52);
  const x = bounds.x + position.x + 2, y = bounds.y + position.y + 40;
  await page.mouse.move(x, y); await page.mouse.down();
  for (let i = 1; i <= steps; i++) { await page.mouse.move(x + delta * i / steps, y); await delay(20); }
  await page.mouse.up(); await page.keyboard.press('Escape');
}
async function step(name, operation) {
  const start = Date.now();
  try { const data = await operation(); results.push({ name, passed: true, milliseconds: Date.now() - start, ...data }); console.log('PASS', name, JSON.stringify(data || {})); }
  catch (error) { results.push({ name, passed: false, message: error.message }); throw error; }
}

try {
  const pupil = await client('pupil'); const teacher = await client('teacher');
  const notebook = await api('pupil', '/api/notebooks/mine', { method: 'POST' });
  const note = await api('pupil', `/api/notebooks/${notebook.id}/pages`, { method: 'POST', body: JSON.stringify({ title: 'QA SDK concurrency ' + randomUUID().slice(0, 8) }) });
  await open(pupil, '/notebook/' + note.id);
  const created = pupil.page.waitForResponse(response => new URL(response.url()).pathname === '/api/drawings' && response.request().method() === 'POST' && response.ok());
  await editor(pupil.page).click(); await pupil.page.keyboard.press('Control+End'); await pupil.page.keyboard.type('/tegning', { delay: 30 });
  await pupil.page.locator('.bn-suggestion-menu').getByText('Tegning', { exact: true }).click();
  drawingId = (await (await created).json()).id; await ready(pupil.page);
  const initialPosition = await draw(pupil, 'rectangle');
  await until(async () => live((await scene()).elements).length === 1, 'first native SDK rectangle durably stored');
  const original = (await copySdk(pupil))[0]; assert.equal(original.type, 'rectangle');
  const firstState = await saved(); const firstClients = [...Y.decodeStateVector(Y.encodeStateVectorFromUpdate(Buffer.from(firstState.stateBase64, 'base64'))).keys()];
  assert.equal(firstClients.length, 1, 'fixture has one actual SDK author before the second joins');
  await api('pupil', `/api/drawings/${drawingId}/grants`, { method: 'PUT', body: JSON.stringify({ userSub: identities.teacher.sub }) });
  await open(teacher, '/notebook/' + note.id); await teacher.page.getByRole('button', { name: 'Åbn tegning', exact: true }).click(); await ready(teacher.page);
  await step('Real SDK clients retain independent user shapes and agree with persisted layer order', async () => {
    await draw(teacher, 'ellipse', { x: .72, y: .62 });
    await until(async () => live((await scene()).elements).length === 2, 'second author native ellipse saved');
    const elements = await converge(pupil, teacher, 'both SDK scenes and saved JSON contain both shapes');
    const area = await canvas(teacher.page).boundingBox(); await teacher.page.mouse.move(area.x + area.width * .64, area.y + area.height * .35);
    await pupil.page.locator('.excalidraw .UserList__collaborator').first().waitFor();
    await delay(100); await pupil.page.screenshot({ path: resolve(output, 'native-presence' + (focus ? '-' + focus : '') + '.png') });
    return { elements: elements.map(item => ({ id: item.id, type: item.type, index: item.index })) };
  });
  const secondState = await saved(); const allClients = [...Y.decodeStateVector(Y.encodeStateVectorFromUpdate(Buffer.from(secondState.stateBase64, 'base64'))).keys()];
  const secondClient = allClients.find(id => !firstClients.includes(id)); assert.ok(secondClient);
  const winner = firstClients[0] > secondClient ? pupil : teacher, loser = winner === pupil ? teacher : pupil;
  if (!focus || focus === 'concurrent') {
  await step('Concurrent same-element drags converge even when the CRDT winner has a lower SDK version', async () => {
    pupil.gate.paused = teacher.gate.paused = true;
    await Promise.all([drag(winner, initialPosition, 80, 1), drag(loser, initialPosition, -65, 18)]);
    await delay(200);
    const [first, second] = await Promise.all([copySdk(winner), copySdk(loser)]);
    const winningElement = first.find(item => item.id === original.id), losingElement = second.find(item => item.id === original.id);
    assert.ok(winningElement && losingElement && winningElement.x !== losingElement.x, 'the two SDK clients hold different concurrent movements');
    assert.ok(winningElement.version < losingElement.version, 'the intended CRDT winner has fewer SDK pointer updates');
    assert.ok(winner.gate.queued.length && loser.gate.queued.length, 'both clients actually queued network traffic');
    release(pupil); release(teacher);
    const elements = await converge(pupil, teacher, 'same-element SDK and persisted scenes converge after releasing delayed frames');
    assert.equal(elements.find(item => item.id === original.id).x, winningElement.x);
    return { winner: winner.identity, winnerVersion: winningElement.version, loserVersion: losingElement.version, x: winningElement.x };
  });
  await step('Native layer shortcuts propagate the SDK order without changing unrelated elements', async () => {
    const before = await copySdk(pupil), rectangle = before.find(item => item.id === original.id);
    const bounds = await canvas(pupil.page).boundingBox();
    await pupil.page.keyboard.press('v'); await pupil.page.mouse.click(bounds.x + bounds.width * .95, bounds.y + bounds.height * .52);
    await pupil.page.mouse.click(bounds.x + initialPosition.x + rectangle.x - original.x + 2, bounds.y + initialPosition.y + 40);
    await pupil.page.keyboard.press('Control+Shift+BracketRight'); await pupil.page.keyboard.press('Escape');
    const elements = await converge(pupil, teacher, 'native layer order persisted and seen by the second SDK');
    assert.equal(elements.at(-1).id, original.id, 'bring-to-front places the first rectangle last');
    assert.deepEqual(elements.map(item => item.id).sort(), before.map(item => item.id).sort());
    return { order: elements.map(item => item.id) };
  });
  }
  if (!focus || focus === 'undo') {
  await step('Native local undo keeps a remote user shape after the remote update', async () => {
    const before = (await copySdk(pupil)).map(item => item.id);
    await draw(pupil, 'rectangle', { x: .29, y: .68 });
    const local = await until(async () => { const value = live((await scene()).elements); return value.length === before.length + 1 && value; }, 'local shape saved before remote edit');
    const localId = local.find(item => !before.includes(item.id)).id;
    const undoBeforeRemote = await dialog(pupil.page).getByTestId('button-undo').isDisabled();
    await draw(teacher, 'diamond', { x: .73, y: .26 });
    // Ctrl+A itself is a native SDK history action. Do not select/copy on the undoing client here.
    const withRemote = await until(async () => { const value = live((await scene()).elements); return value.length === before.length + 2 && value; }, 'remote shape durably stored before undo');
    const remoteId = withRemote.find(item => !before.includes(item.id) && item.id !== localId).id;
    const remoteVersion = withRemote.find(item => item.id === remoteId).version;
    await until(() => pupil.gate.observed.getMap('elements').get(remoteId)?.version === remoteVersion, 'the remote sync frame reached the undoing browser');
    await pupil.page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    const undoAfterRemote = await dialog(pupil.page).getByTestId('button-undo').isDisabled();
    console.log('UNDO diagnostic', JSON.stringify({ localId, remoteId, undoBeforeRemote, undoAfterRemote, before: withRemote.map(item => ({ id: item.id, type: item.type })) }));
    assert.equal(undoAfterRemote, false, 'the SDK retains an available local undo after the remote update');
    await dialog(pupil.page).getByTestId('button-undo').click();
    await until(async () => !live((await scene()).elements).some(item => item.id === localId), 'native undo removes the local shape from the durable scene', 12000);
    const undone = await converge(pupil, teacher, 'native undo converges without removing the remote shape');
    assert.ok(!undone.some(item => item.id === localId), 'the local shape is undone');
    assert.ok(undone.some(item => item.id === remoteId), 'the remote shape is retained');
    return { localId, remoteId, retained: undone.map(item => item.id) };
  });
  }
  if (!focus || focus === 'delete') {
  await step('Concurrent move versus delete converges to the canonical tombstone in both native SDK scenes', async () => {
    const before = (await copySdk(pupil)).map(item => item.id);
    const position = await draw(pupil, 'rectangle', { x: .47, y: .18 });
    await until(async () => live((await scene()).elements).length === before.length + 1, 'new target shape stored for move/delete test');
    const synchronized = await converge(pupil, teacher, 'target shape available to both clients before concurrent delete');
    const target = synchronized.find(item => !before.includes(item.id)); assert.ok(target);
    const deleter = winner, mover = loser;
    pupil.gate.paused = teacher.gate.paused = true;
    const remove = async () => {
      const bounds = await canvas(deleter.page).boundingBox(); await deleter.page.keyboard.press('v');
      await deleter.page.mouse.click(bounds.x + bounds.width * .95, bounds.y + bounds.height * .52);
      await deleter.page.mouse.click(bounds.x + position.x + 2, bounds.y + position.y + 40);
      await deleter.page.keyboard.press('Delete');
    };
    await Promise.all([remove(), drag(mover, position, -70, 15)]); await delay(200);
    const deleted = await copySdk(deleter), moved = await copySdk(mover);
    assert.ok(!deleted.some(item => item.id === target.id), 'the deleting SDK no longer displays the target');
    assert.ok(moved.some(item => item.id === target.id && item.x !== target.x), 'the moving SDK concurrently retains its changed target');
    assert.ok(deleter.gate.queued.length && mover.gate.queued.length);
    release(pupil); release(teacher);
    const elements = await converge(pupil, teacher, 'move versus delete produces identical actual SDK and persisted scenes');
    assert.ok(!elements.some(item => item.id === target.id));
    const tombstone = (await scene()).elements.find(item => item.id === target.id); assert.equal(tombstone?.isDeleted, true);
    return { targetId: target.id, deleter: deleter.identity, deletedVersion: tombstone.version, movedVersion: moved.find(item => item.id === target.id).version };
  });
  }
  if (!focus || focus === 'reconnect') {
  await step('Disconnected client becomes read-only and its actual SDK catches up on reconnect', async () => {
    teacher.gate.offline = true;
    for (const { route, server } of [...teacher.gate.sockets]) { await route.close({ code: 1001, reason: 'QA temporary transport loss' }); await server.close({ code: 1001 }).catch(() => {}); }
    await until(async () => !(await dialog(teacher.page).getByTestId('toolbar-rectangle').count()), 'disconnected drawing pauses editing');
    const count = live((await scene()).elements).length;
    await draw(pupil, 'ellipse', { x: .48, y: .74 });
    await until(async () => live((await scene()).elements).length === count + 1, 'online peer drawing saved during disconnect');
    teacher.gate.offline = false;
    await ready(teacher.page);
    const elements = await converge(pupil, teacher, 'reconnected SDK agrees with the peer and persisted scene');
    return { count: elements.length };
  });
  }
  assert.deepEqual(errors, [], 'no uncaught browser runtime errors');
} catch (error) {
  for (const [index, item] of clients.entries()) await item.page.screenshot({ path: resolve(output, `failure-${index}.png`), fullPage: true }).catch(() => {});
  console.error(error.stack || error); process.exitCode = 1;
} finally {
  for (const client of clients) { release(client); await client.context.close().catch(() => {}); client.gate.observed.destroy(); }
  await browser.close();
  await writeFile(resolve(output, 'report' + (focus ? '-' + focus : '') + '.json'), JSON.stringify({ drawingId, results, errors }, null, 2));
}
