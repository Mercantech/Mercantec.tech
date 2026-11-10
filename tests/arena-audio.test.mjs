import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

const source = await fs.readFile(new URL('../web/wwwroot/js/arena-audio.js', import.meta.url), 'utf8');
const { createArenaAudio, audioAvatarForEvent } = await import('data:text/javascript;base64,' + Buffer.from(source + '\n//# sourceURL=arena-audio.js').toString('base64'));

class AudioParam {
    constructor(value = 0) { this.value = value; this.automation = []; }
    setValueAtTime(value, time) { return this.schedule('set', value, time); }
    linearRampToValueAtTime(value, time) { return this.schedule('linear', value, time); }
    exponentialRampToValueAtTime(value, time) {
        assert.ok(value > 0, 'Web Audio exponential ramps require a positive target');
        return this.schedule('exponential', value, time);
    }
    setTargetAtTime(value, time, constant) {
        assert.ok(Number.isFinite(constant) && constant > 0);
        return this.schedule('target', value, time);
    }
    cancelScheduledValues(time) { assert.ok(Number.isFinite(time)); return this; }
    cancelAndHoldAtTime(time) { assert.ok(Number.isFinite(time)); return this; }
    schedule(kind, value, time) {
        assert.ok(Number.isFinite(value), 'audio automation must stay finite');
        assert.ok(Number.isFinite(time) && time >= 0, 'audio automation needs a valid time');
        this.value = value;
        this.automation.push({ kind, value, time });
        return this;
    }
}

class AudioNode {
    constructor(context, kind) {
        this.context = context;
        this.kind = kind;
        this.connections = [];
        this.disconnected = false;
    }
    connect(target) { this.connections.push(target); return target; }
    disconnect() { this.disconnected = true; this.connections = []; }
}

class AudioSource extends AudioNode {
    constructor(context, kind) {
        super(context, kind);
        this.frequency = new AudioParam(440);
        this.detune = new AudioParam();
        this.playbackRate = new AudioParam(1);
        this.starts = [];
        this.stops = [];
        this.onended = null;
    }
    start(time = 0) {
        assert.ok(Number.isFinite(time) && time >= 0);
        this.starts.push(time);
    }
    stop(time = 0) {
        assert.ok(Number.isFinite(time) && time >= 0);
        this.stops.push(time);
    }
    finish() { this.onended?.(); }
}

class FakeAudioContext {
    constructor({ resumeFailures = 0 } = {}) {
        this.currentTime = 10;
        this.sampleRate = 48000;
        this.state = 'suspended';
        this.destination = new AudioNode(this, 'destination');
        this.nodes = [];
        this.resumeCalls = 0;
        this.closeCalls = 0;
        this.decodeCalls = [];
        this.resumeFailures = resumeFailures;
    }
    node(kind) { const node = new AudioNode(this, kind); this.nodes.push(node); return node; }
    source(kind) { const source = new AudioSource(this, kind); this.nodes.push(source); return source; }
    createOscillator() { return this.source('oscillator'); }
    createBufferSource() { return this.source('buffer-source'); }
    createGain() { const node = this.node('gain'); node.gain = new AudioParam(1); return node; }
    createBiquadFilter() {
        const node = this.node('filter');
        node.frequency = new AudioParam(350);
        node.Q = new AudioParam(1);
        node.gain = new AudioParam();
        return node;
    }
    createDynamicsCompressor() {
        const node = this.node('compressor');
        for (const name of ['threshold', 'knee', 'ratio', 'attack', 'release']) node[name] = new AudioParam();
        return node;
    }
    createStereoPanner() { const node = this.node('panner'); node.pan = new AudioParam(); return node; }
    createBuffer(channels, length, sampleRate) {
        assert.ok(Number.isInteger(channels) && channels > 0);
        assert.ok(Number.isInteger(length) && length > 0);
        assert.ok(Number.isFinite(sampleRate) && sampleRate > 0);
        const data = Array.from({ length: channels }, () => new Float32Array(length));
        return { length, sampleRate, duration: length / sampleRate, getChannelData: channel => data[channel] };
    }
    async decodeAudioData(bytes) {
        assert.ok(bytes instanceof ArrayBuffer, 'sound files must be decoded from actual bytes');
        assert.ok(bytes.byteLength > 0);
        this.decodeCalls.push(bytes);
        const buffer = this.createBuffer(1, 4800, this.sampleRate);
        buffer.decodedFrom = new TextDecoder().decode(bytes);
        return buffer;
    }
    async resume() {
        this.resumeCalls++;
        if (this.resumeFailures-- > 0) throw new Error('Autoplay was blocked');
        this.state = 'running';
    }
    async suspend() { this.state = 'suspended'; }
    async close() { this.closeCalls++; this.state = 'closed'; }
    advance(seconds = 1) { this.currentTime += seconds; }
    get sources() { return this.nodes.filter(node => node instanceof AudioSource); }
}

function reachesDestination(source, visited = new Set()) {
    if (visited.has(source)) return false;
    visited.add(source);
    return source.kind === 'destination' || source.connections.some(node => reachesDestination(node, visited));
}

function harness(options = {}) {
    const contexts = [], requests = [];
    const audio = createArenaAudio({
        createContext: () => {
            const context = new FakeAudioContext(options);
            contexts.push(context);
            return context;
        },
        fetchAudio: async url => {
            requests.push(url);
            if (options.fetchFails) throw new Error('Clip unavailable');
            return { ok: true, arrayBuffer: async () => new TextEncoder().encode(url).buffer };
        },
        ...(options.enabled === undefined ? {} : { enabled: options.enabled }),
        ...(options.volume === undefined ? {} : { volume: options.volume })
    });
    return { audio, contexts, requests, get context() { return contexts[0]; } };
}

function assertAudibleSource(context, sources = context.sources) {
    assert.ok(sources.some(voice => voice.starts.length > 0 && reachesDestination(voice)),
        'an enabled cue must start a source connected to the speaker graph');
    assert.ok(context.nodes.some(node => node.gain?.automation.some(point => point.value > 0)),
        'the sound graph must schedule a nonzero gain');
}

test('sound defaults on while construction and preload wait for a trusted unlock', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    assert.equal(h.audio.enabled, true);
    assert.equal(h.contexts.length, 0);
    assert.equal(h.audio.cue('hit', 1, 'bip01'), false);
    await h.audio.preload(['bip01', 'raymond']);
    assert.equal(h.contexts.length, 0, 'preloading must not create a blocked AudioContext');

    const unlocking = h.audio.unlock();
    assert.equal(h.contexts.length, 1, 'context creation must run inside the gesture call stack');
    assert.equal(h.context.resumeCalls, 1, 'resume must run before the first await loses activation');
    assert.equal(await unlocking, true);
    assert.equal(h.context.sources.filter(voice => voice.starts.length).length, 0,
        'unlock without feedback must not unexpectedly make noise');
    assert.equal(h.audio.cue('hit', 1, 'bip01'), true);
    assertAudibleSource(h.context);
});

test('blocked or subsequently suspended audio can be unlocked by the next gesture without leaking contexts', async t => {
    const h = harness({ resumeFailures: 1 });
    t.after(() => h.audio.dispose());
    assert.equal(await h.audio.unlock(), false);
    assert.equal(h.contexts.length, 1);
    assert.equal(h.audio.cue('jump', 1, 'raymond'), false);
    assert.equal(await h.audio.unlock(), true);
    assert.equal(h.contexts.length, 1);
    assert.equal(h.audio.cue('jump', 1, 'raymond'), true);
    h.context.state = 'suspended';
    h.context.advance();
    assert.equal(h.audio.cue('projectile', 1, 'raymond'), false);
    assert.equal(await h.audio.unlock(), true);
    assert.equal(h.contexts.length, 1);
    assert.equal(h.context.resumeCalls, 3);
    assert.equal(h.audio.cue('projectile', 1, 'raymond'), true);
    assertAudibleSource(h.context);
});

test('mute immediately silences active voices and re-enabling reuses the unlocked graph', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    assert.equal(h.audio.cue('overload', 2, 'bip01'), true);
    const voices = h.context.sources.filter(voice => voice.starts.length);
    assert.ok(voices.length > 0);
    const muting = h.audio.setEnabled(false);
    assert.equal(h.audio.enabled, false);
    assert.ok(voices.every(voice => voice.stops.some(time => time <= h.context.currentTime)),
        'mute must stop existing voices immediately, not just suppress future cues');
    await muting;
    const count = h.context.sources.length;
    assert.equal(h.audio.cue('hit', 1, 'raymond'), false);
    assert.equal(h.context.sources.length, count);
    assert.equal(await h.audio.setEnabled(true), true);
    assert.equal(h.audio.enabled, true);
    assert.equal(h.contexts.length, 1);
    h.context.advance();
    assert.equal(h.audio.cue('hit', 1, 'raymond'), true);
    assertAudibleSource(h.context, h.context.sources.slice(count));
});

test('an intentionally muted mount does not create or resume audio until enabled', async t => {
    const h = harness({ enabled: false });
    t.after(() => h.audio.dispose());
    assert.equal(h.audio.enabled, false);
    assert.equal(await h.audio.unlock(), false);
    assert.equal(h.contexts.length, 0);
    assert.equal(h.audio.cue('hit'), false);
    assert.equal(await h.audio.setEnabled(true, { feedback: true }), true);
    assert.equal(h.contexts.length, 1);
    assertAudibleSource(h.context);
});

test('preloaded recorded clips retain the selected avatar and load once per file', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    await h.audio.preload(['bip01', 'raymond', 'santa', 'lupp']);
    const fetched = h.requests.length;
    assert.ok(fetched > 0);
    await h.audio.preload(['bip01', 'raymond', 'santa', 'lupp']);
    assert.equal(h.requests.length, fetched, 'repeated state/round preloads must reuse cached files');
    assert.equal(new Set(h.requests).size, h.requests.length);
    for (const [avatar, path] of [['bip01', 'bip-01'], ['raymond', 'raymond'], ['santa', 'santa'], ['lupp', 'lupp']]) {
        h.context.advance();
        const count = h.context.sources.length;
        assert.equal(h.audio.cue('projectile', 1, avatar), true);
        const recorded = h.context.sources.slice(count).find(voice => voice.kind === 'buffer-source');
        assert.ok(recorded, avatar + ' should use its recorded projectile sound');
        assert.match(recorded.buffer.decodedFrom, new RegExp('/assets/avatars/' + path + '/audio/ability_1_pulse\\.wav'));
        assertAudibleSource(h.context, [recorded]);
    }
});

test('LUPP abilities play his recorded clips and Game Jam only cancels its owner charge', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    await h.audio.preload(['lupp']);
    for (const [event, clip] of [['feedback', 'ability_2_magnet'], ['prototype', 'ability_3_shield'],
        ['game-jam', 'ultimate_release'], ['prototype-bounce', 'land']]) {
        h.context.advance();
        const count = h.context.sources.length;
        assert.equal(h.audio.cue(event, 1, 'lupp', 'designer'), true, event);
        const recorded = h.context.sources.slice(count).find(voice => voice.kind === 'buffer-source');
        assert.ok(recorded, event + ' must use LUPP audio');
        assert.equal(recorded.buffer.decodedFrom, '/assets/avatars/lupp/audio/' + clip + '.wav');
        assertAudibleSource(h.context, [recorded]);
    }
    h.context.advance();
    h.audio.cue('ultimate-charge', 1, 'lupp', 'designer-one');
    const firstCharge = h.context.sources.at(-1);
    h.audio.cue('ultimate-charge', 1, 'lupp', 'designer-two');
    const secondCharge = h.context.sources.at(-1);
    assert.equal(h.audio.cue('game-jam', 1, 'lupp', 'designer-one'), true);
    assert.ok(firstCharge.stops.some(time => time <= h.context.currentTime));
    assert.ok(secondCharge.stops.every(time => time > h.context.currentTime));
});

test('combat and unique ability events remain audible when recorded clips fail to load', async t => {
    const h = harness({ fetchFails: true });
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    await h.audio.preload(['bip01', 'raymond']);
    const events = ['jump', 'hit', 'projectile', 'shield', 'parry', 'break', 'ko', 'respawn',
        'dodge', 'recovery', 'magnet', 'capture', 'throw', 'escape', 'absorb', 'reflect',
        'repair-start', 'repair', 'repair-cancel', 'armor', 'ultimate-charge', 'overload', 'join', 'join-explode',
        'feedback', 'prototype', 'game-jam', 'prototype-bounce', 'win'];
    for (const event of events) {
        h.context.advance();
        const count = h.context.sources.length;
        assert.equal(h.audio.cue(event, 1, event.startsWith('join') ? 'raymond' : 'bip01'), true, event);
        assertAudibleSource(h.context, h.context.sources.slice(count));
    }
});

test('event routing uses the defender for impacts, the actor for abilities and Raymond for reflected JOIN', () => {
    const fighters = new Map([
        ['robot', { id: 'robot', avatarId: 'bip01' }],
        ['elephant', { id: 'elephant', avatarId: 'raymond' }],
        ['santa', { id: 'santa', avatarId: 'santa' }],
        ['designer', { id: 'designer', avatarId: 'lupp' }]
    ]);
    for (const type of ['hit', 'ko', 'shield', 'parry']) {
        assert.equal(audioAvatarForEvent({ type, actorId: 'robot', targetId: 'elephant' }, fighters), 'raymond', type);
    }
    for (const type of ['break', 'reflect', 'absorb', 'projectile', 'capture', 'repair', 'overload']) {
        assert.equal(audioAvatarForEvent({ type, actorId: 'robot', targetId: 'elephant' }, fighters), 'bip01', type);
    }
    assert.equal(audioAvatarForEvent({ type: 'gift-fest', actorId: 'santa' }, fighters), 'santa');
    for (const type of ['feedback', 'prototype', 'game-jam', 'prototype-bounce']) {
        assert.equal(audioAvatarForEvent({ type, actorId: 'designer', targetId: 'elephant' }, fighters), 'lupp');
    }
    assert.equal(audioAvatarForEvent({ type: 'join-explode', actorId: 'robot', targetId: 'elephant' }, fighters), 'raymond');
    assert.equal(audioAvatarForEvent({ type: 'hit', actorId: 'missing', targetId: 'elephant' }, fighters), 'raymond');
    assert.equal(audioAvatarForEvent({ type: 'hit' }, fighters), 'starter');
});

test('continuous field events and malformed cues cannot create unbounded or nonfinite voices', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    for (let i = 0; i < 200; i++) {
        h.audio.cue('pull', 1, 'bip01');
        h.audio.cue('join-pull', 1, 'raymond');
        h.audio.cue('release', 1, 'raymond');
    }
    assert.ok(h.context.sources.filter(voice => voice.starts.length).length <= 12,
        'continuous network field events must be omitted or throttled');
    for (const value of [NaN, Infinity, -Infinity, -100, 0, 10000]) {
        h.context.advance();
        assert.doesNotThrow(() => h.audio.cue('hit', value, 'bip01'));
    }
    assert.equal(h.audio.cue('unknown-event', 1, 'bip01'), false);
    assert.equal(h.audio.cue('__proto__', 1, 'bip01'), false);
    assert.equal(h.audio.cue(null, 1, 'bip01'), false);
});

test('disposal stops voices, closes once and permanently prevents recreation', async () => {
    const h = harness();
    await h.audio.unlock();
    h.audio.cue('join-explode', 2, 'raymond');
    const voices = h.context.sources.filter(voice => voice.starts.length);
    await h.audio.dispose();
    assert.equal(h.context.closeCalls, 1);
    assert.ok(voices.every(voice => voice.stops.some(time => time <= h.context.currentTime)));
    assert.equal(h.audio.cue('hit', 1, 'bip01'), false);
    assert.equal(await h.audio.unlock(), false);
    await h.audio.setEnabled(true);
    await h.audio.preload(['bip01']);
    await h.audio.dispose();
    assert.equal(h.contexts.length, 1);
    assert.equal(h.context.closeCalls, 1);
});

test('a dense multiplayer burst keeps at most twelve live voices', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    const events = ['jump', 'land', 'hit', 'projectile', 'shield', 'parry', 'break', 'ko', 'respawn',
        'dodge', 'recovery', 'magnet', 'capture', 'throw', 'escape', 'absorb', 'reflect',
        'repair-start', 'repair', 'repair-cancel', 'armor', 'ultimate-charge', 'overload', 'join', 'join-explode', 'win'];
    for (const event of events) h.audio.cue(event, 1, 'bip01');
    const live = h.context.sources.filter(voice => voice.starts.length &&
        !voice.stops.some(time => time <= h.context.currentTime));
    assert.ok(live.length <= 12, 'simultaneous network events must evict old voices rather than stack indefinitely');
});

test('a pending browser resume cannot reactivate audio after the arena is disposed', async () => {
    const context = new FakeAudioContext();
    let finishResume;
    context.resume = () => new Promise(resolve => { finishResume = resolve; });
    const audio = createArenaAudio({ createContext: () => context });
    const unlocking = audio.unlock();
    assert.equal(typeof finishResume, 'function');
    await audio.dispose();
    finishResume();
    assert.equal(await unlocking, false);
    assert.equal(context.closeCalls, 1);
    assert.equal(audio.cue('hit'), false);
    assert.equal(context.sources.length, 0);
});

test('hiding the page invalidates a pending resume and suppresses its feedback', async t => {
    const context = new FakeAudioContext();
    let finishResume;
    context.resume = () => new Promise(resolve => {
        finishResume = () => { context.state = 'running'; resolve(); };
    });
    const audio = createArenaAudio({ createContext: () => context });
    t.after(() => audio.dispose());
    const unlocking = audio.unlock({ feedback: true });
    audio.suspend();
    finishResume();
    assert.equal(await unlocking, false, 'a stale resume must not undo the visibility pause');
    assert.equal(audio.cue('hit', 1, 'bip01'), false);
    assert.equal(context.sources.length, 0, 'confirmation must not play after the page was hidden');
});

test('a stale resume cannot suspend a newer successful gesture unlock', async t => {
    const context = new FakeAudioContext();
    const finishes = [];
    context.resume = () => new Promise(resolve => {
        finishes.push(() => { context.state = 'running'; resolve(); });
    });
    const audio = createArenaAudio({ createContext: () => context });
    t.after(() => audio.dispose());
    const stale = audio.unlock({ feedback: true });
    audio.suspend();
    const fresh = audio.unlock();
    assert.equal(finishes.length, 2);
    finishes[1]();
    assert.equal(await fresh, true);
    finishes[0]();
    assert.equal(await stale, false);
    assert.equal(context.state, 'running', 'the stale promise must preserve the newer active gesture');
    assert.equal(audio.cue('hit', 1, 'bip01'), true);
});

test('a clip finishing its load while muted never replays the cancelled event', async t => {
    const context = new FakeAudioContext();
    const pending = [];
    const audio = createArenaAudio({
        createContext: () => context,
        fetchAudio: url => new Promise(resolve => pending.push(() => resolve({
            ok: true, arrayBuffer: async () => new TextEncoder().encode(url).buffer
        })))
    });
    t.after(() => audio.dispose());
    await audio.unlock();
    const preloading = audio.preload(['bip01']);
    assert.ok(pending.length > 0);
    audio.cue('projectile', 1, 'bip01');
    const count = context.sources.length;
    await audio.setEnabled(false);
    for (const finish of pending) finish();
    await preloading;
    assert.equal(context.sources.length, count, 'late clip decoding must cache the clip without starting it');
    assert.equal(audio.cue('projectile', 1, 'bip01'), false);
    assert.equal(await audio.setEnabled(true), true);
    context.advance();
    assert.equal(audio.cue('projectile', 1, 'bip01'), true);
    assert.ok(context.sources.slice(count).some(voice => voice.kind === 'buffer-source'));
});

test('channel cancellation belongs to one player and ended voices release their graph', async t => {
    const h = harness();
    t.after(() => h.audio.dispose());
    await h.audio.unlock();
    h.audio.cue('repair-start', 1, 'bip01', 'player-one');
    const first = h.context.sources.at(-1);
    h.audio.cue('repair-start', 1, 'bip01', 'player-two');
    const second = h.context.sources.at(-1);
    assert.notEqual(first, second, 'same-avatar players need independent sound ownership');
    h.audio.cue('repair-cancel', 1, 'bip01', 'player-one');
    assert.ok(first.stops.some(time => time <= h.context.currentTime));
    assert.ok(second.stops.every(time => time > h.context.currentTime));
    second.finish();
    assert.equal(second.disconnected, true);
});

test('unsupported browsers and constructor failure leave gameplay callers safe', async () => {
    for (const createContext of [() => null, () => { throw new Error('Audio unavailable'); }]) {
        const audio = createArenaAudio({ createContext, fetchAudio: async () => { throw new Error('No sound'); } });
        assert.equal(await audio.unlock(), false);
        assert.equal(audio.cue('hit', 1, 'bip01'), false);
        await assert.doesNotReject(() => audio.setEnabled(true));
        await assert.doesNotReject(() => audio.preload(['bip01']));
        await audio.dispose();
    }
});
