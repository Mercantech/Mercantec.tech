import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

async function fixture() {
    const values = new Map(), copied = [];
    globalThis.window = { sessionStorage: {
        setItem: (key, value) => values.set(key, value), getItem: key => values.get(key) ?? null,
        removeItem: key => values.delete(key),
    } };
    Object.defineProperty(globalThis, 'navigator', { value: { clipboard: { writeText: async value => copied.push(value) } }, configurable: true });
    const source = await readFile(new URL('../../web/wwwroot/js/extension-access.js', import.meta.url), 'utf8');
    const module = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64') + '#' + Math.random());
    return { module, values, copied };
}

test('connection setup returns only the fixed local route once and stores no credential', async () => {
    const f = await fixture();
    f.module.prepareLogin();
    assert.equal(f.module.consumeLoginReturnPath(), '/extension');
    assert.equal(f.module.consumeLoginReturnPath(), null);
    assert.equal(f.values.size, 0);
});

test('tampered, malformed and expired continuation intents never become redirects', async () => {
    const f = await fixture();
    for (const value of [
        '{', JSON.stringify({intent:'https://evil.invalid',expiresAt:Date.now()+1000}),
        JSON.stringify({intent:'extension-access',expiresAt:Date.now()-1}),
        JSON.stringify({intent:'extension-access',expiresAt:Date.now()+31*60*1000}),
        JSON.stringify({intent:'extension-access',expiresAt:'tomorrow'}),
    ]) {
        f.values.set('lup.extension.login', value);
        assert.equal(f.module.consumeLoginReturnPath(), null);
        assert.equal(f.values.size, 0);
    }
});

test('copy is explicit, bounded and never persists the key', async () => {
    const f = await fixture();
    const fake = 'lup_' + 'A'.repeat(43);
    assert.equal(f.copied.length, 0);
    await assert.rejects(f.module.copyKey('bad'));
    await f.module.copyKey(fake);
    assert.deepEqual(f.copied, [fake]);
    assert.equal(f.values.size, 0);
});
