import { ArenaRenderer } from './arena-renderer.js?v=8';
import { createArenaAudio, audioAvatarForEvent } from './arena-audio.js?v=3';
import { ArenaInputBuffer, arenaNeutralInput } from './arena-input.js?v=1';

const soundStorageKey = 'lup.arena.sound.v1';
const volumeStorageKey = 'lup.arena.volume.v1';
const keys = new Set(), touch = new Map(), listeners = [];
const input = new ArenaInputBuffer();
const supported = new Set(['KeyA','KeyD','KeyW','KeyS','ArrowLeft','ArrowRight','ArrowUp','ArrowDown',
    'Space','KeyJ','KeyK','KeyL','KeyU','KeyI','ShiftLeft','ShiftRight']);
let canvas, bridge, renderer, audio, socket, animation, inputTimer, resizeObserver;
let heartbeat, openTimeout, reconnectTimer, reconnectAttempt = 0, lastMessage = 0;
let practice = false, disposed = true, phase = '', playerId = '', sequence = 0;
let latest, previous, receivedAt = 0, previousAt = 0, lastSent = -Infinity;
let inputPending = false, inputGeneration = 0;
let roundSpoken = false, resultSpoken = false;
let speechCaptionTimer;

function call(name, ...args) {
    if (!disposed && bridge) return bridge.invokeMethodAsync(name, ...args).catch(() => {});
    return Promise.resolve();
}

function on(target, event, handler, options) {
    target.addEventListener(event, handler, options);
    listeners.push(() => target.removeEventListener(event, handler, options));
}

export async function mount(element, reference, options) {
    dispose();
    disposed = false;
    canvas = element;
    bridge = reference;
    audio = createArenaAudio({ enabled: options.soundEnabled ?? soundPreference(),
        volume: (options.soundVolume ?? soundVolume()) / 100, phrases: options.phrases, onPhrase: showCaption });
    void audio.preload([options.selectedAvatarId]);
    on(document, 'pointerdown', () => void audio?.unlock(), { capture: true });
    on(document, 'keydown', () => void audio?.unlock(), { capture: true });
    on(document, 'click', event => {
        const toggle = event.target.closest?.('[data-arena-sound-toggle]');
        if (!toggle) return;
        const enabled = toggle.getAttribute('aria-pressed') !== 'true';
        saveSoundPreference(enabled);
        void audio?.setEnabled(enabled, { feedback: enabled });
    }, { capture: true });

    renderer = new ArenaRenderer(canvas, options, cue);
    renderer.preview(options.stages[0].id, options.selectedAvatarId);
    const fit = () => {
        const rect = canvas.getBoundingClientRect();
        const ratio = Math.min(devicePixelRatio || 1, 2);
        canvas.width = Math.max(1, Math.round(rect.width * ratio));
        canvas.height = Math.max(1, Math.round(rect.height * ratio));
        renderer.setViewport(rect.width, rect.height);
    };
    resizeObserver = new ResizeObserver(fit);
    resizeObserver.observe(canvas);
    fit();

    on(window, 'keydown', event => {
        if (!canvasVisible() || /INPUT|TEXTAREA|SELECT|BUTTON/.test(document.activeElement?.tagName || '')) return;
        if (event.code === 'KeyR' && practice && latest?.isPracticeSandbox && phase === 'playing' && !event.repeat) {
            event.preventDefault();
            void call('ResetPracticeFromKeyboard');
            return;
        }
        if (!supported.has(event.code) || !acceptsInput()) return;
        event.preventDefault();
        if (keys.has(event.code)) return;
        keys.add(event.code);
        transmitInput();
    });
    on(window, 'keyup', event => {
        if (!keys.delete(event.code)) return;
        event.preventDefault();
        transmitInput();
    });
    on(window, 'blur', clearInput);
    on(document, 'visibilitychange', () => {
        if (document.hidden) {
            clearInput();
            audio?.suspend();
        } else {
            lastMessage = performance.now();
            void audio?.unlock();
        }
    });
    on(document, 'pointerdown', event => {
        const button = event.target.closest?.('[data-arena-key]');
        if (!button || !acceptsInput()) return;
        event.preventDefault();
        button.setPointerCapture?.(event.pointerId);
        touch.set(event.pointerId, button.dataset.arenaKey);
        transmitInput();
    });
    const release = event => { if (touch.delete(event.pointerId)) transmitInput(); };
    on(document, 'pointerup', release);
    on(document, 'pointercancel', release);
    on(document, 'lostpointercapture', release);
    on(canvas, 'pointerdown', focusCanvas);

    inputTimer = setInterval(transmitInput, 1000 / 60);
    let lastFrame = performance.now(), reportedError = false;
    function draw(now) {
        if (disposed) return;
        animation = requestAnimationFrame(draw);
        const delta = Math.min(.05, (now - lastFrame) / 1000);
        lastFrame = now;
        if (canvas.width <= 1 || canvas.height <= 1) return;
        try {
            renderer.draw(now, delta, latest, previous, receivedAt, previousAt, playerId, practice);
            reportedError = false;
        } catch (error) {
            if (!reportedError) console.warn('Arena kunne ikke tegne en frame.', error);
            reportedError = true;
        }
    }
    animation = requestAnimationFrame(draw);
    return await renderer.preload();
}

function canvasVisible() { return Boolean(canvas?.offsetWidth && canvas?.offsetHeight); }
function acceptsInput() {
    return !disposed && phase !== 'finished' &&
        (practice || socket?.readyState === WebSocket.OPEN && ['countdown', 'playing'].includes(phase));
}
function focusCanvas() { if (canvasVisible()) canvas.focus({ preventScroll: true }); }
function focusAfterRender() { requestAnimationFrame(() => requestAnimationFrame(focusCanvas)); }

function heldInput() {
    if (!canvasVisible() || document.hidden) return arenaNeutralInput;
    const touchKeys = touch.size ? new Set(touch.values()) : null;
    const down = code => keys.has(code) || touchKeys?.has(code);
    let pad;
    try { pad = Array.from(navigator.getGamepads?.() || []).find(item => item?.connected); }
    catch { /* Embedded browsers may disable gamepads. Keyboard and touch still work. */ }
    const button = index => Boolean(pad?.buttons[index]?.pressed);
    const axis = index => {
        const value = pad?.axes[index] || 0;
        return Math.abs(value) > .23 ? Math.round(value * 16) / 16 : 0;
    };
    const clamp = value => Math.max(-1, Math.min(1, value));
    return {
        horizontal: clamp(Number(down('KeyD') || down('ArrowRight') || button(15)) -
            Number(down('KeyA') || down('ArrowLeft') || button(14)) + axis(0)),
        vertical: clamp(Number(down('KeyS') || down('ArrowDown') || button(13)) -
            Number(down('KeyW') || down('ArrowUp') || button(12)) + axis(1)),
        jump: down('Space') || button(0), attack: down('KeyJ') || button(2),
        special: down('KeyK') || button(1), shield: down('KeyL') || button(6) || button(7),
        grab: down('KeyU') || button(3), smash: down('KeyI') || button(5),
        dodge: down('ShiftLeft') || down('ShiftRight') || button(4)
    };
}

function transmitInput(force = false) {
    if (!acceptsInput()) return;
    const now = performance.now();
    input.update(heldInput(), now);
    // The server retains held input. Send transitions promptly and unchanged state as a heartbeat.
    if (!force && !input.dirty && !input.pressed && now - lastSent < 100) return;
    if (practice ? inputPending : socket.bufferedAmount >= 8192) return;
    const packet = input.packet(++sequence, now);
    if (practice) {
        inputPending = true;
        const generation = inputGeneration;
        void call('PracticeInput', packet).finally(() => {
            if (generation === inputGeneration) inputPending = false;
        });
    } else {
        try { socket.send(JSON.stringify(packet)); }
        catch { return; }
    }
    input.sent();
    lastSent = now;
}

function resetInput() {
    keys.clear();
    touch.clear();
    input.clear();
    lastSent = -Infinity;
    inputGeneration++;
    inputPending = false;
}

function clearInput() {
    keys.clear();
    touch.clear();
    input.clear();
    transmitInput(true);
}

export function preview(stage, id) {
    latest = previous = null;
    phase = '';
    renderer?.preview(stage, id);
}

export async function prepareBattle(stage, fighters) {
    if (!renderer) return false;
    const ids = fighters.map(fighter => typeof fighter === 'string' ? fighter : fighter.avatarId);
    audio?.warmPhrases(ids);
    return await renderer.prepareMatch(stage, fighters);
}

function resetPresentation() {
    latest = previous = null;
    phase = '';
    roundSpoken = resultSpoken = false;
    resetInput();
    renderer?.reset();
    audio?.clearSpeech?.();
    showCaption('');
}

export function beginPractice(id) {
    disconnect();
    practice = true;
    playerId = id;
    resetPresentation();
    focusAfterRender();
}

export function resetPracticePresentation() { resetPresentation(); focusAfterRender(); }
export function stopPractice(stage, id) {
    practice = false;
    resetPresentation();
    preview(stage, id);
}

export function pushState(state) {
    previous = latest;
    previousAt = receivedAt;
    latest = state;
    receivedAt = performance.now();
    const oldPhase = phase;
    phase = state.phase;
    if (oldPhase !== phase && !practice) {
        void call('PhaseChanged', state).then(() => {
            if (['countdown', 'playing'].includes(phase)) focusAfterRender();
        });
    }
    if (oldPhase !== phase && ['countdown', 'playing'].includes(phase))
        void prepareBattle(state.stageId, state.fighters);
    void audio?.preload(state.fighters.map(fighter => fighter.avatarId));
    soundActions(state, previous);
    renderer?.events(state.events || []);
    speakForPhase(state, oldPhase);
    if (phase === 'finished' && oldPhase !== 'finished')
        requestAnimationFrame(() => requestAnimationFrame(() => document.getElementById('arena-result-title')?.focus()));
}

function stopSocketTimers() {
    clearTimeout(openTimeout);
    clearInterval(heartbeat);
}

function removed(message) {
    clearTimeout(reconnectTimer);
    stopSocketTimers();
    const current = socket;
    socket = null;
    if (current) {
        current.onclose = null;
        current.close();
    }
    resetPresentation();
    void call('ArenaRemoved', message || 'Du blev fjernet fra kampen på grund af inaktivitet.');
}

export function connect(url, id) {
    const rejoining = playerId === id && !!latest;
    clearTimeout(reconnectTimer);
    stopSocketTimers();
    if (socket) {
        socket.onclose = null;
        socket.close();
    }
    practice = false;
    disposed = false;
    playerId = id;
    if (rejoining) resetInput();
    else resetPresentation();

    const parsed = new URL(url, location.href);
    if (parsed.protocol === 'https:') parsed.protocol = 'wss:';
    if (parsed.protocol === 'http:') parsed.protocol = 'ws:';
    const current = new WebSocket(parsed.href);
    socket = current;
    openTimeout = setTimeout(() => {
        if (socket === current && current.readyState !== WebSocket.OPEN) current.close();
    }, 10000);
    current.onopen = () => {
        if (socket !== current) return;
        clearTimeout(openTimeout);
        reconnectAttempt = 0;
        lastMessage = performance.now();
        void call('ConnectionChanged', false);
        heartbeat = setInterval(() => {
            if (socket !== current) return;
            if (!document.hidden && performance.now() - lastMessage > 9000) {
                current.close(4000, 'timeout');
                return;
            }
            send({ type: 'ping', sentAt: Date.now() });
        }, 3000);
    };
    current.onmessage = event => {
        if (socket !== current) return;
        lastMessage = performance.now();
        let message;
        try { message = JSON.parse(event.data); } catch { return; }
        switch (message.type) {
            case 'state': pushState(message.state); break;
            case 'room': {
                const room = message.room;
                if (room.phase === 'lobby') {
                    if (phase !== 'lobby') resetPresentation();
                    phase = 'lobby';
                    const id = room.players.find(player => player.id === playerId)?.avatarId || 'starter';
                    renderer?.preview(room.stageId, id, room.players);
                    void prepareBattle(room.stageId, room.players);
                }
                void call('RoomChanged', room);
                break;
            }
            case 'removed': removed(message.message); break;
            case 'error': void call('ArenaError', message.message || 'Handlingen kunne ikke gennemføres.'); break;
            case 'pong': {
                const ping = document.getElementById('arena-ping');
                if (ping) ping.textContent = Math.max(0, Date.now() - message.sentAt) + ' ms';
                break;
            }
        }
    };
    current.onclose = event => {
        if (socket !== current || disposed) return;
        stopSocketTimers();
        socket = null;
        if (event.reason === 'afk_removed') { removed(); return; }
        resetInput();
        void call('ConnectionChanged', true);
        retryConnection();
    };
}

function retryConnection() {
    if (disposed || socket) return;
    if (reconnectAttempt >= 7) {
        void call('ConnectionChanged', false);
        void call('ArenaError', 'Forbindelsen kunne ikke gendannes. Forlad rummet, og deltag igen med rumkoden.');
        return;
    }
    const delay = Math.min(4000, 500 * 2 ** reconnectAttempt++);
    reconnectTimer = setTimeout(async () => {
        if (disposed || socket) return;
        if (!await call('Reconnect')) retryConnection();
    }, delay);
}

export function send(message) {
    if (socket?.readyState !== WebSocket.OPEN) return false;
    try { socket.send(JSON.stringify(message)); return true; }
    catch { return false; }
}

export function disconnect() {
    clearTimeout(reconnectTimer);
    stopSocketTimers();
    if (socket) {
        send({ type: 'leave' });
        socket.onclose = null;
        socket.close(1000, 'leave');
        socket = null;
    }
    reconnectAttempt = 0;
    resetInput();
}

export async function fullscreen() {
    const target = document.querySelector('.arena-shell');
    if (!target) return;
    try {
        if (document.fullscreenElement) await document.exitFullscreen();
        else await target.requestFullscreen();
        focusCanvas();
    } catch {
        void call('ArenaError', 'Fuld skærm er ikke tilgængelig i denne browser.');
    }
}
export async function copyInvite(url) {
    try { await navigator.clipboard.writeText(url); return true; }
    catch { return false; }
}

export function soundPreference() {
    try { return localStorage.getItem(soundStorageKey) !== 'off'; }
    catch { return true; }
}
function saveSoundPreference(enabled) {
    try { localStorage.setItem(soundStorageKey, enabled ? 'on' : 'off'); } catch {}
}
export function soundVolume() {
    try {
        const saved = localStorage.getItem(volumeStorageKey);
        return saved !== null && Number.isFinite(Number(saved)) ? Math.max(0, Math.min(100, Number(saved))) : 65;
    } catch { return 65; }
}
export function setVolume(value) {
    const level = Number.isFinite(value) ? Math.max(0, Math.min(100, value)) : 65;
    try { localStorage.setItem(volumeStorageKey, String(level)); } catch {}
    audio?.setVolume?.(level / 100);
}
export function sound(enabled) {
    saveSoundPreference(enabled);
    if (!enabled) showCaption('');
    return audio?.setEnabled(enabled) || Promise.resolve(false);
}

export async function previewPhrase(avatarId, name) {
    if (await audio?.unlock()) return await audio.phrase(avatarId, 'greeting', name, true);
    return false;
}

function showCaption(text, duration = 0) {
    clearTimeout(speechCaptionTimer);
    const caption = document.getElementById('arena-speech');
    if (!caption) return;
    caption.textContent = text;
    caption.hidden = !text;
    if (text && duration > 0) speechCaptionTimer = setTimeout(() => showCaption(''), duration * 1000);
}

function speakForPhase(state, oldPhase) {
    const local = state.fighters.find(fighter => fighter.id === playerId);
    if (phase === 'playing' && oldPhase !== 'playing' && !roundSpoken) {
        roundSpoken = true;
        const speaker = local?.avatarId !== 'starter' ? local : state.fighters.find(fighter => fighter.avatarId !== 'starter');
        if (speaker) void audio?.phrase?.(speaker.avatarId, 'start', speaker.name);
    }
    if (phase === 'finished' && !resultSpoken) {
        resultSpoken = true;
        const winner = state.fighters.find(fighter => fighter.id === state.winnerId);
        const speaker = local?.avatarId !== 'starter' ? local : winner;
        if (speaker) void audio?.phrase?.(speaker.avatarId, speaker.id === state.winnerId ? 'success' : 'retry', speaker.name, true);
    }
}

function cue(type, strength = 1, event = {}) {
    const fighters = latest?.fighters || [];
    const defender = ['hit', 'ko', 'shield', 'parry'].includes(type);
    const entity = defender ? event.targetId || event.actorId : event.actorId;
    return audio?.cue(type, strength, audioAvatarForEvent({ ...event, type }, fighters), entity);
}

function soundActions(state,old){
    if(!old||state.phase!=='playing')return;
    for(const fighter of state.fighters||[]){
        const before=old.fighters?.find(f=>f.id===fighter.id);
        if(!before||fighter.out||fighter.invulnerable&&fighter.action==='respawn')continue;
        const ability=fighter.combat?.activeAbility;
        if((['rocket','charge','sleigh','iterate'].includes(ability)&&before.combat?.activeAbility!==ability)||(fighter.avatarId==='starter'&&fighter.action==='ability_4_dash'&&before.action!==fighter.action))audio?.cue('dash',1,fighter.avatarId,fighter.id);
        if(ability==='capture'&&before.combat?.activeAbility!==ability)audio?.cue('capture',1,fighter.avatarId,fighter.id);
        if(fighter.shielding&&!before.shielding)audio?.cue('shield-up',1,fighter.avatarId,fighter.id);
        if(fighter.action==='ability_1_pulse'&&!ability&&(before.action!==fighter.action||fighter.actionFrame<before.actionFrame) &&
            !state.events?.some(event => event.type === 'projectile' && event.actorId === fighter.id && event.tick > old.tick))
            audio?.cue('swing',1,fighter.avatarId,fighter.id);
        if(fighter.grounded&&!before.grounded&&before.vy>120)audio?.cue('land',.7,fighter.avatarId,fighter.id);
    }
}

export function dispose() {
    disposed = true;
    disconnect();
    practice = false;
    cancelAnimationFrame(animation);
    clearInterval(inputTimer);
    clearTimeout(speechCaptionTimer);
    resizeObserver?.disconnect();
    listeners.splice(0).forEach(remove => remove());
    renderer?.dispose();
    audio?.dispose();
    renderer = audio = bridge = canvas = latest = previous = null;
}
