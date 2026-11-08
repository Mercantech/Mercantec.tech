const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../web/wwwroot/js/lup-avatars.js'), 'utf8');

function setup(storageBlocked = false) {
    const values = new Map(), audio = [], events = {};
    class AudioMock {
        constructor(file) { this.file = file; this.paused = false; this.currentTime = 10; audio.push(this); }
        async play() { this.played = true; }
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
