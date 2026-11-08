import test from 'node:test';
import assert from 'node:assert/strict';
import * as graph from '../../web/wwwroot/js/notion-graph.js';

// Exercise the production canvas renderer with deterministic frames and its real D3 forces.
function fixture(t, payload = {}) {
  const previous = new Map();
  const install = (key, value) => { previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value }); };
  const frames = new Map(); let nextFrame = 1, now = 0;
  const storage = new Map();
  class Target {
    handlers = new Map(); style = {};
    addEventListener(name, callback, options) {
      this.handlers.set(name, callback);
      options?.signal?.addEventListener('abort', () => this.handlers.delete(name));
    }
    dispatch(name, value = {}) {
      const event = { button: 0, pointerId: 1, pointerType: 'mouse', clientX: 0, clientY: 0, target: this, preventDefault() {}, ...value };
      this.handlers.get(name)?.(event); return event;
    }
    remove() { this.removed = true; }
    setAttribute() {}
    getBoundingClientRect() { return { left: 0, top: 0, width: 800, height: 500 }; }
    setPointerCapture() {}
    focus() {}
  }
  const context = {
    font: '', globalAlpha: 1, fills: [], texts: [], strokes: [], shape: null,
    setTransform() {}, clearRect() { this.fills = []; this.texts = []; this.strokes = []; },
    beginPath() { this.shape = null; }, arc(x, y, radius) { this.shape = { x, y, radius, circle: true }; },
    roundRect() { this.shape = { box: true }; }, moveTo() {}, lineTo() {},
    fill() { if (this.shape?.circle) this.fills.push({ ...this.shape, alpha: this.globalAlpha, color: this.fillStyle }); },
    stroke() { this.strokes.push({ shape: this.shape, alpha: this.globalAlpha, color: this.strokeStyle }); },
    strokeText() {}, fillText(text, x, y) { this.texts.push({ text, x, y, font: this.font, alpha: this.globalAlpha }); },
    measureText(text) { return { width: String(text).length * Number(this.font.match(/([\d.]+)px/)?.[1] || 12) * 0.5 }; },
  };
  const canvas = new Target(); canvas.getContext = () => context;
  const stage = new Target(); stage.parentElement = null;
  const host = new Target(); host.clientWidth = 800; host.clientHeight = 500; host.closest = () => stage;
  host.replaceChildren = (...children) => { host.children = children; };
  const callbacks = [];
  const windowTarget = new Target();
  const media = new Target(); media.matches = true;
  Object.assign(windowTarget, {
    devicePixelRatio: 1, innerWidth: 800, innerHeight: 500,
    matchMedia: query => query.includes('reduced-motion') ? media : { matches: false, addEventListener() {} },
    localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
  });
  install('window', windowTarget);
  install('document', { createElement: name => name === 'canvas' ? canvas : new Target() });
  install('CSS', { supports: () => true });
  install('ResizeObserver', class { constructor(callback) { this.callback = callback; } observe() { this.callback(); } disconnect() {} });
  install('MutationObserver', class { observe() {} disconnect() {} });
  install('getComputedStyle', target => ({ color: ({ node: '#63728b', edge: '#9ba8bc', active: '#2452e8', label: '#17243b', surface: '#ffffff' })[target.style.color.match(/--graph-([a-z]+)/)?.[1]] || '#17243b' }));
  install('requestAnimationFrame', callback => { const id = nextFrame++; frames.set(id, callback); return id; });
  install('cancelAnimationFrame', id => frames.delete(id));
  const flush = () => {
    for (let count = 0; frames.size && count < 100; count++) {
      const pending = [...frames.values()]; frames.clear(); now += 32;
      for (const callback of pending) callback(now);
    }
    assert.equal(frames.size, 0, 'the renderer should settle and stop requesting frames');
  };
  graph.mount(host, { invokeMethodAsync: (...args) => { callbacks.push(args); return Promise.resolve(); } }, {
    nodes: [{ id: 'a', title: 'Klasser og interfaces' }, { id: 'b', title: 'Løs kobling' }, { id: 'c', title: 'Uafhængigt materiale' }],
    edges: [{ fromId: 'a', toId: 'b' }], ...payload,
  });
  flush();
  t.after(() => {
    graph.destroy(host);
    for (const [key, descriptor] of previous) if (descriptor) Object.defineProperty(globalThis, key, descriptor); else delete globalThis[key];
  });
  return { host, canvas, stage, context, callbacks, flush, storage };
}

test('persisted graph preferences are bounded and never contain graph data or selected references', t => {
  const f = fixture(t);
  const value = graph.saveSettings({ nodeSize: 99, labelSize: 0.01, labelWeight: 999, highlightConnections: false, dimUnrelated: false,
    activeId: 'private:reference', nodes: [{ title: 'Private title' }], unexpected: 'do not persist' });
  assert.equal(value.nodeSize, 3); assert.equal(value.labelSize, 0.6); assert.equal(value.labelWeight, 700);
  assert.equal(value.highlightConnections, false); assert.equal(value.dimUnrelated, false);
  assert.equal('activeId' in value, false); assert.equal('nodes' in value, false); assert.equal('unexpected' in value, false);
  assert.deepEqual(graph.readSettings(), value);
  assert.doesNotMatch([...f.storage.values()].join(''), /private|Private|unexpected/);
  const invalid = graph.saveSettings({ labelSize: NaN, labelWeight: Infinity, nodeSize: '2', showLabels: 'false', dimUnrelated: 0 });
  assert.equal(invalid.labelSize, 1); assert.equal(invalid.labelWeight, 500); assert.equal(invalid.nodeSize, 1);
  assert.equal(invalid.showLabels, true); assert.equal(invalid.dimUnrelated, true);
  f.storage.set('lup.connections.graph.v1', '{invalid');
  assert.equal(graph.readSettings().labelSize, 1);
  assert.equal(graph.resetSettings().labelWeight, 500); assert.equal(f.storage.size, 0);
});

test('text size and weight redraw measured labels without moving nodes or reheating physics', t => {
  const f = fixture(t, { options: { labelThreshold: 0.2 } });
  const before = f.context.fills.map(({ x, y, radius }) => ({ x, y, radius }));
  assert.ok(f.context.texts.some(label => label.font.startsWith('500 12px')));
  graph.configure(f.host, { labelSize: 1.5, labelWeight: 700 }); f.flush();
  assert.deepEqual(f.context.fills.map(({ x, y, radius }) => ({ x, y, radius })), before);
  assert.ok(f.context.texts.some(label => label.font.startsWith('700 18px')));
  assert.equal(f.context.texts.some(label => label.font.includes('12px')), false);
});

test('selected neighborhoods highlight while dimming can be independently disabled', t => {
  const f = fixture(t, { options: { activeId: 'a', labelThreshold: 0.2 } });
  assert.deepEqual(f.context.fills.map(point => Number(point.alpha.toFixed(2))), [1, 1, 0.21]);
  assert.ok(f.context.strokes.some(line => !line.shape && line.color === '#2452e8' && line.alpha > 0.8));
  graph.configure(f.host, { dimUnrelated: false }); f.flush();
  assert.deepEqual(f.context.fills.map(point => point.alpha), [1, 1, 1]);
  graph.configure(f.host, { highlightConnections: false, dimUnrelated: true }); f.flush();
  assert.deepEqual(f.context.fills.map(point => point.alpha), [1, 1, 1]);
  assert.equal(f.context.strokes.some(line => !line.shape && line.color === '#2452e8'), false);
});

test('resized visible labels select on one click and only a double click opens the canonical node', t => {
  const f = fixture(t, { nodes: [{ id: 'a', title: 'Interfaces' }], edges: [], options: { labelThreshold: 0.2 } });
  graph.configure(f.host, { labelSize: 2 }); f.flush();
  const title = f.context.texts.find(label => label.text === 'Interfaces');
  assert.match(title.font, /24px/);
  const at = { clientX: title.x, clientY: title.y };
  f.canvas.dispatch('pointerdown', at); f.canvas.dispatch('pointerup', at); f.flush();
  assert.equal(f.callbacks.filter(call => call[0] === 'OnGraphOpen').length, 0);
  assert.equal(f.callbacks.find(call => call[0] === 'OnGraphMenu')?.[1], 'a');
  f.canvas.dispatch('dblclick', at);
  assert.deepEqual(f.callbacks.filter(call => call[0] === 'OnGraphOpen'), [['OnGraphOpen', 'a']]);
  const positions = f.context.fills.map(({ x, y }) => ({ x, y }));
  graph.configure(f.host, { activeId: null }); graph.focus(f.host, ''); f.flush();
  assert.deepEqual(f.context.fills.map(({ x, y }) => ({ x, y })), positions);
});

test('dragging a node never navigates and keyboard selection keeps the details panel in sync', t => {
  const f = fixture(t);
  const first = f.context.fills[0];
  f.canvas.dispatch('pointerdown', { clientX: first.x, clientY: first.y });
  f.canvas.dispatch('pointermove', { clientX: first.x + 25, clientY: first.y + 20 });
  f.canvas.dispatch('pointerup', { clientX: first.x + 25, clientY: first.y + 20 }); f.flush();
  assert.equal(f.callbacks.length, 0);
  f.stage.dispatch('keydown', { key: 'n' }); f.flush();
  assert.equal(f.callbacks.find(call => call[0] === 'OnGraphMenu')?.[1], 'a');
  f.stage.dispatch('keydown', { key: 'Enter' });
  assert.deepEqual(f.callbacks.filter(call => call[0] === 'OnGraphOpen'), [['OnGraphOpen', 'a']]);
  f.stage.dispatch('keydown', { key: 'Escape' }); f.flush();
  assert.equal(f.callbacks.at(-1)[1], '');
});
