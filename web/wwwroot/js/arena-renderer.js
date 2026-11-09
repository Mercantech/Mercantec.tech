import { ArenaEffectArt, combatPresentation, combatStatusFor, effectAvatarForEvent, projectileArtFor, repairReadinessFor } from './arena-effect-art.js?v=3';
import { isOriginalAvatar, originalAvatarAssets, originalAvatarFace, originalAvatarFrame, originalAvatarKey, originalAvatarSource } from './arena-avatar-art.js?v=2';

const WIDTH = 1280;
const HEIGHT = 720;
const ART_ROOT = '/assets/arena/v2/';
const COLORS = ['#ff647c', '#64b5ff', '#ffc766', '#b297ff'];
const AVATARS = ['starter', 'bip01', 'raymond', 'santa'];
const STAGES = ['rooftops', 'workshop', 'skyline'];
const clamp = (value, low, high) => Math.max(low, Math.min(high, value));
const lerp = (a, b, amount) => a + (b - a) * amount;
const loaded = image => Boolean(image?.complete && image.naturalWidth && !image.failed);

function panel(context, x, y, width, height, radius, fill, stroke) {
    context.beginPath();
    context.roundRect(x, y, width, height, radius);
    if (fill) { context.fillStyle = fill; context.fill(); }
    if (stroke) { context.strokeStyle = stroke; context.stroke(); }
}

function label(context, value, x, y, size, color, weight = 600, align = 'center') {
    context.font = weight + ' ' + size + 'px "Space Grotesk", "DM Sans", sans-serif';
    context.fillStyle = color;
    context.textAlign = align;
    context.textBaseline = 'middle';
    context.fillText(value, x, y);
}

function hexColor(value, fallback) {
    return /^#[0-9a-f]{6}$/i.test(value || '') ? value : fallback;
}

function rgb(hex) {
    return [1, 3, 5].map(index => parseInt(hex.slice(index, index + 2), 16));
}

function hue(red, green, blue) {
    const max = Math.max(red, green, blue), min = Math.min(red, green, blue), range = max - min;
    if (!range) return 0;
    const sector = max === red ? ((green - blue) / range) % 6 : max === green ? (blue - red) / range + 2 : (red - green) / range + 4;
    return (sector * 60 + 360) % 360;
}

/** Raster-only stage and character presentation. Simulation coordinates never change here. */
export class ArenaRenderer {
    constructor(canvas, options, cue) {
        this.canvas = canvas;
        this.context = canvas.getContext('2d', { alpha: false });
        this.options = options;
        this.cue = cue || (() => {});
        this.stages = options.stages;
        this.stageId = this.stages[0].id;
        this.avatar = options.selectedAvatarId || 'starter';
        this.images = new Map();
        this.imagePromises = new Map();
        this.failures = new Set();
        this.effectArt = new ArenaEffectArt(this.loadImage.bind(this), this.images);
        this.frameFighters = new Map();
        this.platformFrames = [];
        this.effectFrames = [];
        this.materialVariants = new Map();
        this.originalAnimationTimelines = new Map();
        this.seenEvents = new Set();
        this.effects = [];
        this.previewPlayers = null;
        this.camera = { x: 640, y: 360, zoom: 1 };
        this.shake = 0;
        this.currentTick = 0;
        this.loading = true;
        this.disposed = false;
        this.reducedMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
        this.preloadPromise = this.loadArt();
    }

    get ready() { return !this.loading && !this.failures.size; }
    get assetsFailed() { return Array.from(this.failures); }
    preload() { return this.preloadPromise; }

    async loadArt() {
        const art = [
            ...STAGES.map(id => ['stage-' + id, 'stage-' + id + '.webp']),
            ['fighter-starter', 'fighters-starter.webp'],
            ['portrait-starter', 'portrait-starter.webp'],
            ['fighter-starter-long', 'fighters-starter-long.webp'],
            ['fighter-starter-cap', 'fighters-starter-cap.webp'],
            ['platforms', 'platform-atlas.webp']

        ];
        await Promise.all([
            this.effectArt.preload(),
            ...art.map(([key, file]) => this.loadImage(key, ART_ROOT + file)),
            ...originalAvatarAssets().map(([key, file]) => this.loadImage(key, file)),
            ...AVATARS.filter(isOriginalAvatar).map(id => this.loadImage('portrait-' + id, originalAvatarFace(id)))
        ]);
        if (this.disposed) return false;
        const platforms = this.images.get('platforms');
        if (loaded(platforms)) this.platformFrames = this.splitAndTrim(platforms, 1, 3, { alpha: 64, surfaceCoverage: .22 });
        this.loading = false;
        return this.ready;
    }

    loadImage(key, source) {
        if (this.imagePromises.has(key)) return this.imagePromises.get(key);
        const image = new Image();
        image.decoding = 'async';
        this.images.set(key, image);
        const promise = new Promise(resolve => {
            image.onload = () => resolve(image);
            image.onerror = () => {
                image.failed = true;
                this.failures.add(source);
                console.warn('Arena artwork could not load:', source);
                resolve(null);
            };
        });
        this.imagePromises.set(key, promise);
        image.src = source;
        return promise;
    }

    // Retained for callers that request a character image by avatar/action.
    load(avatarId, action = 'idle') {
        if (!isOriginalAvatar(avatarId)) return this.images.get('fighter-' + avatarId) || null;
        const key = originalAvatarKey(avatarId, action);
        if (!this.images.has(key)) this.loadImage(key, originalAvatarSource(avatarId, action));
        const image = this.images.get(key);
        return loaded(image) ? image : this.images.get(originalAvatarKey(avatarId, 'idle')) || null;
    }

    splitAndTrim(image, columns, rows, options = {}) {
        const canvas = document.createElement('canvas');
        canvas.width = image.naturalWidth;
        canvas.height = image.naturalHeight;
        const context = canvas.getContext('2d', { willReadFrequently: true });
        context.drawImage(image, 0, 0);
        const pixels = context.getImageData(0, 0, canvas.width, canvas.height).data;
        const frameWidth = canvas.width / columns, frameHeight = canvas.height / rows;
        const frames = [];
        for (let row = 0; row < rows; row++) {
            for (let column = 0; column < columns; column++) {
                const left = Math.round(column * frameWidth), top = Math.round(row * frameHeight);
                const right = Math.round((column + 1) * frameWidth), bottom = Math.round((row + 1) * frameHeight);
                let minX = right, minY = bottom, maxX = left, maxY = top;
                let scanTop = top;
                const alpha = options.alpha || 12;
                if (options.surfaceCoverage) {
                    for (let y = top; y < bottom; y++) {
                        let covered = 0;
                        for (let x = left; x < right; x++) if (pixels[(y * canvas.width + x) * 4 + 3] >= alpha) covered++;
                        if (covered >= frameWidth * options.surfaceCoverage) { scanTop = y; break; }
                    }
                }
                for (let y = scanTop; y < bottom; y++) {
                    for (let x = left; x < right; x++) {
                        if (pixels[(y * canvas.width + x) * 4 + 3] < alpha) continue;
                        minX = Math.min(minX, x); maxX = Math.max(maxX, x);
                        minY = Math.min(minY, y); maxY = Math.max(maxY, y);
                    }
                }
                frames.push(maxX >= minX && maxY >= minY
                    ? { x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1 }
                    : { x: left, y: top, width: frameWidth, height: frameHeight });
            }
        }
        return frames;
    }

    reset() {
        this.seenEvents.clear();
        this.effects.length = 0;
        this.shake = 0;
        this.currentTick = 0;
        this.originalAnimationTimelines.clear();
        this.camera = { x: 640, y: 360, zoom: 1 };
    }

    preview(stageId, avatarId, players = null) {
        this.stageId = stageId;
        this.avatar = avatarId;
        this.previewPlayers = players;
        this.reset();
    }

    dispose() {
        this.disposed = true;
        this.images.clear();
        this.imagePromises.clear();
        this.materialVariants.clear();
        this.originalAnimationTimelines.clear();
        this.effects.length = 0;
    }

    events(events) {
        for (const event of events) {
            if (this.seenEvents.has(event.id)) continue;
            this.seenEvents.add(event.id);
            if (this.seenEvents.size > 600) this.seenEvents.delete(this.seenEvents.values().next().value);
            if (event.tick < this.currentTick - 90) continue;
            this.cue(event.type, event.strength);
            const strength = clamp(event.strength || 1, .5, 2);
            const effect = { x: event.x, y: event.y, age: 0, rotation: 0, strength, type: event.type, actorId: event.actorId, targetId: event.targetId };
            switch (event.type) {
                case 'hit': case 'throw':
                    this.effects.push({ ...effect, frames: [0, 1, 2, 3], duration: .32, size: 90 * strength, grow: 1.5 });
                    this.shake = Math.max(this.shake, 2 + 2 * strength);
                    break;
                case 'ko':
                    this.effects.push({ ...effect, frames: [15], duration: .9, size: 180, grow: 3 });
                    this.shake = Math.max(this.shake, 9);
                    break;
                case 'shield': case 'parry': case 'break':
                    this.effects.push({ ...effect, frames: event.type === 'break' ? [0, 1, 2, 3] : [8, 9, 10, 11], duration: .3, size: event.type === 'break' ? 180 : 105, grow: 1.35 });
                    break;
                case 'respawn': case 'ledge':
                    this.effects.push({ ...effect, y: event.y - 35, frames: [14], duration: .7, size: 125, grow: 1.6 });
                    break;
                case 'jump': case 'land':
                    this.effects.push({ ...effect, frames: [12], duration: .35, size: 70, grow: 1.5, floor: true });
                    break;
                case 'dodge': case 'recovery':
                    this.effects.push({ ...effect, frames: [13], duration: .27, size: 100, grow: 1.6 });
                    break;
                case 'gift-grab': case 'magnet': case 'capture': case 'escape': case 'release':
                    this.effects.push({ ...effect, frames: [0], effectId: 'magnet_field', duration: .35, size: event.type === 'magnet' ? 170 : 105, grow: 1.3 });
                    break;
                case 'absorb': case 'reflect':
                    this.effects.push({ ...effect, frames: [0], effectId: event.type === 'reflect' ? 'shield' : 'unlock', duration: .24, size: 110, grow: 1.25 });
                    break;
                case 'armor':
                    this.effects.push({ ...effect, frames: [0], effectId: 'shield', duration: .25, size: 110, grow: 1.15 });
                    break;
                case 'repair-start': case 'repair': case 'repair-cancel':
                    this.effects.push({ ...effect, frames: [0], effectId: event.type === 'repair-cancel' ? 'impact' : 'repair_field', duration: .4, size: event.type === 'repair' ? 150 : 90, grow: 1.3 });
                    break;
                case 'ultimate-charge':
                    this.effects.push({ ...effect, frames: [0], effectId: 'ultimate_charge', duration: .4, size: 140, grow: 1.1 });
                    break;
                case 'gift-fest': case 'overload': case 'join-explode':
                    this.effects.push({ ...effect, frames: [0], effectId: 'ultimate_release', duration: .58, size: event.type === 'overload' ? 340 : 300, grow: 1.35 });
                    this.shake = Math.max(this.shake, this.reducedMotion ? 0 : 6);
                    break;
                case 'join':
                    this.effects.push({ ...effect, frames: [0], effectId: 'ultimate_release', duration: .26, size: 145, grow: 1.2 });
                    break;
                // Pulls are continuous simulation events. The persistent field and
                // tether show them without accumulating flashes on every tick.
            }
        }
        if (this.effects.length > 160) this.effects.splice(0, this.effects.length - 160);
    }

    draw(now, deltaTime, state, previous, receivedAt, previousAt, playerId, practice) {
        const context = this.context;
        if (this.disposed || !context || this.canvas.width < 2 || this.canvas.height < 2) return;
        context.setTransform(this.canvas.width / WIDTH, 0, 0, this.canvas.height / HEIGHT, 0, 0);
        context.imageSmoothingEnabled = true;
        context.imageSmoothingQuality = 'high';
        context.fillStyle = '#071322';
        context.fillRect(0, 0, WIDTH, HEIGHT);
        if (!this.ready) {
            label(context, this.loading ? 'Indlæser' : 'Grafikken kunne ikke indlæses', 640, 360, 18, '#c6d3e4');
            return;
        }
        const stage = this.stages.find(stage => stage.id === (state?.stageId || this.stageId)) || this.stages[0];
        this.background(context, stage);
        let fighters = state?.fighters || this.previewFighters(stage, now);
        if (state && previous && state.tick >= previous.tick) {
            const interval = Math.max(1, receivedAt - previousAt);
            const amount = clamp((now - receivedAt) / interval, 0, 1);
            fighters = fighters.map(fighter => {
                const before = previous.fighters.find(item => item.id === fighter.id);
                if (!before || Math.abs(before.x - fighter.x) > 180 || Math.abs(before.y - fighter.y) > 200 || before.stocks !== fighter.stocks) return fighter;
                const own = fighter.id === playerId;
                const lead = Math.min(.026, Math.max(0, now - receivedAt) / 1000);
                return { ...fighter,
                    x: own ? fighter.x + fighter.vx * lead : lerp(before.x, fighter.x, amount),
                    y: own ? fighter.y + fighter.vy * lead : lerp(before.y, fighter.y, amount) };
            });
        }
        this.frameFighters = new Map(fighters.map(fighter => [fighter.id, fighter]));
        if (state) {
            this.currentTick = state.tick;
            this.updateCamera(fighters, deltaTime);
        }
        context.save();
        context.translate(WIDTH / 2, HEIGHT / 2);
        context.scale(this.camera.zoom, this.camera.zoom);
        context.translate(-this.camera.x, -this.camera.y);
        if (!this.reducedMotion && this.shake > .1) {
            context.translate(Math.sin(now * .049) * this.shake, Math.cos(now * .057) * this.shake * .55);
        }
        this.shake *= Math.pow(.03, deltaTime);
        this.platforms(context, stage);
        for (const projectile of state?.projectiles || []) this.projectile(context, projectile, now);
        for (const fighter of fighters) {
            if (fighter.out || fighter.stocks <= 0) continue;
            this.fighter(context, fighter, now, playerId, state?.phase);
        }
        this.drawEffects(context, deltaTime);
        context.restore();
        if (!state) return;
        this.hud(context, state, playerId);
        if (state.phase === 'countdown') {
            const number = Math.ceil(state.countdown);
            context.save();
            context.shadowColor = '#05101b'; context.shadowBlur = 26;
            label(context, number > 0 ? String(number) : 'KAMP', 640, 285, 114, '#f4f6f8', 700);
            context.restore();
        }
        if (state.suddenDeath && state.phase !== 'finished') {
            panel(context, 535, 75, 210, 32, 5, '#351a31df', '#ffb6c366');
            label(context, 'Sudden death', 640, 91, 14, '#ffe2e7');
        }
        if (state.phase === 'playing' && now - receivedAt > 2500 && !practice) {
            panel(context, 460, 314, 360, 65, 10, '#081727ea', '#8da6c633');
            label(context, 'Forbinder…', 640, 347, 21, '#e5edf5');
        }
    }

    updateCamera(fighters, deltaTime) {
        const active = fighters.filter(fighter => !fighter.out && fighter.stocks > 0);
        let minX = 250, maxX = 1030, minY = 80, maxY = 650;
        for (const fighter of active) {
            minX = Math.min(minX, fighter.x - 110); maxX = Math.max(maxX, fighter.x + 110);
            minY = Math.min(minY, fighter.y - 170); maxY = Math.max(maxY, fighter.y + 130);
        }
        const easing = 1 - Math.exp(-4 * deltaTime);
        const zoom = clamp(Math.min(WIDTH / (maxX - minX), HEIGHT / (maxY - minY)), .65, 1);
        this.camera.x = lerp(this.camera.x, clamp((minX + maxX) / 2, 420, 860), easing);
        this.camera.y = lerp(this.camera.y, clamp((minY + maxY) / 2, 250, 470), easing);
        this.camera.zoom = lerp(this.camera.zoom, zoom, easing);
    }

    background(context, stage) {
        const image = this.images.get('stage-' + stage.id);
        if (!loaded(image)) return;
        // The environment stays behind the collision plane, with only a small camera parallax.
        const scale = Math.max(WIDTH / image.naturalWidth, HEIGHT / image.naturalHeight) * 1.04;
        const width = image.naturalWidth * scale, height = image.naturalHeight * scale;
        const x = (WIDTH - width) / 2 - (this.camera.x - 640) * .06;
        const y = (HEIGHT - height) / 2 - (this.camera.y - 360) * .035;
        context.drawImage(image, x, y, width, height);
        context.fillStyle = '#06142635';
        context.fillRect(0, 0, WIDTH, HEIGHT);
        const shade = context.createLinearGradient(0, 0, 0, HEIGHT);
        shade.addColorStop(0, '#0717293b'); shade.addColorStop(.5, '#07172900'); shade.addColorStop(1, '#07172970');
        context.fillStyle = shade; context.fillRect(0, 0, WIDTH, HEIGHT);
    }

    previewFighters(stage, now) {
        const surfaces = stage.platforms.filter(platform => !platform.oneWay);
        const players = this.previewPlayers || [
            { id: 'preview1', avatarId: this.avatar, name: '', color: COLORS[0], appearance: this.options.appearance },
            { id: 'preview2', avatarId: this.avatar === 'raymond' ? 'bip01' : 'raymond', name: '', color: COLORS[1] }
        ];
        return players.map((player, index) => {
            const surface = surfaces[index % surfaces.length];
            const proportion = surfaces.length === 1 ? .3 + .4 * index / Math.max(1, players.length - 1) : .5;
            return { ...player, x: surface.x + surface.width * proportion, y: surface.y,
                facing: index % 2 === 0 ? 1 : -1, action: 'idle', actionFrame: Math.floor(now / 150) % 8,
                stocks: 3, damage: 0, shield: 100, grounded: true, invulnerable: false, shielding: false, out: false };
        });
    }

    platforms(context, stage) {
        const atlas = this.images.get('platforms');
        const row = STAGES.indexOf(stage.id);
        const frame = this.platformFrames[Math.max(0, row)];
        if (!loaded(atlas) || !frame) return;
        for (const platform of stage.platforms) {
            const height = platform.oneWay ? 34 : 72;
            const cap = Math.min(frame.width * .18, frame.height * .85);
            const targetCap = Math.min(platform.width * .2, cap * height / frame.height);
            const middleSource = frame.width - cap * 2;
            const middleHeightScale = height / frame.height;
            const naturalTileWidth = Math.max(30, middleSource * middleHeightScale);
            const middleWidth = platform.width - targetCap * 2;
            context.save();
            context.shadowColor = '#020b16aa'; context.shadowBlur = 16; context.shadowOffsetY = 9;
            context.drawImage(atlas, frame.x, frame.y, cap, frame.height,
                platform.x, platform.y, targetCap, height);
            context.shadowBlur = 0; context.shadowOffsetY = 0;
            for (let offset = 0; offset < middleWidth; offset += naturalTileWidth) {
                const tileWidth = Math.min(naturalTileWidth, middleWidth - offset);
                context.drawImage(atlas, frame.x + cap, frame.y, middleSource * tileWidth / naturalTileWidth, frame.height,
                    platform.x + targetCap + offset, platform.y, tileWidth, height);
            }
            context.drawImage(atlas, frame.x + frame.width - cap, frame.y, cap, frame.height,
                platform.x + platform.width - targetCap, platform.y, targetCap, height);
            context.restore();
        }
    }

    animationFrame(fighter, now) {
        const action = fighter.action || 'idle';
        const phase = clamp(fighter.actionFrame || 0, 0, 7);
        if (action === 'run' || action === 'walk') return 4 + Math.floor(now / 75) % 4;
        if (action === 'idle' || action === 'think' || action === 'talk') return Math.floor(now / 180) % 4;
        if (action === 'jump') return fighter.vy < -40 ? 8 : 9;
        if (action === 'land') return 11;
        if (action === 'hit' || action === 'knockout') return 14;
        if (action === 'celebrate' || action === 'wave') return 15;
        if (action === 'ultimate_charge') return 12;
        if (action === 'ability_4_dash' || action === 'respawn') return 10;
        if (action === 'ability_3_shield' || action === 'ability_5_repair' || action === 'interact') return 12;
        return phase < 2 ? 12 : 13;
    }

    appearanceImage(fighter, playerId) {
        const appearance = fighter.appearance || (fighter.id === playerId || fighter.id === 'local' || fighter.id === 'preview1' ? this.options.appearance : null);
        const variant = fighter.avatarId === 'starter' && ['long', 'cap'].includes(appearance?.hairStyle) ? 'fighter-starter-' + appearance.hairStyle : null;
        const source = variant ? this.images.get(variant) : this.load(fighter.avatarId, fighter.action);
        if (fighter.avatarId !== 'starter' || !loaded(source)) return source;
        if (!appearance) return source;
        const skin = hexColor(appearance.skin, '#c98d69');
        const hair = hexColor(appearance.hair, '#42302a');
        const outfit = hexColor(appearance.outfit, '#2452e8');
        if (skin.toLowerCase() === '#c98d69' && hair.toLowerCase() === '#42302a' && outfit.toLowerCase() === '#2452e8') return source;
        const key = [skin, hair, outfit, appearance.hairStyle].join(':');
        if (this.materialVariants.has(key)) return this.materialVariants.get(key);
        const canvas = document.createElement('canvas');
        canvas.width = source.naturalWidth; canvas.height = source.naturalHeight;
        const context = canvas.getContext('2d', { willReadFrequently: true });
        context.drawImage(source, 0, 0);
        const image = context.getImageData(0, 0, canvas.width, canvas.height);
        const materials = { skin: { color: rgb(skin), light: .55 }, hair: { color: rgb(hair), light: .19 }, outfit: { color: rgb(outfit), light: .31 } };
        for (let index = 0; index < image.data.length; index += 4) {
            if (image.data[index + 3] < 12) continue;
            const red = image.data[index] / 255, green = image.data[index + 1] / 255, blue = image.data[index + 2] / 255;
            const value = Math.max(red, green, blue), min = Math.min(red, green, blue);
            const saturation = value ? (value - min) / value : 0;
            const colorHue = hue(red, green, blue);
            let material;
            if (colorHue >= 190 && colorHue <= 265 && saturation > .28) material = materials.outfit;
            else if (colorHue >= 5 && colorHue <= 48 && saturation > .24 && value < .42) material = materials.hair;
            else if (colorHue >= 6 && colorHue <= 44 && saturation > .22 && value >= .42 && value < .96) material = materials.skin;
            if (!material) continue;
            const luminance = .2126 * red + .7152 * green + .0722 * blue;
            const shade = clamp(luminance / material.light, .22, 2.4);
            for (let channel = 0; channel < 3; channel++) image.data[index + channel] = clamp(material.color[channel] * shade, 0, 255);
        }
        context.putImageData(image, 0, 0);
        if (this.materialVariants.size >= 20) this.materialVariants.delete(this.materialVariants.keys().next().value);
        this.materialVariants.set(key, canvas);
        return canvas;
    }

    fighter(context, fighter, now, playerId, phase) {
        const image = this.appearanceImage(fighter, playerId);
        if (!image || (!image.width && !image.naturalWidth)) return;
        const original = originalAvatarFrame(fighter, now, this.originalAnimationTimelines);
        const frame = original ? original.frame : this.animationFrame(fighter, now);
        const sourceWidth = (image.naturalWidth || image.width) / 4;
        const sourceHeight = (image.naturalHeight || image.height) / (original ? original.rows : 4);
        const x = frame % 4 * sourceWidth, y = Math.floor(frame / 4) * sourceHeight;
        const width = fighter.avatarId === 'raymond' ? 125 : 118;
        const height = width * sourceHeight / sourceWidth;
        const color = fighter.color || COLORS[0];
        const own = fighter.id === playerId;
        context.save();
        context.translate(fighter.x, fighter.y);
        // A flattened copy of the generated sprite supplies its contact shadow.
        if (fighter.grounded) {
            context.save(); context.globalAlpha = .2; context.filter = 'brightness(0) blur(2px)';
            context.drawImage(image, x, y, sourceWidth, sourceHeight, -width * .37, -2, width * .74, 9);
            context.restore();
        }
        context.scale(fighter.facing || 1, 1);
        if (fighter.invulnerable) context.globalAlpha = .55 + Math.sin(now / 55) * .25;
        context.shadowColor = '#031125a0'; context.shadowBlur = 6;
        context.drawImage(image, x, y, sourceWidth, sourceHeight, -width * .5, -height * (original ? original.originY : .9), width, height);
        context.restore();
        if (fighter.shielding) {
            const frame = 8 + Math.floor(now / 110) % 4;
            const size = 82 + clamp(fighter.shield, 0, 100) * .25;
            this.effectTexture(context, frame, fighter.x, fighter.y - 41, size * 1.25, 0, .7, 'screen', 1, fighter.avatarId, 'shield');
        }
        const combat = combatPresentation(fighter);
        if (fighter.charge > 0 && !['overload-charge', 'join-charge', 'gift-fest-charge'].includes(combat?.activeAbility)) {
            this.effectTexture(context, 14, fighter.x, fighter.y - 43, 110 + fighter.charge * 65, 0, .25 + fighter.charge * .5, 'screen', 1, fighter.avatarId, 'ultimate_charge');
        }
        this.combatEffects(context, fighter, combat, now);
        const actionEffects = { ability_1_pulse: 'impact', ability_2_magnet: 'magnet_field', ability_4_dash: 'dash_trail', ability_5_repair: 'repair_field', ultimate_release: 'ultimate_release' };
        const attached = actionEffects[fighter.action];
        // Combat snapshots provide the real lifetime for fields and channels.
        // Animation-only previews retain the authored action highlights.
        const persistent = combat && ['magnet_field', 'repair_field', 'ultimate_release'].includes(attached);
        if (attached && !persistent && fighter.actionFrame >= 2 && fighter.actionFrame <= 5) {
            const radial = ['magnet_field', 'repair_field', 'ultimate_release'].includes(attached);
            this.effectTexture(context, 13, fighter.x + (radial ? 0 : fighter.facing * 32), fighter.y - (attached === 'repair_field' ? 18 : 43),
                attached === 'ultimate_release' ? 185 : radial ? 125 : 95, radial ? 0 : fighter.facing < 0 ? Math.PI : 0, .8, 'screen', 1, fighter.avatarId, attached);
        }
        if (fighter.name && (phase || this.previewPlayers)) {
            const name = (own ? 'Dig' : fighter.name).slice(0, 22);
            const labelWidth = Math.max(46, Math.min(160, name.length * 6.3 + 20));
            panel(context, fighter.x - labelWidth / 2, fighter.y - 137, labelWidth, 22, 5, '#061727cf', color + '60');
            label(context, name, fighter.x, fighter.y - 126, 11, color, 700);
        }
        if (!fighter.connected && !fighter.isBot && phase === 'playing') {
            label(context, 'Forbinder…', fighter.x, fighter.y - 153, 10, '#f2c6a6');
        }
    }

    combatEffects(context, fighter, combat, now) {
        if (!combat) return;
        const pulse = this.reducedMotion ? 1 : .9 + Math.sin(now / 90) * .1;
        const x = fighter.x, y = fighter.y - 43;
        if (combat.activeAbility === 'magnet') {
            this.effectArt.draw(context, 'bip01', 'magnet_field', 0, x + fighter.facing * 62, y, 270, 0, .7 * pulse, 'screen');
        }
        if (combat.channelRemaining > 0 && combat.activeAbility === 'repair') {
            this.effectArt.draw(context, fighter.avatarId, 'repair_field', 0, x, fighter.y - 27, 165, 0, .75 * pulse, 'screen');
        }
        if (combat.armored) {
            this.effectArt.draw(context, fighter.avatarId, 'shield', 0, x, y, 130, 0, .42 * pulse, 'screen');
        }
        if (['overload-charge', 'join-charge', 'gift-fest-charge'].includes(combat.activeAbility)) {
            this.effectArt.draw(context, fighter.avatarId, 'ultimate_charge', 0, x, y, 165, 0, .8 * pulse, 'screen');
        }
        if (combat.capturedBy) {
            const captor = this.frameFighters.get(combat.capturedBy);
            if (captor && !captor.out && captor.stocks > 0) {
                const dx = x - captor.x, dy = y - (captor.y - 43);
                const length = Math.hypot(dx, dy);
                this.effectArt.draw(context, captor.avatarId, 'magnet_field', 0,
                    (x + captor.x) / 2, (y + captor.y - 43) / 2,
                    Math.max(110, length * 1.65), Math.atan2(dy, dx), .8 * pulse, 'screen', .32);
                this.effectArt.draw(context, captor.avatarId, 'magnet_field', 0, x, y, 105, 0, .55 * pulse, 'screen');
            }
        }
    }

    effectTexture(context, index, x, y, size, rotation = 0, alpha = 1, blend = 'source-over', heightScale = 1, avatarId = 'starter', effectId = null) {
        const id = effectId || (index < 4 ? 'impact' : index < 8 ? this.effectArt.projectileId(avatarId) : index < 12 ? 'shield' : index === 12 || index === 13 ? 'dash_trail' : index === 14 ? 'unlock' : 'ultimate_release');
        this.effectArt.draw(context, avatarId, id, index >= 4 && index < 8 ? index - 4 : 0, x, y, size, rotation, alpha, blend, heightScale);
    }

    projectile(context, projectile, now) {
        const art = projectileArtFor(projectile, this.frameFighters);
        const speed = Math.hypot(projectile.vx, projectile.vy);
        const angle = Math.atan2(projectile.vy, projectile.vx);
        const frame = Math.floor(now * 12 / 1000) % 4;
        const size = Math.max(45, (projectile.radius || 9) * 5);
        if (projectile.kind === 'join') {
            this.effectArt.draw(context, 'raymond', 'magnet_field', 0, projectile.x, projectile.y, size * 2.4, 0, .3, 'screen');
        }
        this.effectArt.draw(context, art.avatarId, art.id, frame, projectile.x - projectile.vx * .018,
            projectile.y - projectile.vy * .018, size * 1.3, angle, .3, 'screen');
        this.effectArt.draw(context, art.avatarId, art.id, frame, projectile.x, projectile.y, size, speed ? angle : 0, 1, 'screen');
        if (art.reflected) {
            const owner = this.frameFighters.get(projectile.ownerId);
            this.effectArt.draw(context, owner?.avatarId || 'bip01', 'shield', 0,
                projectile.x, projectile.y, size * 1.15, 0, .4, 'screen');
        }
    }

    drawEffects(context, deltaTime) {
        this.effects = this.effects.filter(effect => effect.age < effect.duration);
        for (const effect of this.effects) {
            effect.age += deltaTime;
            const progress = clamp(effect.age / effect.duration, 0, 1);
            const index = effect.frames[Math.min(effect.frames.length - 1, Math.floor(progress * effect.frames.length))];
            const size = effect.size * lerp(.7, effect.grow, progress);
            const opacity = Math.min(1, (1 - progress) * 2.4);
            this.effectTexture(context, index, effect.x, effect.y, size, effect.rotation,
                opacity, effect.floor ? 'source-over' : 'screen', effect.floor ? .6 : 1, effectAvatarForEvent(effect, this.frameFighters), effect.effectId);
        }
    }

    hud(context, state, playerId) {
        const count = state.fighters.length;
        const width = Math.min(244, (WIDTH - 96 - 14 * (count - 1)) / Math.max(2, count));
        const start = (WIDTH - width * count - 14 * (count - 1)) / 2;
        const height = state.fighters.some(fighter => combatPresentation(fighter)) ? 110 : 78;
        const seconds = Math.max(0, Math.ceil(state.remainingSeconds));
        const clock = Math.floor(seconds / 60) + ':' + String(seconds % 60).padStart(2, '0');
        panel(context, 572, 20, 136, 45, 7, '#071828cf', '#b3c8df33');
        label(context, clock, 640, 43, 28, '#f2f5f8', 700);
        for (let index = 0; index < count; index++) {
            const fighter = state.fighters[index], x = start + index * (width + 14), y = HEIGHT - height - 20;
            const color = fighter.color || COLORS[index % COLORS.length];
            context.save();
            context.globalAlpha = fighter.out ? .38 : 1;
            const fill = context.createLinearGradient(x, y, x, y + height);
            fill.addColorStop(0, '#142c43ee'); fill.addColorStop(1, '#071727f5');
            panel(context, x, y, width, height, 7, fill, '#c3d5e533');
            panel(context, x, y + height - 4, width, 4, [0, 0, 5, 5], color);
            const portrait = this.images.get('portrait-' + fighter.avatarId);
            if (fighter.avatarId === 'starter') {
                const sheet = this.appearanceImage(fighter, playerId);
                const frameWidth = (sheet.naturalWidth || sheet.width) / 4;
                const frameHeight = (sheet.naturalHeight || sheet.height) / 4;
                context.drawImage(sheet, frameWidth * .15, frameHeight * .03, frameWidth * .7, frameHeight * .7, x + 4, y - 11, 78, 78);
            } else if (loaded(portrait)) context.drawImage(portrait, x + 4, y - 11, 78, 78);
            const name = fighter.id === playerId ? 'Dig' : fighter.name;
            label(context, name.length > 18 ? name.slice(0, 17) + '…' : name, x + 82, y + 16, 11, '#c9d6e4', 600, 'left');
            const damageColor = fighter.damage >= 140 ? '#ff8797' : fighter.damage >= 80 ? '#ffc18b' : '#f5f7fa';
            label(context, fighter.out ? 'Ude' : Math.round(fighter.damage) + '%', x + 80, y + 44, 32, damageColor, 700, 'left');
            label(context, '× ' + fighter.stocks, x + 29, y + 64, 11, color, 700);
            if (fighter.shielding) {
                panel(context, x + 82, y + 65, width - 98, 3, 1, '#52677d');
                panel(context, x + 82, y + 65, (width - 98) * fighter.shield / 100, 3, 1, color);
            }
            this.combatHud(context, fighter, combatPresentation(fighter), x, y, width, fighter.id === playerId);
            context.restore();
        }
    }

    combatHud(context, fighter, combat, x, y, width, own) {
        if (!combat) return;
        const magic = fighter.avatarId === 'santa';
        const energy = magic || fighter.avatarId === 'bip01';
        const resource = energy ? combat.energy : clamp(fighter.shield || 0, 0, 100);
        const resourceColor = magic ? '#f2d177' : energy ? '#76e5f2' : '#b6c9e2';
        label(context, (magic ? 'M ' : energy ? 'E ' : 'L ') + Math.round(resource), x + 14, y + 80, 10, resourceColor, 700, 'left');
        panel(context, x + 14, y + 91, 53, 3, 1.5, '#35485c');
        if (resource > 0) panel(context, x + 14, y + 91, 53 * resource / 100, 3, 1.5, resourceColor);
        const ready = combat.ultimateReady && !fighter.out;
        label(context, ready ? 'I KLAR' : 'I ' + Math.floor(combat.ultimate) + '%', x + 82, y + 88, 11, ready ? '#ffdb8b' : '#c9d6e4', 700, 'left');
        panel(context, x + 82, y + 98, width - 98, 3, 1.5, '#35485c');
        if (combat.ultimate > 0) panel(context, x + 82, y + 98, (width - 98) * combat.ultimate / 100, 3, 1.5, ready ? '#ffdb8b' : '#c1a5ff');
        if (!own || fighter.out) return;
        const seconds = value => value > 0 ? value.toFixed(1) + 's' : 'klar';
        const grab = combat.cooldowns.grab > 0 ? seconds(combat.cooldowns.grab) : magic && resource < 20 ? '20M' : energy && resource < 18 ? '18E' : 'klar';
        const repair = repairReadinessFor(fighter, combat);
        label(context, 'U ' + grab + ' · ↓K ' + repair,
            x + 82, y + 71, 10, '#b8cadb', 600, 'left');
        const status = combatStatusFor(fighter, combat);
        if (status) {
            panel(context, x, y - 28, width, 23, 5, '#081b2ce8', (combat.capturedBy ? '#ffc766' : resourceColor) + '66');
            label(context, status, x + width / 2, y - 16, 11, combat.capturedBy ? '#ffdb8b' : resourceColor, 700);
        }
    }
}
