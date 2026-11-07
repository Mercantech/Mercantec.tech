import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { createContext, runInContext } from 'node:vm';

const source = await readFile(new URL('../../web/wwwroot/js/auth-storage.js', import.meta.url), 'utf8');
const lifetime = 30 * 60 * 1000;
const key = 'mt_login_return';

function fixture() {
    const values = new Map();
    const clock = { now: Date.UTC(2026, 10, 7, 12) };
    const sessionStorage = {
        getItem: key => values.get(key) ?? null,
        setItem: (key, value) => values.set(key, String(value)),
        removeItem: key => values.delete(key),
    };
    function reload() {
        const context = createContext({
            window: {},
            sessionStorage,
            Date: class extends Date { static now() { return clock.now; } },
        });
        runInContext(source, context, { filename: 'auth-storage.js' });
        return context.window.mercantecAuth;
    }
    return { values, clock, reload, auth: reload() };
}

test('return destination survives a full script reload with its query and fragment', () => {
    const f = fixture();
    const path = '/notebook/123?owner=abc&kind=journal#kommentarer';
    f.auth.setLoginReturnPath(path);
    f.clock.now += 5 * 60 * 1000;

    const afterLogin = f.reload();
    assert.equal(afterLogin.consumeLoginReturnPath(), path);
    assert.equal(afterLogin.consumeLoginReturnPath(), null);
    assert.equal(f.values.size, 0);
});

test('latest login attempt replaces an older destination', () => {
    const f = fixture();
    f.auth.setLoginReturnPath('/journal');
    f.auth.setLoginReturnPath('/teacher/notebooks');

    assert.equal(f.auth.consumeLoginReturnPath(), '/teacher/notebooks');
    assert.equal(f.reload().consumeLoginReturnPath(), null);
});

test('return intent remains valid just before its deadline', () => {
    const f = fixture();
    f.auth.setLoginReturnPath('/journal');
    f.clock.now += lifetime - 1;

    assert.equal(f.reload().consumeLoginReturnPath(), '/journal');
});

test('return intent expires at its deadline and is removed', () => {
    for (const elapsed of [lifetime, lifetime + 1, lifetime * 2]) {
        const f = fixture();
        f.auth.setLoginReturnPath('/journal');
        f.clock.now += elapsed;

        assert.equal(f.reload().consumeLoginReturnPath(), null);
        assert.equal(f.values.has(key), false);
    }
});

test('malformed, expired and implausibly future intents are rejected and consumed', () => {
    const f = fixture();
    const validExpiry = f.clock.now + 1000;
    const invalid = [
        '{',
        'null',
        '[]',
        JSON.stringify({ path: '/journal' }),
        JSON.stringify({ path: 123, expiresAt: validExpiry }),
        JSON.stringify({ path: null, expiresAt: validExpiry }),
        JSON.stringify({ path: '/journal', expiresAt: String(validExpiry) }),
        JSON.stringify({ path: '/journal', expiresAt: null }),
        JSON.stringify({ path: '/journal', expiresAt: f.clock.now - 1 }),
        JSON.stringify({ path: '/journal', expiresAt: f.clock.now + lifetime + 1 }),
        JSON.stringify({ path: '/journal', expiresAt: Number.MAX_VALUE }),
    ];
    for (const value of invalid) {
        f.values.set(key, value);
        assert.equal(f.auth.consumeLoginReturnPath(), null, value);
        assert.equal(f.values.has(key), false, value);
    }
});

test('clearing return intent leaves the current token and PKCE records intact', () => {
    const f = fixture();
    f.auth.setTokens('test-access', 'test-refresh', 12345);
    f.auth.setPkce('test-verifier', 'test-state');
    f.auth.setLoginReturnPath('/journal');
    f.auth.clearLoginReturnPath();

    assert.equal(f.reload().consumeLoginReturnPath(), null);
    assert.equal(f.auth.getTokens().access, 'test-access');
    assert.equal(f.auth.getTokens().refresh, 'test-refresh');
    assert.equal(f.auth.getPkce().verifier, 'test-verifier');
    assert.equal(f.auth.getPkce().state, 'test-state');
});

test('local logout storage cleanup leaves no destination to replay after a reload', () => {
    const f = fixture();
    f.auth.setTokens('test-access', 'test-refresh', 12345);
    f.auth.setPkce('test-verifier', 'test-state');
    f.auth.setLoginReturnPath('/notebook/123');

    f.auth.clearTokens();
    f.auth.clearPkce();
    f.auth.clearLoginReturnPath();

    assert.equal(f.values.size, 0);
    const nextVisit = f.reload();
    assert.equal(nextVisit.consumeLoginReturnPath(), null);
    assert.equal(nextVisit.getTokens().access, null);
    assert.equal(nextVisit.getPkce().state, null);
});
