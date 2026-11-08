/* Separate preview entities. The actor's existing CSS clock owns pose timing. */
const states = new WeakMap();
const instances = new Map();
const roots = new Set(['/assets/avatars/raymond']);
const visibleEvents = new Set(['attach_item', 'spawn_projectile', 'spawn_effect']);
const number = (value, fallback = 0) => Number.isFinite(Number(value)) ? Number(value) : fallback;
const bounded = (value, min, max, fallback) => Math.min(max, Math.max(min, number(value, fallback)));

export function safeAsset(root, relative) {
    const match = typeof relative === 'string' ? /^(items|projectiles|effects)\/[a-z0-9_-]+\.png$/.exec(relative) : null;
    return roots.has(root) && typeof relative === 'string'
        && match?.[0] === relative ? root + '/' + relative : null;
}

export function frameAt(timeMs, fps, frames, loop) {
    const duration = frames * 1000 / fps;
    const time = Math.max(0, number(timeMs));
    const cycle = loop ? Math.floor(time / duration) : 0;
    const local = loop ? time % duration : Math.min(time, duration);
    const fractional = Math.min(frames - 1, local * fps / 1000);
    return { frame: Math.floor(fractional), fractional, cycle, finished: !loop && time >= duration, duration, localTime: local };
}

export function sampleAnchor(anchor, frame, fallback = { x: 256, y: 448 }) {
    const samples = anchor?.samples?.filter(sample => Number.isFinite(sample.x) && Number.isFinite(sample.y)) ?? [];
    if (!samples.length) return { ...fallback };
    let before = samples[0], after = samples[samples.length - 1];
    for (const sample of samples) {
        if (sample.frame <= frame) before = sample;
        if (sample.frame >= frame) { after = sample; break; }
    }
    if (anchor.interpolation !== 'linear' || after.frame <= before.frame) return { x: before.x, y: before.y };
    const t = bounded((frame - before.frame) / (after.frame - before.frame), 0, 1, 0);
    return { x: before.x + (after.x - before.x) * t, y: before.y + (after.y - before.y) * t };
}

function lookup(hostOrId) {
    return typeof hostOrId === 'string' ? instances.get(hostOrId) : hostOrId ? states.get(hostOrId) : undefined;
}

function stopFrame(state) {
    if (state.raf) cancelAnimationFrame(state.raf);
    state.raf = 0; state.lastWall = null;
    state.host.dataset.layerRunning = 'false';
}

function clearEntities(state) {
    for (const entity of state.entities) entity.node.remove();
    state.entities = []; state.item = null;
    state.host.dataset.layerEntities = '0';
}

function reset(state) {
    stopFrame(state); clearEntities(state);
    state.actor = null; state.cycle = -1; state.fired.clear(); state.world = 0;
    state.host.dataset.layerFrame = '0'; state.host.dataset.layerCycle = '0';
}

function getClock(state) {
    const actor = state.host.querySelector('.avatar-sheet[data-avatar]');
    if (!actor) return null;
    if (actor !== state.actor) { reset(state); state.actor = actor; }
    const animation = actor.getAnimations()[0];
    if (!animation) return null;
    const fps = bounded(actor.dataset.fps, 1, 60, 10);
    const frames = bounded(actor.dataset.frames, 1, 32, 8);
    const phase = frameAt(animation.currentTime, fps, frames, actor.dataset.loop === 'true');
    return { ...phase, actor, animation, fps, frames };
}

function actionConfig(state) { return state.events?.actions?.[state.options.action] ?? {}; }
function actionEvents(state) { return (actionConfig(state).events ?? []).filter(event => visibleEvents.has(event.type)); }
function anchorAt(state, name, frame) {
    const anchor = actionConfig(state).anchors?.[name] ?? state.events.defaultAnchors?.[name];
    return sampleAnchor(anchor, frame, state.events.coordinateSystem?.origin ?? { x: 256, y: 448 });
}

function makeImage(state, role, asset, animated = false) {
    const src = safeAsset(state.options.assetRoot, asset.standalone ?? asset.file);
    if (!src) return null;
    const node = document.createElement(animated ? 'span' : 'img');
    node.dataset.layer = role; node.dataset.entityId = asset.id;
    node.setAttribute('aria-hidden', 'true');
    if (animated) {
        const image = document.createElement('img'); image.alt = ''; image.draggable = false; image.src = src;
        image.style.width = (number(asset.animationFrames, Array.isArray(asset.frames) ? asset.frames.length : asset.frames) || 1) * 100 + '%';
        node.append(image);
    } else { node.alt = ''; node.draggable = false; node.src = src; }
    node.style.zIndex = role === 'effect' ? '1' : '4';
    state.overlay.append(node);
    return node;
}

function transform(state, entity, position, scaleFactor = 1) {
    const scale = state.options.size / state.coordinateSize;
    const asset = entity.asset;
    const width = bounded(asset.naturalSize?.width ?? asset.width, 8, 2048, 64) * bounded(asset.scale, .1, 4, 1) * scaleFactor;
    const height = bounded(asset.naturalSize?.height ?? asset.height, 8, 2048, 64) * bounded(asset.scale, .1, 4, 1) * scaleFactor;
    const px = bounded(asset.pivot?.x, 0, 1, .5), py = bounded(asset.pivot?.y, 0, 1, .5);
    entity.node.style.width = width * scale + 'px'; entity.node.style.height = height * scale + 'px';
    entity.node.style.left = (position.x - width * px) * scale + 'px';
    entity.node.style.top = (position.y - height * py) * scale + 'px';
    entity.node.dataset.worldX = position.x.toFixed(2); entity.node.dataset.worldY = position.y.toFixed(2);
}

function ensureItem(state, attached) {
    const asset = state.assets.items.find(item => item.id === state.options.itemId);
    if (!asset) return;
    if (!state.item || state.item.asset.id !== asset.id) {
        if (state.item) { state.item.node.remove(); state.entities = state.entities.filter(entity => entity !== state.item); }
        const node = makeImage(state, 'item', asset);
        if (!node) return;
        state.item = { role: 'item', asset, node, attached, anchor: 'right_hand', born: state.world };
        state.entities.push(state.item);
    }
    state.item.attached = attached;
    state.item.node.dataset.attached = String(attached);
}

function trigger(state, event, phase) {
    const payload = event.payload ?? {};
    const born = state.world - Math.max(0, phase.localTime - number(event.frame) * 1000 / phase.fps);
    if (event.type === 'attach_item') { ensureItem(state, true); if (state.item) state.item.anchor = payload.anchor ?? 'right_hand'; return; }
    if (state.entities.length >= 16) return;
    if (event.type === 'spawn_projectile') {
        const asset = state.assets.projectiles.find(item => item.id === state.options.projectileId);
        if (!asset) return;
        const node = makeImage(state, 'projectile', asset, true);
        if (!node) return;
        const position = anchorAt(state, payload.anchor ?? 'muzzle', event.frame);
        state.entities.push({ role: 'projectile', asset, node, position, born,
            lifetime: bounded(asset.lifetimeMs, 100, 5000, 1400), speed: bounded(asset.speed, 10, 2000, 460) });
    } else if (event.type === 'spawn_effect') {
        const asset = state.assets.effects.find(item => item.id === payload.effectSlot);
        if (!asset) return;
        const node = makeImage(state, 'effect', asset);
        if (!node) return;
        const anchor = payload.anchor ?? 'root';
        state.entities.push({ role: 'effect', asset, node, anchor, position: anchorAt(state, anchor, phase.fractional), born,
            follow: (payload.attachment ?? asset.attachment) === 'follow_anchor', lifetime: bounded(asset.durationMs, 100, 5000, 850) });
    }
}

function renderEntities(state, phase) {
    const alive = [];
    for (const entity of state.entities) {
        const age = Math.max(0, state.world - entity.born);
        if (entity.role === 'item') {
            const attachEvent = actionEvents(state).find(event => event.type === 'attach_item');
            const frame = entity.attached ? phase.fractional : number(attachEvent?.frame, 4);
            const point = anchorAt(state, entity.anchor, frame);
            transform(state, entity, { x: point.x + number(entity.asset.gripOffset?.x), y: point.y + number(entity.asset.gripOffset?.y) });
        } else if (entity.role === 'projectile') {
            const point = { x: entity.position.x + entity.speed * age / 1000, y: entity.position.y };
            const viewport = state.host.closest('.avatar-preview-canvas') ?? state.host.parentElement;
            const hostRect = state.host.getBoundingClientRect(), viewportRect = viewport?.getBoundingClientRect();
            const rightEdge = viewportRect && hostRect.width > 0 ? (viewportRect.right - hostRect.left) / (state.options.size / state.coordinateSize) : state.coordinateSize;
            if (age >= entity.lifetime || point.x - number(entity.asset.width, 128) / 2 > rightEdge) { entity.node.remove(); continue; }
            transform(state, entity, point);
            const frames = bounded(entity.asset.animationFrames ?? (Array.isArray(entity.asset.frames) ? entity.asset.frames.length : entity.asset.frames), 1, 16, 1);
            const frame = Math.floor(age * bounded(entity.asset.fps, 1, 60, 12) / 1000) % frames;
            entity.node.firstElementChild.style.transform = `translateX(${-frame * 100 / frames}%)`;
            entity.node.dataset.frame = String(frame);
        } else {
            if (age >= entity.lifetime) { entity.node.remove(); continue; }
            const progress = age / entity.lifetime;
            transform(state, entity, entity.follow ? anchorAt(state, entity.anchor, phase.fractional) : entity.position,
                .84 + .16 * Math.min(1, progress * 5));
            entity.node.style.opacity = String(Math.min(1, progress * 10 + .2) * Math.min(1, (1 - progress) * 4));
        }
        alive.push(entity);
    }
    state.entities = alive;
    state.host.dataset.layerEntities = String(alive.length);
}

function canRun(state) { return !state.disposed && state.ready && state.options.enabled && !state.options.paused && !state.media.matches && !document.hidden && state.host.isConnected; }

function tick(state, timestamp) {
    state.raf = 0;
    if (!state.host.isConnected) { dispose(state.id); return; }
    if (!canRun(state)) { stopFrame(state); return; }
    const phase = getClock(state);
    if (!phase) { stopFrame(state); return; }
    if (state.lastWall !== null) state.world += Math.max(0, timestamp - state.lastWall);
    state.lastWall = timestamp;
    const events = actionEvents(state);
    if (state.cycle !== phase.cycle) { state.cycle = phase.cycle; state.fired.clear(); }
    if (events.some(event => event.type === 'attach_item') && !state.item) ensureItem(state, false);
    for (let index = 0; index < events.length; index++) {
        const event = events[index];
        if (number(event.frame) <= phase.frame && !state.fired.has(index)) {
            state.fired.add(index); trigger(state, event, phase);
        }
    }
    renderEntities(state, phase);
    state.host.dataset.layerFrame = String(phase.frame);
    state.host.dataset.layerCycle = String(phase.cycle);
    const moving = state.entities.some(entity => entity.role !== 'item');
    if ((!phase.finished && events.length) || moving) schedule(state);
    else stopFrame(state);
}

function schedule(state) {
    if (!state.raf && canRun(state)) {
        state.raf = requestAnimationFrame(time => tick(state, time));
        state.host.dataset.layerRunning = 'true';
    }
}

function validOptions(options) { return options && roots.has(options.assetRoot) && typeof options.instanceId === 'string'; }
function signature(options) { return `${options.assetRoot}:${options.avatarId}:${options.action}:${options.replayKey}`; }

function skipPastTransientEvents(state) {
    if (!state.ready || state.disposed) return;
    const phase = getClock(state);
    if (!phase) return;
    state.cycle = phase.cycle;
    actionEvents(state).forEach((event, index) => {
        if (number(event.frame) > phase.frame) return;
        state.fired.add(index);
        if (event.type === 'attach_item') ensureItem(state, true);
    });
    renderEntities(state, phase);
}

export async function mount(host, options) {
    if (!host || !validOptions(options)) return false;
    dispose(options.instanceId);
    const overlay = host.querySelector('[data-avatar-world-overlay]');
    if (!overlay) return false;
    const state = { id: options.instanceId, host, overlay, options: { ...options }, ready: false, disposed: false,
        abort: new AbortController(), raf: 0, lastWall: null, world: 0, entities: [], item: null, actor: null, cycle: -1, fired: new Set(), media: matchMedia('(prefers-reduced-motion: reduce)') };
    states.set(host, state); instances.set(state.id, state);
    host.dataset.layerStatus = 'loading'; host.dataset.layerRunning = 'false';
    state.onVisibility = () => { stopFrame(state); if (!document.hidden) { skipPastTransientEvents(state); schedule(state); } };
    state.onMedia = () => { reset(state); if (!state.media.matches) { skipPastTransientEvents(state); schedule(state); } };
    document.addEventListener('visibilitychange', state.onVisibility);
    state.media.addEventListener('change', state.onMedia);
    try {
        const read = async file => { const response = await fetch(options.assetRoot + '/' + file, { signal: state.abort.signal, credentials: 'same-origin' }); if (!response.ok) throw new Error('Layer assets unavailable'); return response.json(); };
        const [assets, events] = await Promise.all([read('external-assets.json'), read('attachment-events.json')]);
        if (state.disposed) return false;
        if (assets.schemaVersion !== 1 || events.schemaVersion !== 1 || events.avatarId !== options.avatarId
            || !Array.isArray(assets.items) || !Array.isArray(assets.projectiles) || !Array.isArray(assets.effects)) throw new Error('Invalid layer metadata');
        state.assets = assets; state.events = events; state.coordinateSize = bounded(events.coordinateSystem?.width ?? assets.frameSize?.width, 64, 2048, 512);
        state.ready = true; host.dataset.layerStatus = 'ready';
        update(host, state.options);
        return true;
    } catch {
        if (!state.disposed) { host.dataset.layerStatus = 'error'; stopFrame(state); }
        return false;
    }
}

export function update(host, options) {
    const state = lookup(host) ?? lookup(options?.instanceId);
    if (!state || state.disposed || !validOptions(options)) return;
    const previous = state.options;
    const changed = signature(previous) !== signature(options);
    state.options = { ...options, size: bounded(options.size, 24, 512, 248) };
    if (changed) reset(state);
    if (!state.ready) return;
    if (!options.enabled || state.media.matches) { reset(state); return; }
    const phase = getClock(state);
    if (!previous.enabled && options.enabled && !changed) skipPastTransientEvents(state);
    if (phase && state.item && previous.itemId !== options.itemId) {
        const attached = state.item.attached; ensureItem(state, attached); renderEntities(state, phase);
    }
    if (options.paused) { stopFrame(state); if (phase) renderEntities(state, phase); return; }
    schedule(state);
}

export function dispose(hostOrId) {
    const state = lookup(hostOrId);
    if (!state || state.disposed) return;
    state.disposed = true; state.abort.abort(); stopFrame(state); clearEntities(state);
    document.removeEventListener('visibilitychange', state.onVisibility);
    state.media.removeEventListener('change', state.onMedia);
    state.host.dataset.layerStatus = 'disposed';
    instances.delete(state.id); states.delete(state.host);
}
