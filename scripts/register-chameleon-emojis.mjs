// Register the two authored imagegen manifests with stable URL-based UUIDv5 identities.
// Existing characters and their identifiers remain byte-for-byte equivalent JSON values.
import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const catalogPath = path.join(root, 'assets/characters/reactions-catalog.json');
const catalog = JSON.parse(await fs.readFile(catalogPath, 'utf8'));
const entries = [];
const ids = new Set();
const namespace = Buffer.from('6ba7b8119dad11d180b400c04fd430c8', 'hex');
function uuidFor(url) {
    const bytes = createHash('sha1').update(namespace).update(url, 'utf8').digest().subarray(0, 16);
    bytes[6] = (bytes[6] & 15) | 80;
    bytes[8] = (bytes[8] & 63) | 128;
    const hex = bytes.toString('hex');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
for (const part of ['a', 'b']) {
    const manifest = JSON.parse(await fs.readFile(path.join(root, `assets/characters/chameleon/reactions-${part}.json`), 'utf8'));
    if (manifest.generator !== 'builtin-imagegen' || manifest.entries.length !== 24)
        throw new Error(`Expected twenty-four separately generated reactions in part ${part}.`);
    for (const item of manifest.entries) {
        if (!/^[a-z0-9-]+$/.test(item.id) || ids.has(item.id)
            || item.image !== `/images/characters/chameleon/reactions/${item.id}.png`
            || !item.prompt || !(item.sourceArtifact || item.source)
            || !item.alt?.startsWith('Kamæleonen') || !item.title || item.width !== 1254 || item.height !== 1254)
            throw new Error(`Invalid reaction metadata: ${item.id}`);
        await fs.access(path.join(root, 'web/wwwroot', item.image));
        ids.add(item.id);
        entries.push({ id: item.id, character: 'chameleon', title: item.title, image: item.image,
            alt: item.alt, width: item.width, height: item.height,
            guid: uuidFor(`https://lup.dk${item.image}`), name: `chameleon_${item.id.replaceAll('-', '_')}` });
    }
}
catalog.entries = [...catalog.entries.filter(item => item.character !== 'chameleon'), ...entries];
await fs.writeFile(catalogPath, JSON.stringify(catalog, null, 2) + '\n');
console.log(JSON.stringify({ registered: entries.length, catalogTotal: catalog.entries.length }));
