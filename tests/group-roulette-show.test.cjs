const test = require('node:test');
const assert = require('node:assert/strict');
const { readFile } = require('node:fs/promises');
const { resolve } = require('node:path');

// Run the production browser module against a small DOM/audio adapter and a
// virtual frame clock. Assertions observe movement, events and playback rather
// than searching implementation strings or waiting several seconds per spin.
async function environment(t, { reduced = false, audio = false, audioBlocked = false, speech = false, speechHangs = false, settings = {} } = {}) {
  const originals = new Map();
  const install = (name, value) => {
    originals.set(name, Object.getOwnPropertyDescriptor(globalThis, name));
    Object.defineProperty(globalThis, name, { value, writable: true, configurable: true });
  };
  let now = 0, nextId = 1;
  const timers = new Map();
  const clock = {
    set(action, delay = 0) { const id = nextId++; timers.set(id, { action, at: now + delay }); return id; },
    clear(id) { timers.delete(id); },
    async advance(milliseconds) {
      const until = now + milliseconds;
      for (let iteration = 0; iteration < 10000; iteration++) {
        const task = [...timers].filter(([, entry]) => entry.at <= until).sort((a, b) => a[1].at - b[1].at)[0];
        if (!task) break;
        timers.delete(task[0]); now = task[1].at; task[1].action();
        await Promise.resolve(); await Promise.resolve();
      }
      now = until; await Promise.resolve(); await Promise.resolve();
    },
    async finish(promise, limit = 16000) {
      let settled = false, result, error;
      promise.then(value => { settled = true; result = value; }, reason => { settled = true; error = reason; });
      for (let elapsed = 0; !settled && elapsed < limit; elapsed += 20) await this.advance(20);
      assert.ok(settled, 'animation/playback promise must settle within its bounded lifetime');
      if (error) throw error;
      return result;
    },
    count: () => timers.size
  };
  class Element extends EventTarget {
    constructor(tagName = 'div', className = '') {
      super(); this.tagName = tagName; this.children = []; this.parentElement = null; this.attributes = new Map();
      this._classes = new Set(); this.className = className; this._text = ''; this.writes = 0; this.disabled = false;
      this.isConnected = true; this.clientWidth = 190; this.clientHeight = 110;
      this.style = { values: new Map(), setProperty(name, value) { this.values.set(name, value); }, getPropertyValue(name) { return this.values.get(name); } };
      this.classList = { add: (...values) => values.forEach(value => this._classes.add(value)), remove: (...values) => values.forEach(value => this._classes.delete(value)), contains: value => this._classes.has(value) };
    }
    set className(value) { this._classes = new Set(value.split(/\s+/).filter(Boolean)); }
    get className() { return [...this._classes].join(' '); }
    set textContent(value) { this._text = String(value); this.children = []; this.writes++; }
    get textContent() { return this.children.length ? this.children.map(child => child.textContent).join('') : this._text; }
    setAttribute(name, value) { this.attributes.set(name, value); }
    matches(selector) {
      return selector.split(',').some(part => {
        const value = part.trim();
        if (value.startsWith('.')) return this._classes.has(value.slice(1));
        if (value.startsWith('[')) return this.attributes.has(value.slice(1, -1).split('=')[0]);
        return this.tagName === value;
      });
    }
    closest(selector) { return this.matches(selector) ? this : this.parentElement?.closest(selector) ?? null; }
    appendChild(node) { node.parentElement = this; this.children.push(node); return node; }
    remove() { if (this.parentElement) this.parentElement.children = this.parentElement.children.filter(child => child !== this); this.parentElement = null; this.isConnected = false; }
    querySelectorAll(selector) { return this.children.flatMap(child => [...(child.matches(selector) ? [child] : []), ...child.querySelectorAll(selector)]); }
    querySelector(selector) { return this.querySelectorAll(selector)[0] ?? null; }
    focus() { document.activeElement = this; }
    click() { this.clicks = (this.clicks || 0) + 1; }
    setPointerCapture() {}
  }
  class Document extends EventTarget {
    constructor() { super(); this.body = new Element('body'); this.body.style.overflow = ''; this.activeElement = null; }
    createElement(tag) { return new Element(tag); }
    closest() { return null; }
  }
  const document = new Document();
  const audioContexts = [];
  class AudioContext {
    constructor() { this.state = 'suspended'; this.destination = {}; this.oscillators = []; this.sources = []; this.gains = []; audioContexts.push(this); }
    get currentTime() { return now / 1000; }
    async resume() { if (audioBlocked) return await new Promise(() => {}); this.state = 'running'; }
    async close() { this.state = 'closed'; }
    createGain() {
      const gain = { gain: { value: 1, setValueAtTime(value) { this.value = value; }, exponentialRampToValueAtTime() {} }, connect() {}, disconnect() {} };
      this.gains.push(gain); return gain;
    }
    createOscillator() {
      const oscillator = this.node(); oscillator.frequency = { value: 0 };
      oscillator.start = () => { oscillator.started = true; }; this.oscillators.push(oscillator); return oscillator;
    }
    createBufferSource() {
      const source = this.node(); source.start = () => { source.started = true; source.timer = clock.set(() => source.onended?.(), source.buffer.duration * 1000); };
      this.sources.push(source); return source;
    }
    node() {
      return { connect() {}, disconnect() {}, stop(at) { clock.clear(this.timer); if (at === undefined) this.onended?.(); else this.timer = clock.set(() => this.onended?.(), Math.max(0, at * 1000 - now)); } };
    }
    async decodeAudioData(bytes) { this.decodedBytes = bytes.byteLength; return { duration: .6 }; }
  }
  class SpeechEngine extends EventTarget {
    constructor() {
      super(); this.hangs = speechHangs; this.utterances = []; this.cancelled = 0;
      this.voices = [{ name: 'Online Danish', lang: 'da-DK', localService: false },
        { name: 'Local English', lang: 'en-US', localService: true }, { name: 'Local Danish', lang: 'da-DK', localService: true }];
    }
    getVoices() { return this.voices; }
    resume() {}
    speak(phrase) { this.utterances.push(phrase); if (!this.hangs) this.timer = clock.set(() => phrase.onend?.(), 200); }
    cancel() { this.cancelled++; clock.clear(this.timer); }
  }
  const speechEngine = new SpeechEngine();
  class Utterance { constructor(text) { this.text = text; } }
  const values = new Map(), localStorage = { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value) };
  const clipboard = { text: null, async writeText(value) { this.text = value; } };
  install('document', document);
  install('window', { innerWidth: 1200, innerHeight: 800, matchMedia: () => ({ matches: reduced }), ...(audio ? { AudioContext } : {}),
    ...(speech ? { speechSynthesis: speechEngine, SpeechSynthesisUtterance: Utterance } : {}) });
  install('navigator', { clipboard }); install('localStorage', localStorage); install('performance', { now: () => now });
  install('setTimeout', (action, delay) => clock.set(action, delay)); install('clearTimeout', id => clock.clear(id));
  install('cancelAnimationFrame', id => clock.clear(id)); install('requestAnimationFrame', callback => clock.set(() => callback(now), 16));
  const source = await readFile(resolve(__dirname, '../web/wwwroot/js/group-roulette-show.js'), 'utf8');
  const module = await import(`data:text/javascript;base64,${Buffer.from(source + `\n// test instance ${Math.random()}`).toString('base64')}`);
  let canvasCalls = 0;
  const root = new Element('div', 'roulette-app');
  root.shell = root.appendChild(new Element('div', 'roulette-shell')); root.shell.inert = false;
  root.button = root.shell.appendChild(new Element('button', 'roulette-spin'));
  root.lever = root.shell.appendChild(new Element('button', 'roulette-lever'));
  const stage = root.shell.appendChild(new Element('div', 'roulette-reels'));
  const canvas = root.appendChild(new Element('canvas', 'roulette-confetti'));
  canvas.getContext = () => { canvasCalls++; return { setTransform() {}, clearRect() {}, save() {}, translate() {}, rotate() {}, fillRect() {}, restore() {} }; };
  root.setReels = count => {
    stage.children = []; root.reels = Array.from({ length: count }, () => {
      const reel = stage.appendChild(new Element('div', 'roulette-reel'));
      reel.viewport = reel.appendChild(new Element('div', 'roulette-reel-window'));
      reel.fallback = reel.viewport.appendChild(new Element('strong', 'roulette-reel-name')); reel.fallback.textContent = 'Klar?';
      return reel;
    });
  };
  root.setReels(3);
  const callbacks = [];
  module.initialize(root, { invokeMethodAsync: async method => { callbacks.push(method); } }, settings);
  const fire = (target, type, properties = {}, dispatchOn = root) => {
    const event = new Event(type, { cancelable: true });
    Object.defineProperty(event, 'target', { value: target });
    Object.assign(event, properties); dispatchOn.dispatchEvent(event); return event;
  };
  const visibleNames = () => root.reels.map(reel => reel.querySelector('.roulette-reel-strip')?.children[2].textContent);
  t.after(async () => {
    await module.dispose(root);
    for (const [name, descriptor] of originals) {
      if (descriptor) Object.defineProperty(globalThis, name, descriptor); else delete globalThis[name];
    }
  });
  return { module, root, document, clock, fire, callbacks, visibleNames, audioContexts, speechEngine, localStorage, clipboard, canvasCalls: () => canvasCalls };
}

test('names visibly travel through independent vertical strips while Blazor fallback text remains untouched', async t => {
  const { module, root, clock, visibleNames } = await environment(t);
  const spin = module.spin(root, ['Ada', 'Bilal', 'Clara', 'Dan'], ['Clara', 'Bilal', 'Ada'], false, { durationMs: 2200 });
  await clock.advance(100);
  const strip = root.reels[0].querySelector('.roulette-reel-strip');
  assert.equal(strip.children.length, 5);
  const firstPosition = strip.style.getPropertyValue('--roll-y');
  assert.ok(root.reels[0].classList.contains('is-rolling'));
  // A component render during the spin can update its own fallback without
  // replacing or freezing the live JS-owned strip.
  root.reels.forEach(reel => { reel.fallback.textContent = '…'; });
  await clock.advance(80);
  assert.notEqual(strip.style.getPropertyValue('--roll-y'), firstPosition);
  assert.equal(await clock.finish(spin), true);
  assert.deepEqual(visibleNames(), ['Clara', 'Bilal', 'Ada']);
  assert.deepEqual(root.reels.map(reel => reel.fallback.textContent), ['…', '…', '…']);
  assert.ok(root.reels.every(reel => reel.classList.contains('is-locked')));
});

test('reels decelerate and lock one at a time rather than revealing the whole group immediately', async t => {
  const { module, root, clock } = await environment(t);
  const spinning = module.spin(root, ['A', 'B', 'C', 'D'], ['C', 'B', 'A'], false, { durationMs: 3000 });
  await clock.advance(2250);
  assert.deepEqual(root.reels.map(reel => reel.classList.contains('is-locked')), [true, false, false]);
  await clock.advance(420);
  assert.deepEqual(root.reels.map(reel => reel.classList.contains('is-locked')), [true, true, false]);
  assert.equal(await clock.finish(spinning), true);
});

test('reel names fit one centered fixed-height line and retain complete long names for the reveal', async t => {
  const { module, root, clock, visibleNames } = await environment(t, { reduced: true });
  const names = ['QA-elev 2', 'Alexandrine Frederiksen', 'Et usædvanligt langt elevnavn med mange mellemnavne'];
  assert.equal(await clock.finish(module.spin(root, names, names, false)), true);
  assert.deepEqual(visibleNames(), names);
  const fonts = root.reels.map(reel => {
    const row = reel.querySelector('.roulette-reel-strip').children[2];
    assert.equal(row.style.lineHeight, row.style.height, 'vertical centering must use exactly the fixed slot height');
    return parseFloat(row.style.getPropertyValue('--fit-font'));
  });
  assert.ok(fonts[0] > fonts[1], 'longer names receive a smaller font');
  assert.ok(fonts[1] >= fonts[2]);
  assert.ok(fonts[2] >= 13, 'extreme names keep a legible minimum size and use visual ellipsis');
});

test('reduced motion reveals chosen names without cycling or confetti', async t => {
  const { module, root, clock, visibleNames, canvasCalls } = await environment(t, { reduced: true });
  assert.equal(await clock.finish(module.spin(root, ['A', 'B', 'C', 'D'], ['Clara', 'Bilal', 'Asta'], false, { groupName: 'Testholdet' })), true);
  assert.deepEqual(visibleNames(), ['Clara', 'Bilal', 'Asta']);
  assert.ok(root.reels.every(reel => !reel.classList.contains('is-rolling')));
  assert.equal(canvasCalls(), 0);
  assert.equal(root.querySelectorAll('.roulette-celebration').length, 0);
});

test('an explicit classroom animation choice overrides reduced-motion preference for reels and celebrations', async t => {
  const { module, root, clock, visibleNames, canvasCalls } = await environment(t, { reduced: true });
  const spinning = module.spin(root, ['Ada', 'Bo', 'Clara'], ['Clara', 'Bo', 'Ada'], false,
    { motion: true, durationMs: 800, groupName: 'De rullende navne' });
  await clock.advance(100);
  const strip = root.reels[0].querySelector('.roulette-reel-strip');
  const before = strip.style.getPropertyValue('--roll-y');
  assert.ok(root.reels[0].classList.contains('is-rolling'));
  await clock.advance(80);
  assert.notEqual(strip.style.getPropertyValue('--roll-y'), before);
  assert.equal(await clock.finish(spinning), true);
  assert.deepEqual(visibleNames(), ['Clara', 'Bo', 'Ada']);
  assert.ok(root.classList.contains('roulette-motion-enabled'));
  assert.ok(canvasCalls() > 0);
  assert.equal(root.querySelectorAll('.roulette-celebration').length, 1);
});

test('an explicit quiet classroom choice disables animations even without reduced-motion preference', async t => {
  const { module, root, clock, visibleNames, canvasCalls } = await environment(t);
  assert.equal(await clock.finish(module.spin(root, ['Ada', 'Bo', 'Clara'], ['Clara', 'Bo', 'Ada'], false,
    { motion: false, groupName: 'Stille navne' })), true);
  assert.deepEqual(visibleNames(), ['Clara', 'Bo', 'Ada']);
  assert.ok(root.reels.every(reel => !reel.classList.contains('is-rolling')));
  assert.ok(root.classList.contains('roulette-motion-disabled'));
  assert.equal(canvasCalls(), 0);
  assert.equal(root.querySelectorAll('.roulette-celebration').length, 0);
});

test('cancellation settles a mounted spin, removes strips and stops further frame work', async t => {
  const { module, root, clock } = await environment(t);
  const spinning = module.spin(root, ['A', 'B', 'C'], ['C', 'B', 'A'], false);
  await clock.advance(120); module.cancel(root);
  assert.equal(await spinning, false);
  assert.equal(root.querySelectorAll('.roulette-reel-strip').length, 0);
  assert.equal(clock.count(), 0);
  await clock.advance(5000);
  assert.deepEqual(root.reels.map(reel => reel.fallback.textContent), ['Klar?', 'Klar?', 'Klar?']);
});

test('cancel before first frame, invalid markup and disconnected roots always settle safely', async t => {
  const { module, root, clock } = await environment(t);
  const early = module.spin(root, ['A'], ['A', 'A', 'A'], false); module.cancel(root);
  assert.equal(await early, false);
  root.reels[0].viewport.remove();
  assert.equal(await clock.finish(module.spin(root, ['A'], ['A', 'A', 'A'], false)), false);
  root.setReels(3);
  const disconnected = module.spin(root, ['A'], ['A', 'A', 'A'], false);
  await clock.advance(100); root.isConnected = false;
  assert.equal(await clock.finish(disconnected), false);
});

test('new draw reads the current reel nodes when balanced groups change from three pupils to two', async t => {
  const { module, root, clock, visibleNames } = await environment(t);
  assert.equal(await clock.finish(module.spin(root, ['A', 'B', 'C', 'D', 'E'], ['E', 'A', 'B'], false, { durationMs: 800 })), true);
  const oldReels = root.reels; root.setReels(2);
  assert.equal(await clock.finish(module.spin(root, ['C', 'D'], ['D', 'C'], false, { durationMs: 800 })), true);
  assert.deepEqual(visibleNames(), ['D', 'C']);
  assert.ok(root.reels.every(reel => !oldReels.includes(reel)));
});

test('second click cannot start concurrent reel animation', async t => {
  const { module, root } = await environment(t);
  const first = module.spin(root, ['A', 'B', 'C'], ['C', 'B', 'A'], false);
  assert.equal(await module.spin(root, ['A', 'B', 'C'], ['A', 'B', 'C'], false), false);
  module.cancel(root); assert.equal(await first, false);
});

test('lever drag invokes one draw and suppresses its following click, while Space respects inputs and disabled controls', async t => {
  const { module, root, fire, callbacks, document } = await environment(t);
  fire(root.lever, 'pointerdown', { pointerId: 4, clientY: 100 });
  fire(root.lever, 'pointermove', { pointerId: 4, clientY: 155 });
  fire(root.lever, 'pointerup', { pointerId: 4, clientY: 155 });
  assert.deepEqual(callbacks, ['SpinFromLever']);
  assert.ok(fire(root.lever, 'click').defaultPrevented);
  fire(document.body, 'keydown', { code: 'Space' }, document); assert.equal(root.button.clicks, 1);
  root.button.disabled = true;
  fire(document.body, 'keydown', { code: 'Space' }, document); assert.equal(root.button.clicks, 1);
  root.button.disabled = false;
  fire({ closest: () => true }, 'keydown', { code: 'Space' }, document); assert.equal(root.button.clicks, 1);
  await module.dispose(root);
  fire(document.body, 'keydown', { code: 'Space' }, document); assert.equal(root.button.clicks, 1);
});

test('trusted pointer activation unlocks audio before interop; opt-in spin emits lever ticks locks and win notes', async t => {
  const { module, root, fire, clock, audioContexts } = await environment(t, { audio: true });
  fire(root.button, 'pointerdown');
  assert.equal(audioContexts.length, 1); assert.equal(audioContexts[0].state, 'running');
  assert.equal(audioContexts[0].oscillators.length, 0);
  assert.equal(await clock.finish(module.spin(root, ['A', 'B', 'C'], ['C', 'B', 'A'], true, { durationMs: 800, volume: .6 })), true);
  const notes = audioContexts[0].oscillators.filter(node => node.started).map(node => node.frequency.value);
  assert.ok(notes.includes(120)); assert.ok(notes.includes(70)); assert.ok(notes.includes(230)); assert.ok(notes.includes(1046.5));
  await module.setSound(root, false);
  assert.equal(clock.count(), 1, 'only the current confetti frame remains after effects are muted');
});

test('muted spins stay silent even after browser audio is unlocked', async t => {
  const { module, root, fire, clock, audioContexts } = await environment(t, { audio: true });
  fire(root.button, 'pointerdown');
  assert.equal(await clock.finish(module.spin(root, ['A', 'B', 'C'], ['A', 'B', 'C'], false, { durationMs: 800 })), true);
  assert.equal(audioContexts[0].oscillators.length, 0);
});

test('initial settings are quiet until a user gesture and report actual browser audio readiness', async t => {
  const { module, root, fire, audioContexts } = await environment(t, { audio: true, settings: { sound: true, volume: .4 } });
  assert.equal(audioContexts.length, 0);
  assert.equal(module.capabilities(root).sound, true); assert.equal(module.capabilities(root).audioReady, false);
  root.button.setAttribute('data-roulette-auto', ''); fire(root.button, 'pointerdown');
  assert.equal(module.capabilities(root).audioReady, true);
  assert.equal(await module.setSound(root, false), true); assert.equal(module.capabilities(root).sound, false);
});

test('browser autoplay denial has a bounded timeout and does not freeze the draw or claim enabled audio', async t => {
  const { module, root, clock } = await environment(t, { audio: true, audioBlocked: true });
  assert.equal(await clock.finish(module.setSound(root, true)), false);
  assert.equal(module.capabilities(root).sound, false);
  assert.equal(await clock.finish(module.spin(root, ['A', 'B', 'C'], ['A', 'B', 'C'], true, { durationMs: 800 })), true);
  assert.equal(module.capabilities(root).audioReady, false);
});

test('local WAV speech can be preloaded, played with volume and cancelled without holding up the next group', async t => {
  const { module, root, clock, fire, audioContexts } = await environment(t, { audio: true });
  fire(root.button, 'pointerdown');
  const wav = Buffer.from('RIFF-test-wave').toString('base64');
  assert.equal(await module.prepareSpeech(root, wav), true);
  const playing = module.playSpeech(root, wav, .8); await clock.advance(20);
  const audio = audioContexts[0];
  assert.equal(audio.sources.length, 1); assert.ok(audio.sources[0].started);
  assert.equal(audio.gains.at(-1).gain.value, .8);
  module.setVolume(root, .5); assert.equal(audio.gains.at(-1).gain.value, .5);
  module.cancelSpeech(root); assert.equal(await playing, false);
  assert.equal(clock.count(), 0);
  assert.equal(await clock.finish(module.playSpeech(root, wav, .8)), true);
  assert.equal(audio.sources.length, 2);
});

test('disposal stops speech and active animation and closes the audio graph', async t => {
  const { module, root, clock, fire, audioContexts } = await environment(t, { audio: true });
  fire(root.button, 'pointerdown');
  const playing = module.playSpeech(root, Buffer.from('RIFF').toString('base64'));
  await clock.advance(20); await module.dispose(root);
  assert.equal(await playing, false); assert.equal(audioContexts[0].state, 'closed'); assert.equal(clock.count(), 0);
});

test('late audio decoding after account cancellation cannot restore a stale prepared voice clip', async t => {
  const { module, root, fire, audioContexts } = await environment(t, { audio: true });
  fire(root.button, 'pointerdown');
  let decoded;
  audioContexts[0].decodeAudioData = () => new Promise(resolve => { decoded = resolve; });
  const preparing = module.prepareSpeech(root, Buffer.from('RIFF').toString('base64'));
  module.cancel(root); decoded({ duration: .6 });
  assert.equal(await preparing, false);
});

test('browser speech chooses a local Danish voice and reads CamelCase team names as individual words', async t => {
  const { module, root, clock, speechEngine } = await environment(t, { speech: true });
  assert.equal(module.capabilities(root).speechAvailable, true);
  assert.equal(await clock.finish(module.playBrowserSpeech(root, 'DeGyldneKodeNinjaer', .8)), true);
  const phrase = speechEngine.utterances[0];
  assert.equal(phrase.text, 'De Gyldne Kode Ninjaer'); assert.equal(phrase.voice.name, 'Local Danish');
  assert.equal(phrase.voice.localService, true); assert.equal(phrase.volume, .8);
});

test('browser speech waits for the voice inventory and refuses a remote-only voice', async t => {
  const { module, root, clock, speechEngine } = await environment(t, { speech: true });
  const local = speechEngine.voices[2]; speechEngine.voices = [];
  const speaking = module.playBrowserSpeech(root, 'Kodeholdet');
  await clock.advance(200); assert.equal(speechEngine.utterances.length, 0);
  speechEngine.voices = [local]; speechEngine.dispatchEvent(new Event('voiceschanged'));
  assert.equal(await clock.finish(speaking), true);
  speechEngine.voices = [{ name: 'Cloud voice', lang: 'da-DK', localService: false }];
  assert.equal(await module.playBrowserSpeech(root, 'Et andet hold'), false);
  assert.equal(speechEngine.utterances.length, 1);
});

test('hanging or cancelled browser speech settles and cannot block automatic progression', async t => {
  const { module, root, clock, speechEngine } = await environment(t, { speech: true, speechHangs: true });
  assert.equal(await clock.finish(module.playBrowserSpeech(root, 'Kodeholdet')), false);
  assert.equal(speechEngine.cancelled, 1);
  const cancelled = module.playBrowserSpeech(root, 'Næste hold'); module.cancelSpeech(root);
  assert.equal(await cancelled, false);
  speechEngine.hangs = false;
  assert.equal(await clock.finish(module.playBrowserSpeech(root, 'Nu virker det')), true);
});

test('twelve distinct celebrations reveal only after the final reel and clean up between draws', async t => {
  const { module, root, clock } = await environment(t);
  const variants = new Set();
  for (let index = 0; index < 12; index++) {
    const spinning = module.spin(root, ['A', 'B', 'C'], ['A', 'B', 'C'], false,
      { durationMs: 800, celebrationIndex: index, groupName: '<Et sikkert holdnavn>', celebrationText: 'Sikke et hold!' });
    await clock.advance(100);
    assert.equal(root.querySelectorAll('.roulette-celebration').length, 0);
    assert.equal(await clock.finish(spinning), true);
    const overlay = root.querySelector('.roulette-celebration'); assert.ok(overlay);
    assert.equal(root.querySelectorAll('.roulette-celebration').length, 1);
    assert.equal(overlay.querySelector('.roulette-celebration-title').textContent, '<Et sikkert holdnavn>');
    variants.add(overlay.className);
  }
  assert.equal(variants.size, 12);
  await clock.advance(3300); assert.equal(root.querySelectorAll('.roulette-celebration').length, 0);
});

test('local round recovery stays isolated by account and hold and fails safely with unavailable storage', async t => {
  const { module, localStorage } = await environment(t);
  module.saveSession('hold-a:user-a', '{"round":1}'); module.saveSession('hold-b:user-a', '{"round":2}');
  assert.equal(module.loadSession('hold-a:user-a'), '{"round":1}'); assert.equal(module.loadSession('hold-b:user-a'), '{"round":2}');
  assert.equal(module.loadSession('hold-a:user-b'), null);
  localStorage.setItem = localStorage.getItem = () => { throw new Error('storage blocked'); };
  assert.doesNotThrow(() => module.saveSession('blocked', 'anything')); assert.equal(module.loadSession('blocked'), null);
});

test('clipboard errors are reported for the visible manual-copy fallback', async t => {
  const { module, clipboard } = await environment(t);
  assert.equal(await module.copy('Team A: Ada, Bo'), true); assert.equal(clipboard.text, 'Team A: Ada, Bo');
  clipboard.writeText = async () => { throw new Error('clipboard denied'); };
  assert.equal(await module.copy('Team B: Clara, Dan'), false);
});

test('finishing same-tab presentation returns to the hold without closing the user tab', async t => {
  const { module } = await environment(t); let closed = 0, destination = null;
  window.close = () => { closed++; }; window.location = { assign(value) { destination = value; } };
  module.closeOrReturn('/holds/hold-a?view=groups'); assert.equal(closed, 0); assert.equal(destination, '/holds/hold-a?view=groups');
});

test('an opened presentation can close with a safe return fallback', async t => {
  const { module, clock } = await environment(t); let closed = 0, destination = null;
  window.opener = { closed: false }; window.closed = false; window.close = () => { closed++; window.closed = true; };
  window.location = { assign(value) { destination = value; } };
  module.closeOrReturn('/holds/hold-a?view=groups'); assert.equal(closed, 1); await clock.advance(150); assert.equal(destination, null);
});
