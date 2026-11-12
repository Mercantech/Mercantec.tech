import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

const source = await fs.readFile(new URL('../web/wwwroot/js/arena-input.js', import.meta.url), 'utf8');
const { ArenaInputBuffer, arenaNeutralInput } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));

test('a short directional special survives a busy transport and a release', () => {
    const input = new ArenaInputBuffer();
    input.update({ ...arenaNeutralInput, vertical: -1, special: true }, 10);
    input.update(arenaNeutralInput, 19);
    const packet = input.packet(8, 30);
    assert.equal(packet.special, false);
    assert.equal(packet.vertical, 0);
    assert.equal(packet.pressed, 4);
    assert.equal(packet.pressVertical, -1);
    assert.equal(packet.sequence, 8);
    input.sent();
    assert.equal(input.packet(9, 32).pressed, undefined);
});

test('a held press is sent once and the release stays pending until it can be sent', () => {
    const input = new ArenaInputBuffer();
    const held = { ...arenaNeutralInput, jump: true };
    input.update(held, 0);
    assert.equal(input.packet(1, 0).pressed, 1);
    input.sent();
    input.update(held, 16);
    assert.equal(input.dirty, false);
    assert.equal(input.packet(2, 16).pressed, undefined);
    input.update(arenaNeutralInput, 24);
    input.update(arenaNeutralInput, 50);
    assert.equal(input.dirty, true);
    assert.equal(input.packet(3, 50).jump, false);
});

test('old presses expire and a new round cannot replay the previous round input', () => {
    const input = new ArenaInputBuffer();
    input.update({ ...arenaNeutralInput, attack: true }, 3);
    input.update(arenaNeutralInput, 8);
    assert.equal(input.packet(1, 204).pressed, undefined);
    input.update({ ...arenaNeutralInput, grab: true, horizontal: 1 }, 210);
    assert.equal(input.packet(2, 211).pressed, 16);
    input.clear();
    assert.deepEqual(input.packet(3, 211), { type: 'input', sequence: 3, ...arenaNeutralInput });
});

test('a new press survives while a different stale press expires', () => {
    const input = new ArenaInputBuffer();
    input.update({ ...arenaNeutralInput, jump: true }, 0);
    input.update({ ...arenaNeutralInput, attack: true, horizontal: -1 }, 190);
    const packet = input.packet(1, 205);
    assert.equal(packet.pressed, 2);
    assert.equal(packet.pressHorizontal, -1);
});
