const test = require('node:test');
const assert = require('node:assert/strict');
const { readFile } = require('node:fs/promises');
const { resolve } = require('node:path');

// Exercise the browser module itself. These adapters expose only the DOM surface
// used by its animation; no app account, browser or live classroom is involved.
async function environment(t, { reduced = false } = {}) {
  const originals = new Map();
  const install = (name, value) => {
    originals.set(name, Object.getOwnPropertyDescriptor(globalThis, name));
    Object.defineProperty(globalThis, name, { value, writable: true, configurable: true });
  };
  class Document extends EventTarget {
    constructor() { super(); this.body = { style: { overflow: '' } }; this.activeElement = null; }
    closest() { return null; }
  }
  const document = new Document();
  const values = new Map();
  const localStorage = { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
  const clipboard = { text: null, async writeText(value) { this.text = value; } };
  install('document', document);
  install('window', { innerWidth: 1200, innerHeight: 800, matchMedia: () => ({ matches: reduced }) });
  install('navigator', { clipboard }); install('localStorage', localStorage);
  install('cancelAnimationFrame', () => {}); install('requestAnimationFrame', callback => setTimeout(() => callback(performance.now()), 1));
  const source = await readFile(resolve(__dirname, '../web/wwwroot/js/group-roulette-show.js'), 'utf8');
  const module = await import(`data:text/javascript;base64,${Buffer.from(source + `\n// test instance ${Math.random()}`).toString('base64')}`);
  let canvasCalls = 0;
  class Root extends EventTarget {
    constructor() {
      super(); this.isConnected = true; this.shell = { inert: false };
      this.button = { disabled: false, clicks: 0, click() { this.clicks++; } };
      this.reels = Array.from({ length: 3 }, () => {
        let text = 'Klar?'; const node = { writes: 0, style: { setProperty() {} }, parentElement: { clientWidth: 190 } };
        Object.defineProperty(node, 'textContent', { get: () => text, set: value => { text = value; node.writes++; } });
        return { node, classes: new Set(), classList: { add(value) { this.owner.add(value); }, remove(value) { this.owner.delete(value); }, owner: new Set() }, querySelector: () => node };
      });
    }
    querySelectorAll(selector) { return selector === '.roulette-reel' ? this.reels : []; }
    querySelector(selector) {
      if (selector === '.roulette-spin') return this.button;
      if (selector === '.roulette-shell') return this.shell;
      if (selector === '.roulette-confetti') return { width: 1200, height: 800, getContext() { canvasCalls++; return { clearRect() {} }; } };
      return null;
    }
  }
  const root = new Root();
  module.initialize(root, { invokeMethodAsync: async () => {} });
  t.after(async () => {
    await module.dispose(root);
    for (const [name, descriptor] of originals) {
      if (descriptor) Object.defineProperty(globalThis, name, descriptor);
      else delete globalThis[name];
    }
  });
  return { module, root, document, localStorage, clipboard, canvasCalls: () => canvasCalls };
}

test('reduced motion reveals the chosen names without cycling or confetti', async t => {
  const { module, root, canvasCalls } = await environment(t, { reduced: true });
  assert.equal(await module.spin(root, ['A', 'B', 'C', 'D'], ['Clara', 'Bilal', 'Asta'], false), true);
  assert.deepEqual(root.reels.map(reel => reel.node.textContent), ['Clara', 'Bilal', 'Asta']);
  assert.deepEqual(root.reels.map(reel => reel.node.writes), [1, 1, 1]);
  assert.equal(canvasCalls(), 0);
});

test('cancelling a mounted spin settles its promise and stops name changes', async t => {
  const { module, root } = await environment(t);
  const spinning = module.spin(root, ['A', 'B', 'C', 'D'], ['D', 'B', 'A'], false);
  const atCancel = root.reels.map(reel => reel.node.textContent);
  module.cancel(root);
  assert.equal(await spinning, false);
  await new Promise(resolve => setTimeout(resolve, 160));
  assert.deepEqual(root.reels.map(reel => reel.node.textContent), atCancel);
  // Cancel also frees the gate, so a later intentional spin is accepted.
  const next = module.spin(root, ['A', 'B', 'C'], ['A', 'B', 'C'], false);
  module.cancel(root);
  assert.equal(await next, false);
});

test('a second click cannot start another animation while reels are running', async t => {
  const { module, root } = await environment(t);
  const first = module.spin(root, ['A', 'B', 'C'], ['C', 'B', 'A'], false);
  assert.equal(await module.spin(root, ['A', 'B', 'C'], ['A', 'B', 'C'], false), false);
  module.cancel(root);
  assert.equal(await first, false);
});

test('the next spin settles when balanced groups change the reel count from three to two', async t => {
  const { module, root } = await environment(t, { reduced: true });
  assert.equal(await module.spin(root, ['A', 'B', 'C', 'D', 'E'], ['E', 'A', 'B'], false), true);
  root.reels = root.reels.slice(0, 2);
  assert.equal(await module.spin(root, ['C', 'D'], ['D', 'C'], false), true);
  assert.deepEqual(root.reels.map(reel => reel.node.textContent), ['D', 'C']);
});

test('disposal removes the global space shortcut and settles an active spin', async t => {
  const { module, root, document } = await environment(t);
  const key = () => { const event = new Event('keydown', { cancelable: true }); event.code = 'Space'; return event; };
  document.dispatchEvent(key());
  assert.equal(root.button.clicks, 1);
  const spinning = module.spin(root, ['A', 'B', 'C'], ['C', 'B', 'A'], false);
  await module.dispose(root);
  assert.equal(await spinning, false);
  document.dispatchEvent(key());
  assert.equal(root.button.clicks, 1);
});

test('local round recovery is isolated by key and fails safely when storage is unavailable', async t => {
  const { module, localStorage } = await environment(t);
  module.saveSession('hold-a:user-a', '{"round":1}');
  module.saveSession('hold-b:user-a', '{"round":2}');
  assert.equal(module.loadSession('hold-a:user-a'), '{"round":1}');
  assert.equal(module.loadSession('hold-b:user-a'), '{"round":2}');
  assert.equal(module.loadSession('hold-a:user-b'), null);
  localStorage.setItem = () => { throw new Error('storage blocked'); };
  localStorage.getItem = () => { throw new Error('storage blocked'); };
  assert.doesNotThrow(() => module.saveSession('blocked', 'anything'));
  assert.equal(module.loadSession('blocked'), null);
});

test('clipboard failure is reported for the visible manual-copy fallback', async t => {
  const { module, clipboard } = await environment(t);
  assert.equal(await module.copy('Team A: Ada, Bo'), true);
  assert.equal(clipboard.text, 'Team A: Ada, Bo');
  clipboard.writeText = async () => { throw new Error('clipboard denied'); };
  assert.equal(await module.copy('Team B: Clara, Dan'), false);
});

test('finishing the same-tab presentation returns to the hold without closing the user tab', async t => {
  const { module } = await environment(t);
  let closed = 0, destination = null;
  window.close = () => { closed++; };
  window.location = { assign(value) { destination = value; } };
  module.closeOrReturn('/holds/hold-a?view=groups');
  assert.equal(closed, 0);
  assert.equal(destination, '/holds/hold-a?view=groups');
});

test('finishing an opened presentation closes it and retains a safe return fallback', async t => {
  const { module } = await environment(t);
  let closed = 0, destination = null;
  window.opener = { closed: false };
  window.closed = false;
  window.close = () => { closed++; window.closed = true; };
  window.location = { assign(value) { destination = value; } };
  module.closeOrReturn('/holds/hold-a?view=groups');
  assert.equal(closed, 1);
  await new Promise(resolve => setTimeout(resolve, 150));
  assert.equal(destination, null);
});
