import { randomBytes } from 'node:crypto';
import { closeSync, chmodSync, fsyncSync, lstatSync, mkdirSync, openSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

export function ensureServiceKey(path) {
  mkdirSync(dirname(path), { recursive: true, mode: 0o755 });
  let file;
  try {
    // Exclusive creation preserves the same key across restarts and races.
    file = openSync(path, 'wx', 0o444);
  } catch (error) {
    if (error.code !== 'EEXIST') throw new Error('The shared document service key could not be initialized');
  }
  if (file !== undefined) {
    try {
      writeFileSync(file, randomBytes(32).toString('hex'), 'utf8');
      fsyncSync(file);
    } finally {
      closeSync(file);
    }
  }
  const stat = lstatSync(path);
  if (!stat.isFile() || stat.isSymbolicLink()) throw new Error('Invalid shared document service key file');
  const key = readFileSync(path, 'utf8').trim();
  if (key.length < 32 || key.length > 16384) throw new Error('Invalid shared document service key file');
  chmodSync(path, 0o444);
  // The key itself is intentionally never returned or logged.
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  ensureServiceKey(process.env.NOTION_SERVICE_SECRET_FILE || '/run/notion/service-key');
  // Dokploy treats a successful one-shot exit as an unhealthy service. Keep the
  // initializer alive; readiness is a separate content-validation healthcheck.
  setInterval(() => {}, 60 * 60 * 1000);
}
