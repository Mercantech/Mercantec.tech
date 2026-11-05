import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, writeFileSync, unlinkSync, rmdirSync, chmodSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { ensureServiceKey } from '../src/init-key.mjs';
import { resolveServiceSecret } from '../src/api-client.mjs';

function keyFixture(t) {
  const directory = mkdtempSync(join(tmpdir(), 'lup-notion-key-'));
  const path = join(directory, 'service-key');
  t.after(() => {
    try { chmodSync(path, 0o666); unlinkSync(path); } catch (e) { if (e.code !== 'ENOENT') throw e; }
    rmdirSync(directory);
  });
  return path;
}

test('initializer creates a random key and preserves it on subsequent starts', t => {
  const path = keyFixture(t);
  ensureServiceKey(path);
  const original = readFileSync(path, 'utf8');
  assert.match(original, /^[0-9a-f]{64}$/);
  ensureServiceKey(path);
  assert.equal(readFileSync(path, 'utf8'), original);
});

test('initializer rejects an invalid existing key without overwriting it', t => {
  const path = keyFixture(t);
  writeFileSync(path, 'too-short');
  assert.throws(() => ensureServiceKey(path), /Invalid shared/);
  assert.equal(readFileSync(path, 'utf8'), 'too-short');
});

test('explicit secret takes precedence and file fallback is validated', t => {
  const path = keyFixture(t);
  ensureServiceKey(path);
  assert.equal(resolveServiceSecret('', path), readFileSync(path, 'utf8'));
  assert.equal(resolveServiceSecret('  explicit-override  ', path), 'explicit-override');
  assert.equal(resolveServiceSecret(undefined, undefined), '');
  assert.throws(() => resolveServiceSecret('', path + '.missing'), /could not be loaded/);
});
