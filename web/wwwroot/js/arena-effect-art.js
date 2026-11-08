const EFFECT_ROOT = '/assets/arena/v3/effects/';
const EFFECT_IDS = ['shield', 'magnet_field', 'dash_trail', 'repair_field', 'ultimate_charge', 'ultimate_release', 'impact', 'unlock'];
const PROJECTILES = { bip01: ['pulse_cyan', 'pulse_amber'], raymond: ['pulse_blue', 'pulse_gold'] };
const valid = image => Boolean(image?.complete && image.naturalWidth && !image.failed);

// These are the original, individually authored character effects. Each texture
// has a real transparent guard border; no tight atlas trim or matte threshold.
export class ArenaEffectArt {
    constructor(loadImage, images) { this.loadImage = loadImage; this.images = images; }
    async preload() {
        const requests = [];
        for (const avatar of Object.keys(PROJECTILES)) {
            for (const id of [...EFFECT_IDS, ...PROJECTILES[avatar]]) {
                requests.push(this.loadImage(this.key(avatar, id), EFFECT_ROOT + avatar + '-' + id + '.webp'));
            }
        }
        await Promise.all(requests);
    }
    key(avatar, id) { return 'original-effect:' + avatar + ':' + id; }
    avatar(id) { return id === 'raymond' ? 'raymond' : 'bip01'; }
    projectileId(avatarId, charged = false) { return PROJECTILES[this.avatar(avatarId)][charged ? 1 : 0]; }
    draw(context, avatarId, id, frame, x, y, size, rotation = 0, opacity = 1, blend = 'source-over', heightScale = 1) {
        const avatar = this.avatar(avatarId);
        const image = this.images.get(this.key(avatar, id));
        if (!valid(image) || !(size > 0) || opacity <= 0) return;
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
}

export function effectAvatarForEvent(event, fighters) {
    const defensive = ['shield', 'parry', 'break'].includes(event.type);
    const first = defensive ? event.targetId : event.actorId;
    const second = defensive ? event.actorId : event.targetId;
    return fighters.get(first)?.avatarId || fighters.get(second)?.avatarId || 'starter';
}
