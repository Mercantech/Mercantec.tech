import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fingerprint } from './fingerprint.mjs';

test('release identity detects code and binary changes, excludes docs/builds/secrets, and tolerates checkout EOLs', async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), 'lup-release-hash-'));
  try {
    await mkdir(path.join(root, 'web', 'wwwroot'), { recursive: true });
    const code = path.join(root, 'web', 'Page.razor');
    const image = path.join(root, 'web', 'wwwroot', 'test.png');
    await writeFile(code, 'linje et\nlinje to\n');
    await writeFile(image, Buffer.from([0, 255, 13, 10, 3]));
    const original = await fingerprint(root);
    await writeFile(code, 'linje et\r\nlinje to\r\n');
    assert.equal(await fingerprint(root), original);
    await writeFile(path.join(root, 'web', '.env.local'), 'fake ignored configuration');
    await writeFile(path.join(root, 'web', 'README.md'), 'documentation');
    await mkdir(path.join(root, 'web', 'obj'));
    await writeFile(path.join(root, 'web', 'obj', 'build.json'), '{}');
    assert.equal(await fingerprint(root), original);
    await writeFile(image, Buffer.from([0, 255, 10, 3]));
    assert.notEqual(await fingerprint(root), original);
    await writeFile(image, Buffer.from([0, 255, 13, 10, 3]));
    await writeFile(code, 'en anden funktion\n');
    assert.notEqual(await fingerprint(root), original);
  } finally { await rm(root, { recursive: true, force: true }); }
});
