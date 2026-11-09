import { createHash } from 'node:crypto';
import { readFile, readdir, lstat, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

// Both Docker images and the observer hash the SAME build inputs. No .git or credentials
// enter the Docker images. Build outputs and the .dockerignore exclusions are omitted.
const roots = ['api', 'web', 'shared', 'editor', 'image-tools', 'extensions', 'collaboration', 'release-news'];
const ignored = new Set(['bin', 'obj', '.git', 'node_modules', 'obsidian', '.obsidian', '.vs', '.idea', '.data']);
export function ignoredInput(relative) {
  const parts = relative.split('/');
  return parts.some(part => ignored.has(part) || part === '.env' || part.startsWith('.env.'))
    || relative.endsWith('.md')
    || /^web\/wwwroot\/(notion-editor|image-tools)(\/|$)/.test(relative);
}

export async function fingerprint(root) {
  const files = [];
  async function visit(relative) {
    if (ignoredInput(relative)) return;
    const absolute = path.join(root, ...relative.split('/'));
    let stat;
    try { stat = await lstat(absolute); }
    catch (error) { if (error.code === 'ENOENT') return; throw error; }
    if (stat.isSymbolicLink()) throw new Error('release_input_symlink');
    if (stat.isDirectory()) {
      for (const entry of (await readdir(absolute)).sort()) await visit(`${relative}/${entry}`);
    } else if (stat.isFile()) files.push(relative);
  }
  for (const relative of [...roots, 'docker-compose.yml']) await visit(relative);
  const digest = createHash('sha256').update('lup-build-inputs-v1\0');
  for (const relative of files.sort()) {
    let content = await readFile(path.join(root, ...relative.split('/')));
    // Git normalizes source text differently on Windows and Linux; binary assets remain exact.
    try {
      const text = new TextDecoder('utf-8', { fatal: true }).decode(content);
      if (!text.includes('\0')) content = Buffer.from(text.replaceAll('\r\n', '\n'));
    } catch { /* binary asset */ }
    digest.update(relative).update('\0').update(createHash('sha256').update(content).digest('hex')).update('\0');
  }
  return digest.digest('hex');
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const revision = await fingerprint(path.resolve(process.argv[2] || '.'));
  const json = JSON.stringify({ schemaVersion: 1, revision }) + '\n';
  if (process.argv[3]) await writeFile(process.argv[3], json);
  else process.stdout.write(json);
}
