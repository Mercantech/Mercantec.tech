import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';

const directory = new URL('../web/wwwroot/images/characters/', import.meta.url);
const archive = new URL('../api/Storage/CharacterOriginals/manifest.json', import.meta.url);
const imagePath = /^\/images\/characters\/([a-z0-9-]+(?:\/[a-z0-9-]+)*\/[a-z0-9-]+\.png)(?:\?v=[a-f0-9]+)?$/u;
let manifest;

export async function openCharacterOriginal(image) {
    const relative = imagePath.exec(image)?.[1];
    if (!relative) throw new Error(`Unexpected character original path: ${image}`);

    const local = new URL(relative, directory);
    try {
        const bytes = await readFile(local);
        return {
            bytes,
            async verifyUnchanged() {
                if (!(await readFile(local)).equals(bytes)) throw new Error(`Original PNG changed: ${image}`);
            },
        };
    } catch (error) {
        if (error.code !== 'ENOENT') throw error;
    }

    manifest ??= readFile(archive, 'utf8').then(text => {
        const value = JSON.parse(text);
        if (value.schemaVersion !== 1 || !Array.isArray(value.files))
            throw new Error('Unexpected character originals manifest.');
        return new Map(value.files.map(file => [file.path, file]));
    });
    const entry = (await manifest).get(relative);
    if (!entry) throw new Error(`Missing local character original: ${image}`);

    const response = await fetch(new URL(image, 'https://lup.dk'), {
        redirect: 'error',
        signal: AbortSignal.timeout(30_000),
    });
    if (!response.ok || !/^image\/png(?:;|$)/i.test(response.headers.get('content-type') ?? ''))
        throw new Error(`Could not fetch archived character original: ${image} (${response.status})`);

    const bytes = Buffer.from(await response.arrayBuffer());
    const digest = createHash('sha256').update(bytes).digest('hex');
    if (bytes.length !== entry.bytes || digest !== entry.sha256)
        throw new Error(`Archived PNG does not match the manifest: ${image}`);

    return { bytes };
}
