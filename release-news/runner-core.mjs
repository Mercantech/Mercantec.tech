import fs from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

export const REVISION = /^[a-f0-9]{64}$/;
export const COMMIT = /^[a-f0-9]{40}$/;
export const MAX_DIFF_BYTES = 160_000;
export const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

export async function readJson(file) {
  try { return JSON.parse(await fs.readFile(file, 'utf8')); }
  catch (error) { if (error.code === 'ENOENT') return null; throw new Error('Persistent worker data could not be read.'); }
}

export async function writeJson(file, value, { mode = 0o600 } = {}) {
  await fs.mkdir(path.dirname(file), { recursive: true, mode: 0o700 });
  const temporary = `${file}.${randomUUID()}.tmp`;
  let handle;
  try {
    handle = await fs.open(temporary, 'wx', mode);
    await handle.chmod(mode);
    await handle.writeFile(JSON.stringify(value));
    await handle.sync();
    await handle.close(); handle = null;
    await fs.rename(temporary, file);
  } finally {
    await handle?.close().catch(() => {});
    await fs.unlink(temporary).catch(() => {});
  }
}

/** Only fixed, public-safe diagnostics leave the private observer volume. */
export function classifyWorkerError(error, phase) {
  const text = [error?.message, error?.stderr, error?.code, error?.cause?.message, error?.cause?.code].filter(value => typeof value === 'string').join('\n');
  if (error?.status === 401) return 'service_auth_failed';
  if (error?.status === 400) return 'request_bad_request';
  if (error?.status === 403) return 'request_forbidden';
  if (error?.status === 404) return 'request_not_found';
  if (error?.status >= 500 && error?.status <= 599) return 'request_server_error';
  if (/index\.lock/i.test(text)) return 'git_index_locked';
  if ((phase === 'resolving-source' || /git/i.test(text)) && /ENOTFOUND|EAI_AGAIN|ETIMEDOUT|could not resolve|name or service not known|network is unreachable|connection timed out/i.test(text)) return 'git_network';
  if (/ECONNREFUSED|ENOTFOUND|ETIMEDOUT|EAI_AGAIN|UND_ERR_CONNECT_TIMEOUT|EHOSTUNREACH|ENETUNREACH|ECONNRESET/.test(text)) return 'endpoint_unreachable';
  if (/redirect/i.test(text)) return 'request_redirected';
  if (error?.code === 'response_not_json') return 'response_not_json';
  if (/browserType\.launch|chromium\.launch|crashpad/i.test(text)) {
    if (/crashpad|read.only|EACCES|not writable|permission denied/i.test(text)) return 'browser_cache_not_writable';
    return 'browser_launch_failed';
  }
  if (error?.code === 'browser_cache_not_writable' || error?.code === 'browser_launch_failed') return error.code;
  if (/bounded source history|source commit was not found/i.test(text)) return 'source_not_found';
  if (/No valid screenshots|Capture did not complete/i.test(text)) return 'capture_failed';
  if (error?.code === 'web_metadata_unavailable' || error?.code === 'api_metadata_unavailable') return error.code;
  if (error?.metadataSource === 'web' || error?.metadataSource === 'api') return `${error.metadataSource}_metadata_unavailable`;
  return 'worker_error';
}

export function truncateUtf8(text, limit) {
  const bytes = Buffer.from(text, 'utf8');
  if (bytes.length <= limit) return text;
  for (let end = limit; end >= Math.max(0, limit - 3); end--) {
    try { return new TextDecoder('utf-8', { fatal: true }).decode(bytes.subarray(0, end)); }
    catch { /* Never cut in the middle of a code point. */ }
  }
  return '';
}

export function isReviewableFile(file) {
  if (typeof file !== 'string' || !file || /[\x00-\x1f]/.test(file)) return false;
  const normalized = file.replaceAll('\\', '/');
  if (normalized.startsWith('/') || normalized.split('/').includes('..')) return false;
  if (/(^|\/)(?:\.git|\.github|\.tools|\.data|bin|obj|node_modules|release-news)(\/|$)/i.test(normalized)) return false;
  if (/(^|\/)(?:\.env(?:\..*)?|[^/]*(?:credentials|secrets?)[^/]*|service-key|id_rsa|id_ed25519)(\/|$)/i.test(normalized)) return false;
  if (/\.(?:pem|pfx|p12|key|keystore|jks|md|lock)$/i.test(normalized)) return false;
  if (/(^|\/)(?:appsettings[^/]*\.json|package-lock\.json|yarn\.lock|pnpm-lock\.yaml|coverage\.json|image-sources\.json|Dockerfile(?:\..*)?|docker-compose[^/]*\.ya?ml)$/i.test(normalized)) return false;
  if (/^web\/wwwroot\/(?:notion-editor|image-tools)\//.test(normalized)) return false;
  return true;
}

export function isBinaryFile(file) {
  return /\.(?:png|jpe?g|webp|gif|ico|pdf|zip|gz|7z|mp[34]|wav|ogg|woff2?|ttf|wasm|dll|exe|pdb|blat|dat)$/i.test(file);
}

export function redactSecrets(text) {
  return text
    .replace(/-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----[\s\S]*?-----END (?:[A-Z ]+ )?PRIVATE KEY-----/g, '[private key redacted]')
    .replace(/\bsk-(?:(?:proj|svcacct)-)?[A-Za-z0-9_-]{16,}\b/g, '[API key redacted]')
    .replace(/\bBearer\s+[A-Za-z0-9._~+\/-]{16,}/gi, 'Bearer [redacted]')
    .replace(/((?:api[_-]?key|client[_-]?secret|password|access[_-]?token|refresh[_-]?token)\s*["']?\s*[:=]\s*["']?)[^\s"',;}{]{8,}/gi, '$1[redacted]');
}

const AUTHOR_EMAILS = new Map([
  ['kasc@mercantec.dk', 'Kasper'],
  ['mags@mercantec.dk', 'Mathias'],
  ['mathiasgsteenberg@gmail.com', 'Mathias'],
  ['120091142+mags-gh@users.noreply.github.com', 'Mathias'],
]);
const AUTHOR_NAMES = new Map([
  ['kasperhbc', 'Kasper'], ['kasper', 'Kasper'], ['kasc', 'Kasper'],
  ['mags', 'Mathias'], ['mags-gh', 'Mathias'], ['mathiasgsteenberg', 'Mathias'],
]);

export function canonicalAuthors(authors) {
  const found = new Set();
  for (const author of authors) {
    const name = AUTHOR_EMAILS.get(String(author.email ?? '').trim().toLowerCase())
      ?? AUTHOR_NAMES.get(String(author.name ?? '').trim().toLowerCase());
    if (name) found.add(name);
  }
  return ['Kasper', 'Mathias'].filter(name => found.has(name));
}

export function normalizeEntries(response, scenarioIds) {
  if (!Array.isArray(response?.entries) || response.entries.length > 5) throw new Error('Invalid release analysis.');
  const used = new Set();
  return response.entries.map(entry => {
    if (!entry || !/^[a-z0-9][a-z0-9-]{0,79}$/.test(entry.id) || used.has(entry.id)) throw new Error('Invalid release analysis.');
    const title = typeof entry.title === 'string' ? entry.title.trim() : '';
    const description = typeof entry.description === 'string' ? entry.description.trim() : '';
    if (!title || title.length > 100 || !description || description.length > 500) throw new Error('Invalid release analysis.');
    used.add(entry.id);
    return { id: entry.id, title, description, scenarioId: scenarioIds.has(entry.scenarioId) ? entry.scenarioId : null };
  });
}

export function isPng(bytes) {
  return bytes.length >= 45 && bytes.length <= MAX_IMAGE_BYTES
    && bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
    && bytes.toString('ascii', 12, 16) === 'IHDR'
    && bytes.readUInt32BE(16) > 0 && bytes.readUInt32BE(16) <= 4096
    && bytes.readUInt32BE(20) > 0 && bytes.readUInt32BE(20) <= 4096
    && bytes.toString('ascii', bytes.length - 8, bytes.length - 4) === 'IEND';
}

export async function publicationImage(capture, directory) {
  if (capture?.ok !== true || typeof capture.path !== 'string') return undefined;
  try {
    const root = await fs.realpath(directory);
    const file = await fs.realpath(path.resolve(directory, capture.path));
    const relative = path.relative(root, file);
    if (!relative || relative.startsWith(`..${path.sep}`) || relative === '..' || path.isAbsolute(relative)) return undefined;
    const stat = await fs.stat(file);
    if (!stat.isFile() || stat.size > MAX_IMAGE_BYTES) return undefined;
    const bytes = await fs.readFile(file);
    if (!isPng(bytes)) return undefined;
    const alt = typeof capture.alt === 'string' ? capture.alt.trim().slice(0, 250) : '';
    if (!alt) return undefined;
    return { alt, base64: bytes.toString('base64') };
  } catch { return undefined; }
}

export function validCursor(value) {
  return value && COMMIT.test(value.commit) && REVISION.test(value.revision)
    && typeof value.capturedAtUtc === 'string' && Number.isFinite(Date.parse(value.capturedAtUtc));
}

/** Retain the comparison pair and every unacknowledged outbox; never follow cache links. */
export async function pruneCaches(dataDir, state) {
  if (state?.remoteSyncPending || !validCursor(state?.cursor)) return;
  const root = path.resolve(dataDir);
  if ((await fs.lstat(root)).isSymbolicLink()) throw new Error('Unsafe worker cache directory.');
  const realRoot = await fs.realpath(root);
  async function ownedDirectory(name) {
    const directory = path.join(root, name);
    let stat;
    try { stat = await fs.lstat(directory); } catch (error) { if (error.code === 'ENOENT') return null; throw error; }
    if (!stat.isDirectory() || stat.isSymbolicLink() || path.relative(realRoot, await fs.realpath(directory)) !== name) throw new Error('Unsafe worker cache directory.');
    return directory;
  }
  const captures = await ownedDirectory('captures'), outbox = await ownedDirectory('outbox');
  const previous = state.previousRevision ?? Object.values(state.history ?? {}).filter(value => validCursor(value) && value.revision !== state.cursor.revision)
    .sort((left, right) => Date.parse(right.capturedAtUtc) - Date.parse(left.capturedAtUtc))[0]?.revision;
  const keep = new Set([state.cursor.revision, previous].filter(value => REVISION.test(value)));
  if (outbox) for (const entry of await fs.readdir(outbox, { withFileTypes: true })) {
    const revision = /^([a-f0-9]{64})\.json$/.exec(entry.name)?.[1];
    if (!revision) continue;
    const file = path.join(outbox, entry.name), stat = await fs.lstat(file);
    let processed = false;
    if (stat.isFile() && !stat.isSymbolicLink() && stat.size <= 32 * 1024 * 1024) {
      try {
        const value = await readJson(file), cursor = state.history?.[revision];
        processed = value?.deliveryAcknowledged === true && validCursor(cursor)
          && value.payload?.revision === revision && value.payload?.commit === cursor.commit;
      } catch { /* A damaged or unknown pending item is retained, not discarded. */ }
    }
    if (processed) await fs.unlink(file);
    else keep.add(revision);
  }
  if (captures) for (const entry of await fs.readdir(captures, { withFileTypes: true })) {
    if (!REVISION.test(entry.name) || keep.has(entry.name)) continue;
    const directory = path.join(captures, entry.name), stat = await fs.lstat(directory);
    if (!stat.isDirectory() || stat.isSymbolicLink() || path.relative(await fs.realpath(captures), await fs.realpath(directory)) !== entry.name) continue;
    const marker = path.join(directory, '.runner-manifest.json');
    let manifest;
    try {
      const markerStat = await fs.lstat(marker);
      if (!markerStat.isFile() || markerStat.isSymbolicLink()) continue;
      manifest = await readJson(marker);
    } catch { continue; }
    if (manifest?.revision === entry.name && Number.isFinite(Date.parse(manifest.capturedAtUtc))
        && manifest.items && typeof manifest.items === 'object' && !Array.isArray(manifest.items)) {
      await fs.rm(directory, { recursive: true, force: true });
    }
  }
}
