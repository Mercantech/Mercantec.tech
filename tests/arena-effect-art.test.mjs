import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const source = await fs.readFile(new URL('../web/wwwroot/js/arena-effect-art.js', import.meta.url), 'utf8');
const { ArenaEffectArt, combatPresentation, combatStatusFor, effectAvatarForEvent, projectileArtFor, repairReadinessFor } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

test('effects load original per-character layers and four-frame projectiles without the rejected atlas', async () => {
    const calls = [];
    const art = new ArenaEffectArt((key, url) => { calls.push({ key, url }); return Promise.resolve(); }, new Map());
    await art.preload();
    assert.equal(calls.length, 20);
    assert.equal(new Set(calls.map(call => call.url)).size, 20);
    assert.ok(calls.every(call => call.url.startsWith('/assets/arena/v3/effects/')));
    assert.ok(calls.some(call => call.url.endsWith('bip01-pulse_cyan.webp')));
    assert.ok(calls.some(call => call.url.endsWith('raymond-pulse_blue.webp')));
    assert.ok(calls.every(call => !call.url.includes('effects-atlas')));
});

test('the two avatars select their own neutral and charged projectile artwork', () => {
    const art = new ArenaEffectArt(() => {}, new Map());
    assert.equal(art.projectileId('bip01'), 'pulse_cyan');
    assert.equal(art.projectileId('bip01', true), 'pulse_amber');
    assert.equal(art.projectileId('raymond'), 'pulse_blue');
    assert.equal(art.projectileId('raymond', true), 'pulse_gold');
});

test('projectile drawing preserves the full guarded source tile and restores canvas state', () => {
    const images = new Map();
    const art = new ArenaEffectArt(() => {}, images);
    const image = { complete: true, naturalWidth: 2048, naturalHeight: 512 };
    images.set(art.key('raymond', 'pulse_blue'), image);
    const draws = [];
    const context = { save() { this.saved = true; }, restore() { this.restored = true; }, translate() {}, rotate() {}, drawImage(...args) { draws.push(args); } };
    for (let frame = 0; frame < 4; frame++) art.draw(context, 'raymond', 'pulse_blue', frame, 10, 20, 80, 0, 1, 'screen');
    assert.deepEqual(draws.map(args => args.slice(1, 5)), [[0,0,512,512],[512,0,512,512],[1024,0,512,512],[1536,0,512,512]]);
    assert.ok(context.saved && context.restored);
});

test('defensive flashes belong to the defender and attack effects to the attacker', () => {
    const fighters = new Map([['robot', { avatarId: 'bip01' }], ['professor', { avatarId: 'raymond' }]]);
    assert.equal(effectAvatarForEvent({ type: 'shield', actorId: 'robot', targetId: 'professor' }, fighters), 'raymond');
    assert.equal(effectAvatarForEvent({ type: 'parry', actorId: 'professor', targetId: 'robot' }, fighters), 'bip01');
    assert.equal(effectAvatarForEvent({ type: 'hit', actorId: 'robot', targetId: 'professor' }, fighters), 'bip01');
    assert.equal(effectAvatarForEvent({ type: 'break', actorId: 'professor' }, fighters), 'raymond');
    assert.equal(effectAvatarForEvent({ type: 'join-explode', actorId: 'robot', targetId: 'professor' }, fighters), 'raymond');
});

test('JOIN is gold and reflection retains the original caster artwork after ownership changes', () => {
    const fighters = new Map([['robot', { avatarId: 'bip01' }], ['professor', { avatarId: 'raymond' }]]);
    assert.deepEqual(projectileArtFor({ ownerId: 'professor', kind: 'pulse' }, fighters),
        { avatarId: 'raymond', id: 'pulse_blue', reflected: false });
    assert.deepEqual(projectileArtFor({ ownerId: 'robot', sourceAvatarId: 'raymond', kind: 'reflected' }, fighters),
        { avatarId: 'raymond', id: 'pulse_blue', reflected: true });
    assert.deepEqual(projectileArtFor({ ownerId: 'robot', sourceAvatarId: 'raymond', kind: 'join', reflected: true }, fighters),
        { avatarId: 'raymond', id: 'pulse_gold', reflected: true });
    assert.deepEqual(projectileArtFor({ ownerId: 'robot', kind: 'join' }, fighters),
        { avatarId: 'raymond', id: 'pulse_gold', reflected: false });
    assert.deepEqual(projectileArtFor({ ownerId: 'disconnected', sourceAvatarId: 'bip01', kind: 'pulse' }, fighters),
        { avatarId: 'bip01', id: 'pulse_cyan', reflected: false });
    assert.equal(projectileArtFor({ ownerId: 'legacy' }, fighters).id, 'pulse_cyan');
});

test('resource HUD uses authoritative meter, seconds and states and leaves legacy snapshots untouched', () => {
    assert.equal(combatPresentation({ avatarId: 'bip01' }), null);
    assert.equal(combatPresentation({ avatarId: 'starter', combat: { ultimate: 100 } }), null);
    const combat = combatPresentation({ avatarId: 'bip01', actionFrame: 7, combat: {
        energy: 38.5, ultimate: 100, cooldowns: { grab: .85, repair: 5.75 },
        channelRemaining: .4, armored: true, capturedBy: 'professor', activeAbility: 'repair'
    } });
    assert.equal(combat.energy, 38.5);
    assert.equal(combat.ultimateReady, true);
    assert.equal(combat.cooldowns.grab, .85);
    assert.equal(combat.cooldowns.repair, 5.75);
    assert.equal(combat.channelRemaining, .4);
    assert.equal(combat.armored, true);
    assert.equal(combat.capturedBy, 'professor');
    assert.equal(combat.activeAbility, 'repair');
    assert.equal(combatPresentation({ avatarId: 'raymond', combat: { ultimate: 99.9 } }).ultimateReady, false);
    assert.equal(combatPresentation({ avatarId: 'raymond', combat: { ultimate: 100, cooldowns: { ultimate: .1 } } }).ultimateReady, false);
});

test('missing and malformed optional resources never produce nonfinite HUD geometry or fake readiness', () => {
    const combat = combatPresentation({ avatarId: 'raymond', combat: {
        energy: Infinity, ultimate: NaN, cooldowns: { pulse: -1, grab: Infinity, dash: NaN, repair: '2' },
        channelRemaining: Infinity, armored: 'true', capturedBy: {}, activeAbility: 4
    } });
    assert.equal(combat.energy, 0);
    assert.equal(combat.ultimate, 0);
    assert.equal(combat.ultimateReady, false);
    assert.equal(combat.channelRemaining, 0);
    assert.ok(Object.values(combat.cooldowns).every(value => Number.isFinite(value) && value >= 0));
    assert.equal(combat.armored, false);
    assert.equal(combat.capturedBy, null);
    assert.equal(combat.activeAbility, null);
    const clamped = combatPresentation({ avatarId: 'bip01', combat: { energy: -10, ultimate: 120 } });
    assert.equal(clamped.energy, 0);
    assert.equal(clamped.ultimate, 100);
});

test('shared channel timer distinguishes ultimate charging from actual restoration', () => {
    const fighter = (avatarId, activeAbility, channelRemaining) => ({ avatarId, combat: { activeAbility, channelRemaining } });
    assert.equal(combatStatusFor(fighter('bip01', 'overload-charge', .6)), 'Lader Kerneoverload · 0.6s');
    assert.equal(combatStatusFor(fighter('raymond', 'join-charge', .9)), 'Lader JOIN · 0.9s');
    assert.equal(combatStatusFor(fighter('bip01', 'repair', .4)), 'Reparerer 0.4s');
    assert.equal(combatStatusFor(fighter('raymond', 'repair', .8)), 'Gendanner 0.8s');
    assert.equal(combatStatusFor(fighter('raymond', 'capture', .5)), null);
    assert.equal(combatStatusFor({ avatarId: 'raymond' }), null);
});

test('repair HUD prioritizes cooldown, full health, landing and each kit resource before readiness', () => {
    const fighter = { avatarId: 'bip01', damage: 25, grounded: true, shield: 100, combat: { energy: 100 } };
    assert.equal(repairReadinessFor(fighter), 'klar');
    assert.equal(repairReadinessFor({ ...fighter, damage: 0, grounded: false, combat: { energy: 0, cooldowns: { repair: 2.5 } } }), '2.5s');
    assert.equal(repairReadinessFor({ ...fighter, damage: 0, grounded: false, combat: { energy: 0 } }), 'fuldt');
    assert.equal(repairReadinessFor({ ...fighter, grounded: false, combat: { energy: 0 } }), 'land');
    assert.equal(repairReadinessFor({ ...fighter, combat: { energy: 39 } }), '40E');
    assert.equal(repairReadinessFor({ ...fighter, avatarId: 'raymond', shield: 39 }), '40L');
    assert.equal(repairReadinessFor({ ...fighter, avatarId: 'raymond', shield: 40, combat: { energy: 0 } }), 'klar');
    assert.equal(repairReadinessFor({ ...fighter, combat: null }), null);
});

test('invalid optional effect geometry is ignored rather than poisoning canvas state', () => {
    const art = new ArenaEffectArt(() => {}, new Map([['original-effect:bip01:shield', { complete: true, naturalWidth: 512, naturalHeight: 512 }]]));
    const context = { save() { throw new Error('Invalid geometry reached canvas'); } };
    art.draw(context, 'bip01', 'shield', 0, Infinity, 0, 100);
    art.draw(context, 'bip01', 'shield', 0, 0, 0, 100, 0, NaN);
    art.draw(context, 'bip01', 'shield', 0, 0, 0, 100, 0, 1, 'screen', Infinity);
});
