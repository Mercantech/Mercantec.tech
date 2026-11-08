const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/lup-avatars.js'), 'utf8');

function setup(storageBlocked = false, playbackBlocked = false) {
    const values = new Map(), audio = [], events = {};
    class AudioMock {
        constructor(file) { this.file = file; this.paused = true; this.currentTime = 10; audio.push(this); }
        async play() { if (playbackBlocked) throw new Error('Playback blocked'); this.played = true; this.paused = false; }
        pause() { this.paused = true; }
    }
    const document = { hidden: false, addEventListener(name, fn) { events[name] = fn; } };
    const context = { window: {}, document, Audio: AudioMock, localStorage: {
        getItem(key) { if (storageBlocked) throw new Error('blocked'); return values.get(key) ?? null; },
        removeItem(key) { if (storageBlocked) throw new Error('blocked'); values.delete(key); }
    }};
    vm.runInNewContext(source, context);
    return { api: context.window.lupAvatars, values, audio, document, events };
}

test('legacy migration reads only the current account and removes only its record', () => {
    const { api, values } = setup();
    const key = subject => 'lup-avatar-profile-v1:user:' + encodeURIComponent(subject);
    values.set(key('student-a'), '{"selectedAvatarId":"bip01"}');
    values.set(key('student-b'), '{"selectedAvatarId":"starter"}');
    values.set('lup-avatar-profile-v1:guest', '{"selectedAvatarId":"guest-choice"}');
    values.set(key('guest'), '{"selectedAvatarId":"named-account"}');
    assert.equal(api.get('student-a'), '{"selectedAvatarId":"bip01"}');
    assert.equal(api.get('student-b'), '{"selectedAvatarId":"starter"}');
    assert.equal(api.get(null), '{"selectedAvatarId":"guest-choice"}');
    assert.equal(api.get('guest'), '{"selectedAvatarId":"named-account"}');
    assert.equal(api.get('unknown'), null);
    assert.equal(api.remove('student-a'), true);
    assert.equal(api.get('student-a'), null);
    assert.equal(api.get('student-b'), '{"selectedAvatarId":"starter"}');
    assert.equal(api.set, undefined, 'new choices must never be written to browser storage');
});

test('both complete avatar packs allow their exact 28 effects and five spoken files', async () => {
    const { api, audio } = setup();
    const actions = ['idle','blink','wave','think','talk','point','celebrate','encourage','surprised','unlock','walk','run','jump','land','sit','sleep','pickup','interact','hit','knockout','respawn','ability_1_pulse','ability_2_magnet','ability_3_shield','ability_4_dash','ability_5_repair','ultimate_charge','ultimate_release'];
    const phrases = ['phrase_1_welcome','phrase_2_start','phrase_3_success','phrase_4_retry','phrase_5_milestone'];
    for (const avatar of ['bip-01', 'raymond']) {
        for (const id of [...actions, ...phrases])
            assert.equal(await api.play(`/assets/avatars/${avatar}/audio/${id}.wav`), true, avatar + '/' + id);
    }
    assert.equal(audio.length, 66);
    assert.ok(audio.slice(0, -1).every(clip => clip.paused && clip.currentTime === 0));
    assert.equal(audio.at(-1).paused, false);
});

test('audio allowlist rejects remote, invented, traversal and modified URLs', async () => {
    const { api, audio } = setup();
    for (const file of [
        'https://example.com/speech.wav', '//example.com/speech.wav',
        '/assets/avatars/unknown/audio/walk.wav', '/assets/avatars/raymond/audio/../walk.wav',
        '/assets/avatars/raymond/audio/./walk.wav', '/assets/avatars/raymond/audio/%77alk.wav',
        '/assets/avatars/raymond/audio/phrase_1_invented.wav', '/assets/avatars/raymond/audio/invented.wav',
        '/assets/avatars/raymond/audio/walk.wav?url=https://example.com',
        '/assets/avatars/raymond/audio/walk.wav#fragment', '/assets/avatars/raymond/audio/walk.wav ',
        '/assets/avatars/raymond/audio/walk.wav\n',
        null, {}, 5
    ]) assert.equal(await api.play(file), false, String(file));
    assert.equal(audio.length, 0);
});

test('explicit stop cancels and resets playback and remains safe when called again', async () => {
    const { api, audio } = setup();
    await api.play('/assets/avatars/raymond/audio/phrase_1_welcome.wav');
    api.stop(); api.stop();
    assert.equal(audio[0].paused, true);
    assert.equal(audio[0].currentTime, 0);
});

test('blocked playback reports failure without throwing or writing browser state', async () => {
    const { api, values, audio } = setup(false, true);
    assert.equal(await api.play('/assets/avatars/raymond/audio/ability_1_pulse.wav'), false);
    assert.equal(values.size, 0);
    assert.equal(audio.length, 1);
    api.stop();
    assert.equal(audio[0].paused, true);
});

test('blocked legacy storage does not block database-backed avatars', () => {
    const { api } = setup(true);
    assert.equal(api.get('student-a'), null);
    assert.equal(api.remove('student-a'), false);
});

test('speech plays only on explicit request, stays local and stops previous playback', async () => {
    const { api, audio, document, events } = setup();
    assert.equal(audio.length, 0);
    assert.equal(await api.play('https://example.com/speech.wav'), false);
    assert.equal(await api.play('/assets/avatars/bip-01/audio/../other.wav'), false);
    assert.equal(audio.length, 0);
    assert.equal(await api.play('/assets/avatars/bip-01/audio/phrase_1_welcome.wav'), true);
    assert.equal(await api.play('/assets/avatars/bip-01/audio/phrase_2_start.wav'), true);
    assert.equal(audio.length, 2);
    assert.equal(audio[0].paused, true);
    assert.equal(audio[0].currentTime, 0);
    document.hidden = true;
    events.visibilitychange();
    assert.equal(audio[1].paused, true);
});
