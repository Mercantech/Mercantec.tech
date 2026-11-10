// Browser audio is unlocked by the client's native input handlers, before Blazor interop.
const folders = { bip01: 'bip-01', raymond: 'raymond', santa: 'santa', lupp: 'lupp' };
const clips = ['jump','land','hit','knockout','respawn','celebrate','ability_1_pulse','ability_2_magnet','ability_3_shield','ability_4_dash','ability_5_repair','ultimate_charge','ultimate_release'];
const actions = {
    jump:'jump', land:'land', hit:'hit', ko:'knockout', respawn:'respawn', win:'celebrate',
    projectile:'ability_1_pulse', swing:'ability_1_pulse', magnet:'ability_2_magnet', capture:'ability_2_magnet',
    throw:'ability_2_magnet', 'gift-grab':'ability_2_magnet', feedback:'ability_2_magnet', shield:'ability_3_shield', 'shield-up':'ability_3_shield',
    parry:'ability_3_shield', reflect:'ability_3_shield', absorb:'ability_3_shield', armor:'ability_3_shield', prototype:'ability_3_shield',
    break:'hit', dodge:'ability_4_dash', dash:'ability_4_dash', recovery:'ability_4_dash',
    'repair-start':'ability_5_repair', 'ultimate-charge':'ultimate_charge', overload:'ultimate_release',
    join:'ultimate_release', 'join-explode':'ultimate_release', 'gift-fest':'ultimate_release', 'game-jam':'ultimate_release', 'prototype-bounce':'land'
};
const isAvatar = avatar => Object.hasOwn(folders, avatar);
const tones = {
    confirm:[660,.16,1.5], countdown:[440,.12,1], go:[660,.26,2], hit:[150,.13,.45], ko:[145,.48,.2],
    jump:[380,.15,2], land:[125,.10,.7], shield:[520,.16,1.4], 'shield-up':[520,.16,1.4],
    parry:[780,.20,1.5], projectile:[600,.19,1.8], swing:[210,.10,1.4], win:[520,.65,2], break:[180,.32,.3],
    dodge:[330,.14,1.8], dash:[260,.23,2.2], recovery:[350,.25,2.4], respawn:[330,.30,2],
    magnet:[180,.32,2.5], capture:[230,.23,.7], 'gift-grab':[500,.26,1.5], feedback:[180,.32,2.5], throw:[210,.20,2.3],
    escape:[530,.18,1.7], absorb:[480,.17,.65], reflect:[790,.22,1.5], armor:[115,.19,.6], prototype:[520,.16,1.4], 'prototype-bounce':[125,.10,.7],
    'repair-start':[370,.38,1.5], repair:[600,.30,2], 'repair-cancel':[240,.12,.6],
    'ultimate-charge':[160,.55,3], overload:[100,.65,2.7], join:[240,.55,2], 'join-explode':[90,.65,2.5], 'gift-fest':[500,.55,2], 'game-jam':[500,.55,2]
};

export function audioAvatarForEvent(event, fighters = []) {
    if (event.type === 'join-explode') return 'raymond';
    const map = fighters instanceof Map ? fighters : new Map(fighters.map(f => [f.id, f]));
    const defender = ['hit','ko','shield','parry'].includes(event.type);
    return map.get(defender ? event.targetId || event.actorId : event.actorId)?.avatarId || 'starter';
}

export function createArenaAudio({ createContext = () => new (globalThis.AudioContext || globalThis.webkitAudioContext)(),
    fetchAudio = globalThis.fetch?.bind(globalThis), enabled = true, volume = .65, onStateChange = () => {} } = {}) {
    let context, master, disposed = false, unavailable = false, paused = false, pauseVersion = 0;
    const voices = new Set(), buffers = new Map(), loading = new Map(), queued = new Set(), last = new Map();
    const state = () => disposed ? 'closed' : !enabled ? 'muted' : unavailable ? 'unavailable' : context?.state || 'locked';
    const notify = () => onStateChange(state());
    const stop = voice => {
        if (!voices.delete(voice)) return;
        try { voice.source.stop(); } catch { }
        try { voice.source.disconnect(); voice.gain.disconnect(); } catch { }
    };
    const stopMatching = (entity, clip) => { for (const v of [...voices]) if ((!entity || v.entity === entity) && (!clip || v.clip === clip)) stop(v); };
    async function load(avatar, clip) {
        if (!isAvatar(avatar) || !context || disposed || typeof fetchAudio !== 'function') return null;
        const key = avatar + ':' + clip;
        if (buffers.has(key)) return buffers.get(key);
        if (!loading.has(key)) {
            const owner = context;
            loading.set(key, (async () => {
                try {
                    const response = await fetchAudio('/assets/avatars/' + folders[avatar] + '/audio/' + clip + '.wav');
                    if (!response.ok) return null;
                    const data = await response.arrayBuffer();
                    if (disposed || owner !== context) return null;
                    const decoded = await owner.decodeAudioData(data);
                    if (!decoded || !Number.isFinite(decoded.duration) || decoded.duration <= 0 || disposed || owner !== context) return null;
                    buffers.set(key, decoded);
                    return decoded;
                } catch { return null; }
            })());
        }
        return loading.get(key);
    }
    async function preload(avatars = []) {
        for (const avatar of avatars) if (isAvatar(avatar)) queued.add(avatar);
        if (!context || disposed || !enabled) return;
        const pending = [...queued]; queued.clear();
        await Promise.all(pending.flatMap(avatar => clips.map(clip => load(avatar, clip))));
    }
    function unlock({ feedback = false } = {}) {
        if (disposed || !enabled) return Promise.resolve(false);
        const version = pauseVersion; paused = false;
        try {
            if (!context || context.state === 'closed') {
                context = createContext(); unavailable = false;
                master = context.createGain();
                master.gain.setValueAtTime(Math.max(0, Math.min(1, Number.isFinite(volume) ? volume : .65)), context.currentTime);
                if (context.createDynamicsCompressor) {
                    const limiter = context.createDynamicsCompressor();
                    limiter.threshold.setValueAtTime(-14, context.currentTime);
                    limiter.ratio.setValueAtTime(12, context.currentTime);
                    master.connect(limiter); limiter.connect(context.destination);
                } else master.connect(context.destination);
                context.onstatechange = notify;
            }
            // Keep resume in the trusted click/keydown stack; no await before this call.
            const owner = context;
            const resumed = owner.state === 'running' ? undefined : owner.resume();
            notify();
            return Promise.resolve(resumed).then(() => {
                if (disposed || !enabled || paused || version !== pauseVersion || owner !== context || owner.state !== 'running') {
                    if (paused && !disposed && owner.state === 'running') void owner.suspend().catch(() => {});
                    notify(); return false;
                }
                void preload();
                if (feedback) cue('confirm');
                notify(); return true;
            }).catch(() => { notify(); return false; });
        } catch { unavailable = true; notify(); return Promise.resolve(false); }
    }
    function setEnabled(value, options) {
        if (disposed) return Promise.resolve(false);
        enabled = Boolean(value);
        if (!enabled) { pauseVersion++; stopMatching(); notify(); return Promise.resolve(false); }
        return unlock(options);
    }
    function cue(type, strength = 1, avatar = 'starter', entity = avatar) {
        if (disposed || !enabled || paused || context?.state !== 'running') return false;
        if (type === 'pull' || type === 'join-pull' || type === 'release') return false;
        if (type === 'repair-cancel' || type === 'repair') stopMatching(entity, 'ability_5_repair');
        if (['overload','join','gift-fest','game-jam'].includes(type)) stopMatching(entity, 'ultimate_charge');
        if (type === 'hit' || type === 'ko') { stopMatching(entity, 'ability_5_repair'); stopMatching(entity, 'ultimate_charge'); }
        const tone = Object.hasOwn(tones,type) ? tones[type] : null;
        const clip = Object.hasOwn(actions,type) ? actions[type] : null;
        if (!tone) return false;
        const time = context.currentTime;
        const key = entity + ':' + (clip || type);
        const interval = clip === 'ability_2_magnet' ? .22 : ['hit','projectile','swing'].includes(type) ? .045 : .12;
        if (time - (last.get(key) ?? -Infinity) < interval) return false;
        last.set(key, time);
        const strengthGain = Math.max(.35, Math.min(1.5, Number.isFinite(strength) ? strength : 1));
        const buffer = buffers.get(avatar + ':' + clip);
        if (!buffer && clip) void load(avatar, clip);
        // A fetch/decode failure never silences gameplay: the local cue plays immediately.
        const source = buffer ? context.createBufferSource() : context.createOscillator();
        const gain = context.createGain();
        const duration = buffer ? Math.min(3, buffer.duration) : tone[1];
        if (buffer) source.buffer = buffer;
        else {
            source.type = ['ko','break','overload','join-explode','armor'].includes(type) ? 'triangle' : 'sine';
            source.frequency.setValueAtTime(tone[0], time);
            source.frequency.exponentialRampToValueAtTime(Math.max(25, tone[0] * tone[2]), time + duration);
        }
        gain.gain.setValueAtTime(.001, time);
        gain.gain.linearRampToValueAtTime((buffer ? .75 : .28) * strengthGain, time + .008);
        if (buffer) {
            gain.gain.setValueAtTime(.75 * strengthGain, time + Math.max(.009, duration - .035));
            gain.gain.linearRampToValueAtTime(.001, time + duration);
        } else gain.gain.exponentialRampToValueAtTime(.001, time + duration);
        source.connect(gain); gain.connect(master);
        while (voices.size >= 12) stop(voices.values().next().value);
        const voice = { source, gain, entity, clip };
        voices.add(voice);
        source.onended = () => { voices.delete(voice); try { source.disconnect(); gain.disconnect(); } catch { } };
        source.start(time); source.stop(time + duration + .015);
        return true;
    }
    function suspend() { paused = true; pauseVersion++; stopMatching(); if (context && context.state !== 'closed') void context.suspend().catch(() => {}); }
    function dispose() {
        if (disposed) return;
        disposed = true; paused = true; pauseVersion++; stopMatching(); queued.clear(); buffers.clear(); loading.clear();
        if (context) { context.onstatechange = null; try { void context.close().catch(() => {}); } catch { } }
        notify();
    }
    return { get enabled() { return enabled; }, get state() { return state(); }, unlock, setEnabled, cue, preload, suspend, dispose };
}
