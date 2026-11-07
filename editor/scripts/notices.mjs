import { readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const lock = JSON.parse(await readFile(resolve(root, 'package-lock.json'), 'utf8'));
let output = 'LUP document editor — third-party notices\n\nThis bundle uses unmodified open source dependencies. BlockNote packages are MPL-2.0; the collaboration components, React and many other dependencies use MIT or other licenses below. No BlockNote XL packages are included.\n\nBlockNote v0.56.0 source: https://github.com/TypeCellOS/BlockNote/tree/v0.56.0\nMPL-2.0 source and licensing information: https://www.mozilla.org/MPL/2.0/\n\n';
for (const [path, entry] of Object.entries(lock.packages)) {
  if (!path || entry.dev) continue;
  const directory = resolve(root, path);
  let pkg; try { pkg = JSON.parse(await readFile(resolve(directory, 'package.json'), 'utf8')); } catch { continue; }
  output += '\n' + '='.repeat(72) + '\n' + pkg.name + '@' + pkg.version + '\nLicense: ' + (pkg.license || entry.license || 'See package license') + '\n';
  const repository = typeof pkg.repository === 'string' ? pkg.repository : pkg.repository?.url;
  if(repository) output += 'Source repository: ' + repository + (pkg.gitHead ? ' (commit ' + pkg.gitHead + ')' : '') + '\n';
  output += 'Package and source: https://www.npmjs.com/package/' + pkg.name + '/v/' + pkg.version + '\n';
  for (const file of await readdir(directory)) {
    if(!/^(licen[sc]e|copying|notice)([.-].*)?$/i.test(file)) continue;
    try { output += '\n' + file + '\n' + await readFile(resolve(directory, file), 'utf8') + '\n'; } catch {}
  }
}
for (const file of ['EXCALIDRAW-LICENSE.txt', 'FONT-NOTICES.txt']) {
  output += '\n' + '='.repeat(72) + '\n' + await readFile(resolve(root, 'public/excalidraw', file), 'utf8') + '\n';
}
await writeFile(resolve(root, 'public/THIRD-PARTY-NOTICES.txt'), output.replace(/[\t ]+$/gm, '').trimEnd() + '\n');
