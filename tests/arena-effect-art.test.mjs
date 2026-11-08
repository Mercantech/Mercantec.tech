import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const source = await fs.readFile(new URL('../web/wwwroot/js/arena-effect-art.js', import.meta.url), 'utf8');
const { ArenaEffectArt, effectAvatarForEvent } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

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
});
