const EFFECT_ROOT = '/assets/arena/v3/effects/';
const EFFECT_IDS = ['shield', 'magnet_field', 'dash_trail', 'repair_field', 'ultimate_charge', 'ultimate_release', 'impact', 'unlock'];
const PROJECTILES = { bip01: ['pulse_cyan', 'pulse_amber'], raymond: ['pulse_blue', 'pulse_gold'], santa: ['pulse_blue', 'pulse_gold'], lupp: ['pulse_blue', 'pulse_gold'] };
const valid = image => Boolean(image?.complete && image.naturalWidth && !image.failed);
const finite = (value, fallback = 0) => Number.isFinite(value) ? value : fallback;
const resource = value => Math.max(0, Math.min(100, finite(value)));

// Reflections change ownership, never the artwork of the original projectile.
// JOIN keeps its gold comet even when BIP sends it back towards Raymond.
export function projectileArtFor(projectile, fighters) {
    const source = projectile.sourceAvatarId || fighters.get(projectile.ownerId)?.avatarId || 'starter';
    const avatarId = projectile.kind === 'join' ? 'raymond' : source;
    const palette = PROJECTILES[avatarId] || PROJECTILES.bip01;
    return {
        avatarId,
        id: ['join', 'gift', 'prototype'].includes(projectile.kind) ? palette[1] : palette[0],
        reflected: projectile.reflected === true || projectile.kind === 'reflected'
    };
}

// Older snapshots and menu previews have no combat resources. Keep their HUD
// unchanged; authoritative resource values never come from an animation frame.
export function combatPresentation(fighter) {
    if (!fighter.combat || (!Object.hasOwn(PROJECTILES, fighter.avatarId) && !fighter.combat.capturedBy)) return null;
    const combat = fighter.combat;
    const cooldowns = Object.fromEntries(['pulse', 'grab', 'dash', 'recovery', 'repair', 'ultimate']
        .map(id => [id, Math.max(0, finite(combat.cooldowns?.[id]))]));
    const ultimate = resource(combat.ultimate);
    return {
        energy: resource(combat.energy), ultimate, cooldowns,
        ultimateReady: ultimate >= 100 && cooldowns.ultimate === 0,
        channelRemaining: Math.max(0, finite(combat.channelRemaining)),
        captureRemaining: Math.max(0, finite(combat.captureRemaining)),
        escapePresses: Math.min(3, Math.max(0, Math.floor(finite(combat.escapePresses)))),
        armored: combat.armored === true,
        capturedBy: typeof combat.capturedBy === 'string' ? combat.capturedBy : null,
        activeAbility: typeof combat.activeAbility === 'string' ? combat.activeAbility : null
    };
}

export function combatStatusFor(fighter, combat = combatPresentation(fighter)) {
    if (!combat) return null;
    if (combat.capturedBy) return combat.captureRemaining > 0
        ? 'Hop fri · ' + combat.captureRemaining.toFixed(1) + 's' : 'Grebet · Hop fri';
    const remaining = combat.channelRemaining > 0 ? ' · ' + combat.channelRemaining.toFixed(1) + 's' : '';
    if (combat.activeAbility === 'overload-charge') return 'Lader Kerneoverload' + remaining;
    if (combat.activeAbility === 'join-charge') return 'Lader JOIN' + remaining;
    if (combat.activeAbility === 'gift-fest-charge') return 'Lader gavefest' + remaining;
    if (combat.activeAbility === 'game-jam-charge') return 'Lader Game Jam' + remaining;
    if (combat.activeAbility === 'repair' && combat.channelRemaining > 0) {
        return (fighter.avatarId === 'lupp' ? 'Playtestpause ' : fighter.avatarId === 'santa' ? 'Julevarme ' : fighter.avatarId === 'bip01' ? 'Reparerer ' : 'Gendanner ') + combat.channelRemaining.toFixed(1) + 's';
    }
    return combat.armored ? 'Panser' : combat.activeAbility === 'magnet' ? 'Magnet' : null;
}

export function repairReadinessFor(fighter, combat = combatPresentation(fighter)) {
    if (!combat) return null;
    if (combat.cooldowns.repair > 0) return combat.cooldowns.repair.toFixed(1) + 's';
    if (fighter.damage <= 0) return 'fuldt';
    if (!fighter.grounded) return 'land';
    const magic = fighter.avatarId === 'santa';
    const ideas = fighter.avatarId === 'lupp';
    const energy = ideas || magic || fighter.avatarId === 'bip01';
    const cost = ideas ? 30 : magic ? 45 : 40;
    if ((energy ? combat.energy : resource(fighter.shield)) < cost) return ideas ? '30 IDÉ' : magic ? '45M' : energy ? '40E' : '40L';
    return 'klar';
}

// These are the original, individually authored character effects. Each texture
// has a real transparent guard border; no tight atlas trim or matte threshold.
export class ArenaEffectArt {
    constructor(loadImage, images) { this.loadImage = loadImage; this.images = images; }
    async preload(avatarIds = Object.keys(PROJECTILES)) {
        const requests = [];
        for (const avatar of new Set(avatarIds.map(id => this.avatar(id)))) {
            for (const id of [...EFFECT_IDS, ...PROJECTILES[avatar]]) {
                requests.push(this.loadImage(this.key(avatar, id), EFFECT_ROOT + avatar + '-' + id + '.webp', 'low'));
            }
        }
        await Promise.all(requests);
    }
    key(avatar, id) { return 'original-effect:' + avatar + ':' + id; }
    avatar(id) { return Object.hasOwn(PROJECTILES, id) ? id : 'bip01'; }
    projectileId(avatarId, charged = false) { return PROJECTILES[this.avatar(avatarId)][charged ? 1 : 0]; }
    draw(context, avatarId, id, frame, x, y, size, rotation = 0, opacity = 1, blend = 'source-over', heightScale = 1) {
        const avatar = this.avatar(avatarId);
        if (![frame, x, y, size, rotation, opacity, heightScale].every(Number.isFinite) || size <= 0 || opacity <= 0 || heightScale <= 0) return;
        const image = this.images.get(this.key(avatar, id));
        if (!valid(image)) {
            this.drawFallback(context, avatar, id, x, y, size, rotation, opacity, blend, heightScale);
            return;
        }
        const columns = id.startsWith('pulse_') ? 4 : 1;
        const cellWidth = image.naturalWidth / columns;
        const cellHeight = image.naturalHeight;
        const index = columns === 1 ? 0 : ((Math.floor(frame) % columns) + columns) % columns;
        const aspect = cellWidth / cellHeight;
        const width = aspect >= 1 ? size : size * aspect;
        const height = (aspect >= 1 ? size / aspect : size) * heightScale;
        context.save();
        context.translate(x, y);
        context.rotate(rotation);
        context.globalAlpha = Math.max(0, Math.min(1, opacity));
        context.globalCompositeOperation = blend;
        context.drawImage(image, index * cellWidth, 0, cellWidth, cellHeight, -width / 2, -height / 2, width, height);
        context.restore();
    }

    drawFallback(context, avatar, id, x, y, size, rotation, opacity, blend, heightScale) {
        const projectile = id.startsWith('pulse_');
        const color = id === 'pulse_gold' || id === 'pulse_amber' ? '255,205,105'
            : avatar === 'santa' ? '255,219,145'
            : avatar === 'lupp' ? '194,158,255' : '121,220,255';
        const radius = size * (projectile ? .23 : .42);
        context.save();
        context.translate(x, y);
        context.rotate(rotation);
        context.scale(projectile ? 1.65 : 1, (projectile ? .56 : 1) * heightScale);
        context.globalAlpha = Math.min(1, opacity);
        context.globalCompositeOperation = blend;
        const glow = context.createRadialGradient(0, 0, 0, 0, 0, radius);
        glow.addColorStop(0, projectile ? '#fff' : 'rgba(' + color + ',.08)');
        glow.addColorStop(projectile ? .36 : .72, 'rgba(' + color + ',.72)');
        glow.addColorStop(1, 'rgba(' + color + ',0)');
        context.beginPath();
        context.arc(0, 0, radius, 0, Math.PI * 2);
        context.fillStyle = glow;
        context.fill();
        context.restore();
    }
}

export function effectAvatarForEvent(event, fighters) {
    if (['join', 'join-pull', 'join-explode'].includes(event.type)) return 'raymond';
    const defensive = ['shield', 'parry', 'break'].includes(event.type);
    const first = defensive ? event.targetId : event.actorId;
    const second = defensive ? event.actorId : event.targetId;
    return fighters.get(first)?.avatarId || fighters.get(second)?.avatarId || 'starter';
}
