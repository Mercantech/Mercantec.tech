// Original account-avatar art. Each action keeps its authored eight-frame sheet.
const ACTIONS = Object.freeze({
    idle: { fps: 8, loop: true }, blink: { fps: 10 }, wave: { fps: 10 },
    think: { fps: 8, loop: true }, talk: { fps: 10, loop: true }, point: { fps: 10 },
    celebrate: { fps: 10 }, encourage: { fps: 10 }, surprised: { fps: 10 }, unlock: { fps: 10 },
    walk: { fps: 10, loop: true }, run: { fps: 12, loop: true }, jump: { fps: 10 },
    land: { fps: 10 }, sit: { fps: 10 }, sleep: { fps: 6, loop: true },
    pickup: { fps: 10 }, interact: { fps: 10 }, hit: { fps: 10 }, knockout: { fps: 10 },
    respawn: { fps: 10 }, ability_1_pulse: { fps: 8, authoritative: true },
    ability_2_magnet: { fps: 8, authoritative: true }, ability_3_shield: { fps: 8 },
    ability_4_dash: { fps: 8, authoritative: true }, ability_5_repair: { fps: 8, authoritative: true },
    ultimate_charge: { fps: 5, authoritative: true }, ultimate_release: { fps: 5, authoritative: true }
});
const ROOTS = Object.freeze({ bip01: '/assets/avatars/bip-01', raymond: '/assets/avatars/raymond', santa: '/assets/avatars/santa', lupp: '/assets/avatars/lupp' });
export const ORIGINAL_COMBAT_ACTIONS = Object.freeze([
    'idle', 'walk', 'run', 'jump', 'land', 'hit', 'knockout', 'respawn', 'interact', 'celebrate',
    'ability_1_pulse', 'ability_2_magnet', 'ability_3_shield', 'ability_4_dash', 'ability_5_repair',
    'ultimate_charge', 'ultimate_release'
]);
export const isOriginalAvatar = avatarId => Object.hasOwn(ROOTS, avatarId);
export const originalAvatarAction = action => Object.hasOwn(ACTIONS, action) ? action : 'idle';
export const originalAvatarKey = (avatarId, action = 'idle') => 'avatar-' + avatarId + '-' + originalAvatarAction(action);
export const originalAvatarSource = (avatarId, action = 'idle') => isOriginalAvatar(avatarId)
    ? ROOTS[avatarId] + '/sprites/' + originalAvatarAction(action) + '.png' : null;
export const originalAvatarFace = avatarId => isOriginalAvatar(avatarId) ? ROOTS[avatarId] + '/face.png' : null;
export function originalAvatarAssets() {
    return Object.keys(ROOTS).flatMap(avatarId => ORIGINAL_COMBAT_ACTIONS.map(action =>
        [originalAvatarKey(avatarId, action), originalAvatarSource(avatarId, action)]));
}

/** One-shot poses start at frame zero; attacks follow the authoritative server phase. */
export function originalAvatarFrame(fighter, now, timelines) {
    if (!isOriginalAvatar(fighter.avatarId)) return null;
    const action = originalAvatarAction(fighter.action || 'idle');
    const definition = ACTIONS[action];
    const identity = fighter.id || fighter.avatarId;
    let timeline = timelines.get(identity);
    if (!timeline || timeline.action !== action || timeline.avatarId !== fighter.avatarId) {
        timeline = { action, avatarId: fighter.avatarId, startedAt: now };
        timelines.set(identity, timeline);
    }
    let frame;
    if (definition.authoritative && Number.isFinite(fighter.actionFrame)) {
        frame = Math.floor(Math.max(0, Math.min(7, fighter.actionFrame)));
    } else {
        const elapsed = Math.max(0, now - timeline.startedAt) * definition.fps / 1000;
        frame = definition.loop ? Math.floor(elapsed) % 8 : Math.min(7, Math.floor(elapsed));
    }
    return { action, frame, columns: 4, rows: 2, originX: .5, originY: 224 / 256 };
}
