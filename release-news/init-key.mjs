import { randomBytes } from 'node:crypto';
import { mkdir, readFile, writeFile, chown, chmod } from 'node:fs/promises';

const directory = '/run/lup-updates';
const account = (await readFile('/etc/passwd', 'utf8')).split('\n').find(line => line.startsWith('pwuser:'))?.split(':');
if (!account) throw new Error('missing_release_observer_user');
const uid = Number(account[2]), gid = Number(account[3]);
await mkdir(directory, { recursive: true });
await mkdir('/data', { recursive: true });
const filename = `${directory}/service-key`;
try { await writeFile(filename, randomBytes(48).toString('base64url'), { flag: 'wx', mode: 0o440 }); }
catch (error) { if (error.code !== 'EEXIST') throw error; }
if ((await readFile(filename, 'utf8')).trim().length < 32) throw new Error('invalid_release_service_key');
await chown(directory, 0, gid);
await chown(filename, 0, gid);
await chmod(filename, 0o440);
await chown('/data', uid, gid);
// Keep the initializer healthy for Dokploy; no credential is ever printed.
setInterval(() => {}, 60_000);
