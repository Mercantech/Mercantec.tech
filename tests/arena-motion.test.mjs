import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../web/wwwroot/js/arena-motion.js', import.meta.url), 'utf8');
const { ArenaMotion } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

const fighter = (id, x, extra = {}) => ({
    id, x, y: 500, vx: 300, vy: 0, stocks: 3, grounded: true, ...extra
});
const snapshot = (tick, x, extra = {}) => ({
    tick, stageId: 'rooftops', phase: 'playing',
    fighters: [fighter('own', x), fighter('remote', x)], projectiles: [], ...extra
});

test('remote motion stays forward through jitter while the local fighter uses the newest snapshot', () => {
    const motion = new ArenaMotion();
    const arrivals = [[100, 0], [102, 33], [104, 74], [106, 99], [108, 135], [110, 172]];
    const positions = [];
    let packet = 0;
    for (let now = 0; now < 205; now += 1000 / 60) {
        while (packet < arrivals.length - 1 && arrivals[packet + 1][1] <= now) packet++;
        const [tick, receivedAt] = arrivals[packet];
        const prior = arrivals[packet - 1];
        const state = snapshot(tick, (tick - 100) * 5);
        const rendered = motion.sample(now, 1 / 60, state,
            prior && snapshot(prior[0], (prior[0] - 100) * 5), receivedAt, prior?.[1], 'own');
        positions.push(rendered.fighters[1].x);
        assert.ok(rendered.fighters[0].x >= state.fighters[0].x);
        assert.ok(rendered.fighters[0].x <= state.fighters[0].x + 7.5);
    }
    assert.ok(positions.some(x => x > 10 && x < 20), 'there are frames between authoritative positions');
    assert.ok(positions.every((x, index) => !index || x >= positions[index - 1]));
});

test('the renderer caps extrapolation on a stale snapshot and snaps after reconnect', () => {
    const motion = new ArenaMotion();
    const first = snapshot(100, 100);
    motion.sample(0, 0, first, null, 0, 0, 'own');
    let remote;
    for (let now = 17; now < 1000; now += 17) {
        remote = motion.sample(now, .017, first, null, 0, 0, 'own').fighters[1];
    }
    assert.equal(remote.x, 107.5);
    const recovered = snapshot(160, 600);
    const result = motion.sample(1000, .016, recovered, first, 1000, 0, 'own');
    assert.equal(result.fighters[1].x, 600);
});

test('stock loss and projectile reflection apply immediately instead of blending old ownership', () => {
    const motion = new ArenaMotion();
    const first = snapshot(100, 100, { projectiles: [{ id: 1, ownerId: 'remote', kind: 'pulse', x: 130, y: 400, vx: 100 }] });
    const second = snapshot(102, 100, {
        fighters: [fighter('own', 110), fighter('remote', 700, { stocks: 2, action: 'respawn' })],
        projectiles: [{ id: 1, ownerId: 'own', kind: 'reflected', x: 150, y: 400, vx: -100 }]
    });
    motion.sample(0, 0, first, null, 0, 0, 'own');
    const result = motion.sample(33, .016, second, first, 33, 0, 'own');
    assert.equal(result.fighters[1].x, 700);
    assert.equal(result.projectiles[0].x, 150);
    assert.equal(result.projectiles[0].ownerId, 'own');
});
