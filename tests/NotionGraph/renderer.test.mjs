import test from 'node:test';
import assert from 'node:assert/strict';
import * as graph from '../../web/wwwroot/js/notion-graph.js';

// Exercise the production canvas renderer with deterministic frames and its real D3 forces.
function fixture(t, payload = {}, { reducedMotion = true } = {}) {
  const previous = new Map();
  const install = (key, value) => { previous.set(key, Object.getOwnPropertyDescriptor(globalThis, key)); Object.defineProperty(globalThis, key, { configurable: true, writable: true, value }); };
  const frames = new Map(); let nextFrame = 1, now = 0;
  const storage = new Map();
  const imageRequests = [];
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
    font: '', globalAlpha: 1, fills: [], texts: [], strokes: [], images: [], shape: null,
    setTransform() {}, clearRect() { this.fills = []; this.texts = []; this.strokes = []; this.images = []; },
    beginPath() { this.shape = null; this.segments = []; }, arc(x, y, radius) { this.shape = { x, y, radius, circle: true }; },
    roundRect() { this.shape = { box: true }; }, moveTo(x, y) { this.segments.push([x, y]); }, lineTo(x, y) { this.segments.push([x, y]); },
    fill() { if (this.shape?.circle) this.fills.push({ ...this.shape, alpha: this.globalAlpha, color: this.fillStyle }); },
    stroke() { this.strokes.push({ shape: this.shape, alpha: this.globalAlpha, color: this.strokeStyle, segments: this.segments }); },
    strokeText() {}, fillText(text, x, y) { this.texts.push({ text, x, y, font: this.font, alpha: this.globalAlpha }); },
    drawImage(image, x, y, width, height) { this.images.push({ url: image.url, x, y, width, height }); },
    measureText(text) { return { width: String(text).length * Number(this.font.match(/([\d.]+)px/)?.[1] || 12) * 0.5 }; },
  };
  const canvas = new Target(); canvas.getContext = () => context;
  const stage = new Target(); stage.parentElement = null;
  const host = new Target(); host.clientWidth = 800; host.clientHeight = 500; host.closest = () => stage;
  host.replaceChildren = (...children) => { host.children = children; };
  const callbacks = [];
  const windowTarget = new Target();
  const media = new Target(); media.matches = reducedMotion;
  Object.assign(windowTarget, {
    devicePixelRatio: 1, innerWidth: 800, innerHeight: 500,
    matchMedia: query => query.includes('reduced-motion') ? media : { matches: false, addEventListener() {} },
    localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value), removeItem: key => storage.delete(key) },
  });
  install('window', windowTarget);
  install('document', { createElement: name => name === 'canvas' ? canvas : new Target() });
  install('Image', class {
    set src(value) { this.url = value; imageRequests.push(this); }
    load(width, height) { this.naturalWidth = width; this.naturalHeight = height; this.complete = true; this.onload(); }
    fail() { this.onerror(); }
  });
  install('CSS', { supports: () => true });
  install('ResizeObserver', class { constructor(callback) { this.callback = callback; } observe() { this.callback(); } disconnect() {} });
  install('MutationObserver', class { observe() {} disconnect() {} });
  install('getComputedStyle', target => ({ color: ({ node: '#63728b', edge: '#9ba8bc', active: '#2452e8', label: '#17243b', surface: '#ffffff' })[target.style.color.match(/--graph-([a-z]+)/)?.[1]] || '#17243b' }));
  install('requestAnimationFrame', callback => { const id = nextFrame++; frames.set(id, callback); return id; });
  install('cancelAnimationFrame', id => frames.delete(id));
  const advance = (limit) => {
    for (let count = 0; frames.size && count < limit; count++) {
      const pending = [...frames.values()]; frames.clear(); now += 32;
      for (const callback of pending) callback(now);
    }
  };
  const flush = () => {
    advance(180);
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
  return { host, canvas, stage, context, callbacks, advance, flush, storage, imageRequests };
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

test('graph titles and tooltips render custom images inline and keep their title area clickable', t => {
  const url = '/images/characters/chameleon/reactions/coffee.webp';
  const icon = { text: ':chameleon_coffee:', imageUrl: url, alt: 'Kamæleon med kaffe' };
  const titleParts = [icon, { text: ' Machine ' }, icon, { text: ' Learning 😀' }];
  const f = fixture(t, { nodes: [{ id: 'a', title: ':chameleon_coffee: Machine :chameleon_coffee: Learning 😀', titleParts }], edges: [] });
  const [{ x, y, radius }] = f.context.fills;
  assert.equal(f.imageRequests.length, 1);
  assert.equal(f.context.images.length, 0);
  f.imageRequests[0].load(80, 40); f.flush();
  assert.deepEqual(f.context.fills.map(point => [point.x, point.y, point.radius]), [[x, y, radius]]);
  assert.deepEqual(f.context.texts.map(label => label.text), [' Machine ', ' Learning 😀']);
  const [first, second] = f.context.images;
  assert.equal(first.url, url);
  assert.equal(first.width, 12 * 1.2);
  assert.equal(first.height, 6 * 1.2);
  assert.ok(first.x < f.context.texts[0].x && second.x < f.context.texts[1].x);
  f.canvas.dispatch('dblclick', { clientX: first.x + first.width / 2, clientY: first.y + first.height / 2 });
  assert.deepEqual(f.callbacks.filter(call => call[0] === 'OnGraphOpen'), [['OnGraphOpen', 'a']]);

  f.canvas.dispatch('pointermove', { clientX: x, clientY: y }); f.flush();
  assert.deepEqual(f.context.images.map(image => Number(image.width.toFixed(1))), [15.6, 15.6]);
  assert.ok(f.context.texts.some(label => label.text === '0 indgående · 0 udgående'));
  assert.equal(f.imageRequests.length, 1);
  f.stage.dispatch('keydown', { key: 'n' }); f.flush();
  assert.equal(f.host.children[1].textContent, 'Kamæleon med kaffe Machine Kamæleon med kaffe Learning 😀');
});

test('failed emoji images fall back to text and request the catalog refresh only once per graph', t => {
  const titleParts = [
    { text: ':one:', imageUrl: '/api/emojis/one/content?ticket=old' }, { text: ' ' },
    { text: ':two:', imageUrl: '/api/emojis/two/content?ticket=old' }, { text: ' :ukendt:' },
  ];
  const nodes = [{ id: 'a', title: ':one: :two: :ukendt:', titleParts }];
  const f = fixture(t, { nodes, edges: [] });
  assert.equal(f.imageRequests.length, 2);
  for (const image of f.imageRequests) image.fail();
  f.flush();
  assert.equal(f.context.texts.map(label => label.text).join(''), ':one: :two: :ukendt:');
  assert.deepEqual(f.callbacks, [['OnGraphEmojiError']]);

  titleParts[0] = { ...titleParts[0], imageUrl: '/api/emojis/one/content?ticket=fresh' };
  graph.update(f.host, { nodes, edges: [] }); f.flush();
  assert.equal(f.imageRequests.length, 3);
  f.imageRequests[2].fail(); f.flush();
  assert.deepEqual(f.callbacks, [['OnGraphEmojiError']]);
});

test('titles stay readable near 0.2 zoom by default and respect a higher saved threshold', t => {
  const f = fixture(t, { nodes: [{ id: 'a', title: 'Klasser og interfaces' }, { id: 'b', title: 'Løs kobling' }] });
  assert.equal(graph.readSettings().labelThreshold, 0.2);
  // Nine 0.8× zoom steps take this graph from 1.45× to about 0.195×.
  for (let step = 0; step < 9; step++) graph.zoom(f.host, -1);
  f.flush();
  assert.equal(f.context.texts.length, 2);
  const above = f.context.texts.find(label => label.text === 'Klasser og interfaces');
  const below = f.context.texts.find(label => label.text === 'Løs kobling');
  assert.ok(above.y < f.context.fills[0].y && above.alpha > 0.5);
  assert.ok(below.y > f.context.fills[1].y && below.alpha > 0.5);
  assert.ok(below.y - above.y >= 16);
  f.canvas.dispatch('dblclick', { clientX: above.x, clientY: above.y });
  assert.deepEqual(f.callbacks.filter(call => call[0] === 'OnGraphOpen'), [['OnGraphOpen', 'a']]);

  graph.saveSettings({ labelThreshold: 0.8 });
  graph.configure(f.host, graph.readSettings()); f.flush();
  assert.equal(f.context.texts.length, 0);

  graph.configure(f.host, graph.resetSettings()); f.flush();
  assert.equal(f.context.texts.length, 2);
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

test('with reduced motion only directly linked nodes follow the drag and the edge tracks both endpoints', t => {
  const f = fixture(t);
  const positions = () => f.context.fills.map(({ x, y }) => [x, y]);
  const [first, second, unrelated] = positions();
  f.canvas.dispatch('pointerdown', { clientX: first[0], clientY: first[1] });
  const target = { clientX: first[0] + 80, clientY: first[1] - 40 };
  f.canvas.dispatch('pointermove', target);
  f.flush();

  const [dragged, neighbor, other] = positions();
  assert.ok(Math.hypot(dragged[0] - target.clientX, dragged[1] - target.clientY) < 0.01);
  assert.ok(neighbor[0] - second[0] > 10 && neighbor[1] < second[1]);
  assert.deepEqual(other, unrelated);
  const edge = f.context.strokes.find(stroke => !stroke.shape);
  assert.deepEqual(edge?.segments, [dragged, neighbor]);

  f.canvas.dispatch('pointercancel', target);
  f.flush();
  assert.deepEqual(positions(), [dragged, neighbor, unrelated]);
  assert.deepEqual(f.callbacks, []);

  graph.configure(f.host, { linkForce: 0 }); f.flush();
  const [newOrigin, fixedNeighbor] = positions();
  const nextTarget = { clientX: newOrigin[0] - 40, clientY: newOrigin[1] + 20 };
  f.canvas.dispatch('pointerdown', { clientX: newOrigin[0], clientY: newOrigin[1] });
  f.canvas.dispatch('pointermove', nextTarget);
  f.canvas.dispatch('pointerup', nextTarget);
  f.flush();
  const [nextDragged, nextNeighbor] = positions();
  assert.ok(Math.hypot(nextDragged[0] - nextTarget.clientX, nextDragged[1] - nextTarget.clientY) < 0.01);
  assert.deepEqual(nextNeighbor, fixedNeighbor);
  assert.deepEqual(f.callbacks, []);
});

test('dragging a leaf visibly pulls a connected hub on the first frame with normal motion', t => {
  const f = fixture(t, {
    nodes: Array.from({ length: 100 }, (_, i) => ({ id: String(i), title: `Punkt ${i}`,
      color: i === 0 ? '#c2314e' : i === 1 ? '#3a6ae9' : '#666' })),
    edges: Array.from({ length: 99 }, (_, i) => ({ fromId: '0', toId: String(i + 1) })),
    options: { showLabels: false },
  }, { reducedMotion: false });
  const hub = () => f.context.fills.find(point => point.color === '#c2314e');
  const original = hub();
  const leaf = f.context.fills.find(point => point.color === '#3a6ae9');
  const target = { clientX: leaf.x + 140, clientY: leaf.y + 100 };
  f.canvas.dispatch('pointerdown', { clientX: leaf.x, clientY: leaf.y });
  f.canvas.dispatch('pointermove', target);
  f.advance(1);
  assert.ok(hub().x - original.x > 20 && hub().y - original.y > 10);

  f.canvas.dispatch('pointerup', target);
  f.flush();
  assert.deepEqual(f.callbacks, []);
});
