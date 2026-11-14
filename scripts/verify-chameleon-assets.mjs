// Read-only asset verification. Only qa-report.json is written; native art and previews stay untouched.
// Run: node scripts/verify-chameleon-assets.mjs [--allow-partial]
import fs from 'node:fs/promises';
import path from 'node:path';
import { createRequire } from 'node:module';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { openCharacterOriginal } from './character-originals.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sharp = createRequire(path.join(root, '.tools/character-previews/package.json'))('sharp');
const assetDirectory = path.join(root, 'assets/characters/chameleon');
const publicDirectory = path.join(root, 'web/wwwroot');
const expected = { emojis: 48, infographics: 16, scenes: 4, portraits: 4 };
// Imagegen PNGs can contain isolated 1-4/255-alpha fringe pixels far from the visible silhouette.
// Report those native pixels exactly, but distinguish them from a genuinely clipped visible figure.
const visibleAlphaThreshold = 4;
const errors = [];
const warnings = [];
const manifests = [];
const entries = [];
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const message = error => error instanceof Error ? error.message : String(error);

async function readJson(relativePath, required = true) {
    try {
        const value = JSON.parse(await fs.readFile(path.join(root, relativePath), 'utf8'));
        manifests.push({ file: relativePath, found: true, generator: value.generator ?? value.mode ?? null,
            status: value.status ?? null, entries: Array.isArray(value.entries) ? value.entries.length : null });
        return value;
    } catch (error) {
        if (required) errors.push(`${relativePath}: ${message(error)}`);
        else warnings.push(`${relativePath}: not available yet (${message(error)})`);
        manifests.push({ file: relativePath, found: false });
        return null;
    }
}

// Use only the authoritative A/B manifests, never prompt plans or obsolete batch receipts.
for (const part of ['a', 'b']) {
    const relative = `assets/characters/chameleon/reactions-${part}.json`;
    const manifest = await readJson(relative);
    if (!manifest) continue;
    if (manifest.generator !== 'builtin-imagegen') errors.push(`${relative}: unexpected generator`);
    if (!Array.isArray(manifest.entries) || manifest.entries.length !== 24)
        errors.push(`${relative}: expected 24 delivered entries, found ${manifest.entries?.length ?? 0}`);
    for (const item of manifest.entries ?? []) entries.push({ ...item, group: 'emojis', manifest: relative });
}
const infographics = await readJson('assets/characters/chameleon/ml-infographics.json');
if (infographics) {
    if (infographics.generator !== 'builtin-imagegen' || infographics.status !== 'complete')
        errors.push('ml-infographics.json: generation is not complete');
    for (const item of infographics.entries ?? []) entries.push({ ...item, group: 'infographics', manifest: 'assets/characters/chameleon/ml-infographics.json' });
}
const artwork = await readJson('assets/characters/chameleon/artwork.json');
if (artwork) {
    if (artwork.generator !== 'builtin-imagegen' || artwork.status !== 'complete')
        errors.push('artwork.json: generation is not complete');
    for (const item of artwork.entries ?? []) {
        if (!['scene', 'portrait'].includes(item.kind)) errors.push(`artwork.json: unexpected kind for ${item.id}`);
        entries.push({ ...item, group: item.kind === 'scene' ? 'scenes' : 'portraits', manifest: 'assets/characters/chameleon/artwork.json' });
    }
}
const portrait = await readJson('assets/characters/chameleon/portrait-provenance.json');
if (portrait) entries.push({ ...portrait, id: 'portrait', image: '/images/characters/chameleon/portrait.png',
    sourceArtifact: portrait.sourceArtifact ?? portrait.source, group: 'portraits', manifest: 'assets/characters/chameleon/portrait-provenance.json' });

const previewRecords = new Map();
for (const relative of ['assets/characters/chameleon/preview-report.json', 'assets/characters/reactions-preview-report.json']) {
    const report = await readJson(relative);
    if (!Array.isArray(report)) {
        if (report) errors.push(`${relative}: expected an array`);
        continue;
    }
    for (const record of report) if (record.image?.startsWith('/images/characters/chameleon/')) previewRecords.set(record.image, record);
}

function publicPath(image) {
    if (!/^\/images\/characters\/chameleon\/(?:[a-z0-9-]+\/)?[a-z0-9-]+\.(?:png|webp)$/.test(image ?? ''))
        throw new Error(`Unexpected public asset path: ${image}`);
    return path.join(publicDirectory, image.slice(1));
}

async function alphaReport(bytes) {
    const { data, info } = await sharp(bytes).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let min = 255, max = 0, transparentPixels = 0, opaquePixels = 0;
    let left = info.width, top = info.height, right = -1, bottom = -1;
    let visibleLeft = info.width, visibleTop = info.height, visibleRight = -1, visibleBottom = -1;
    for (let pixel = 0, offset = info.channels - 1; offset < data.length; pixel++, offset += info.channels) {
        const alpha = data[offset];
        min = Math.min(min, alpha);
        max = Math.max(max, alpha);
        if (alpha === 0) transparentPixels++;
        if (alpha === 255) opaquePixels++;
        if (alpha > 0) {
            const x = pixel % info.width, y = Math.floor(pixel / info.width);
            left = Math.min(left, x); top = Math.min(top, y);
            right = Math.max(right, x); bottom = Math.max(bottom, y);
            if (alpha > visibleAlphaThreshold) {
                visibleLeft = Math.min(visibleLeft, x); visibleTop = Math.min(visibleTop, y);
                visibleRight = Math.max(visibleRight, x); visibleBottom = Math.max(visibleBottom, y);
            }
        }
    }
    const bounds = right < 0 ? null : { left, top, rightExclusive: right + 1, bottomExclusive: bottom + 1 };
    const margins = bounds ? { left, top, right: info.width - right - 1, bottom: info.height - bottom - 1 } : null;
    const visibleBounds = visibleRight < 0 ? null : { left: visibleLeft, top: visibleTop,
        rightExclusive: visibleRight + 1, bottomExclusive: visibleBottom + 1 };
    const visibleMargins = visibleBounds ? { left: visibleLeft, top: visibleTop,
        right: info.width - visibleRight - 1, bottom: info.height - visibleBottom - 1 } : null;
    const pixels = info.width * info.height;
    return { min, max, transparentPixels, opaquePixels, transparentFraction: transparentPixels / pixels,
        bounds, margins, minimumMargin: margins ? Math.min(...Object.values(margins)) : null,
        freeEdges: Boolean(margins && Object.values(margins).every(value => value > 0)),
        visibleAlphaThreshold, visibleBounds, visibleMargins,
        minimumVisibleMargin: visibleMargins ? Math.min(...Object.values(visibleMargins)) : null,
        visibleFreeEdges: Boolean(visibleMargins && Object.values(visibleMargins).every(value => value > 0)) };
}

const counts = Object.fromEntries(Object.keys(expected).map(group => [group, entries.filter(entry => entry.group === group).length]));
for (const [group, count] of Object.entries(expected)) if (counts[group] !== count)
    errors.push(`${group}: expected ${count} manifest entries, found ${counts[group]}`);
// Copying a collection must not leave discarded concepts exposed as unregistered public media.
for (const [group, directory] of Object.entries({ emojis: 'reactions', infographics: 'infographics', scenes: 'scenes', portraits: '' })) {
    const expectedFiles = new Set(entries.filter(item => item.group === group).flatMap(item =>
        [path.basename(item.image), path.basename(item.image.replace(/\.png$/, '.webp'))]));
    const published = await fs.readdir(path.join(publicDirectory, 'images/characters/chameleon', directory));
    for (const file of published.filter(file => /\.(png|webp)$/.test(file)))
        if (!expectedFiles.has(file)) errors.push(`Unregistered public ${group} file: ${file}`);
}
const seenImages = new Set();
const seenEmojiIds = new Set();
const seenEmojiHashes = new Map();
const results = [];
for (const item of entries) {
    const result = { id: item.id, group: item.group, image: item.image, manifest: item.manifest, valid: false, errors: [], warnings: [] };
    try {
        if (seenImages.has(item.image)) throw new Error('Duplicate public image identity');
        seenImages.add(item.image);
        if (item.group === 'emojis') {
            if (seenEmojiIds.has(item.id)) throw new Error('Duplicate emoji id');
            seenEmojiIds.add(item.id);
            if (item.image !== `/images/characters/chameleon/reactions/${item.id}.png`) throw new Error('Emoji id and public filename differ');
        }
        const original = await openCharacterOriginal(item.image);
        const nativeBytes = original.bytes;
        const metadata = await sharp(nativeBytes).metadata();
        const landscape = item.group === 'infographics' || item.group === 'scenes';
        const width = landscape ? 1536 : 1254, height = landscape ? 1024 : 1254;
        result.native = { format: metadata.format, width: metadata.width, height: metadata.height,
            channels: metadata.channels, hasAlpha: metadata.hasAlpha, bytes: nativeBytes.length, sha256: sha256(nativeBytes) };
        if (metadata.format !== 'png' || metadata.width !== width || metadata.height !== height)
            result.errors.push(`Expected native PNG ${width}x${height}`);
        if (item.width !== width || item.height !== height) result.errors.push('Manifest dimensions do not match expected dimensions');
        if (!item.prompt?.trim()) result.errors.push('Missing complete prompt');
        const sourceArtifact = item.sourceArtifact ?? item.source;
        if (!sourceArtifact) result.errors.push('Missing generated sourceArtifact');
        else {
            const sourceBytes = await fs.readFile(path.isAbsolute(sourceArtifact) ? sourceArtifact : path.join(root, sourceArtifact));
            result.sourceArtifact = sourceArtifact;
            result.sourceSha256 = sha256(sourceBytes);
            result.sourceMatches = result.native.sha256 === result.sourceSha256;
            if (!result.sourceMatches) result.errors.push('Native file differs from generated sourceArtifact');
        }
        if (item.sha256 && item.sha256.toLowerCase() !== result.native.sha256) result.errors.push('Native hash differs from provenance hash');
        result.visualReviewRecorded = item.visuallyReviewed === true || item.qa?.visuallyReviewed === true || (Array.isArray(item.qa) && item.qa.length > 0);
        if (!result.visualReviewRecorded) result.errors.push('No visual review recorded in manifest');
        if (item.group === 'emojis') {
            if (metadata.channels !== 4 || !metadata.hasAlpha) result.errors.push('Emoji must be native RGBA');
            result.alpha = await alphaReport(nativeBytes);
            if (result.alpha.min !== 0 || result.alpha.max !== 255) result.errors.push('Emoji alpha range must include 0 and 255');
            if (!result.alpha.visibleFreeEdges) result.errors.push(`Visible emoji pixels (alpha>${visibleAlphaThreshold}/255) touch at least one image edge`);
            if (!result.alpha.freeEdges && result.alpha.visibleFreeEdges)
                result.warnings.push(`Near-transparent fringe pixels (alpha<=${visibleAlphaThreshold}/255) touch at least one image edge; native original preserved`);
            if (result.alpha.transparentFraction < .05) result.errors.push('Less than five percent of emoji pixels are fully transparent');
            if (result.alpha.minimumVisibleMargin < Math.ceil(width * .02)) result.warnings.push('Visible silhouette margin is less than two percent on at least one edge');
            const sameHash = seenEmojiHashes.get(result.native.sha256);
            if (sameHash) result.errors.push(`Duplicate emoji bitmap also used by ${sameHash}`);
            seenEmojiHashes.set(result.native.sha256, item.id);
        }
        const preview = item.image.replace(/\.png$/, '.webp');
        const previewBytes = await fs.readFile(publicPath(preview));
        const previewMetadata = await sharp(previewBytes).metadata();
        const previewWidth = item.group === 'emojis' ? 512 : landscape ? 1200 : 768;
        const previewHeight = landscape ? 800 : previewWidth;
        result.preview = { image: preview, format: previewMetadata.format, width: previewMetadata.width,
            height: previewMetadata.height, hasAlpha: previewMetadata.hasAlpha, bytes: previewBytes.length, sha256: sha256(previewBytes) };
        if (previewMetadata.format !== 'webp' || previewMetadata.width !== previewWidth || previewMetadata.height !== previewHeight)
            result.errors.push(`Expected WebP preview ${previewWidth}x${previewHeight}`);
        if (item.group === 'emojis') {
            if (!previewMetadata.hasAlpha) result.errors.push('Emoji WebP preview lost its alpha channel');
            result.preview.alpha = await alphaReport(previewBytes);
            if (result.preview.alpha.min !== 0 || result.preview.alpha.max !== 255) result.errors.push('Emoji preview alpha range must include 0 and 255');
        }
        const recorded = previewRecords.get(item.image);
        result.previewReportMatches = Boolean(recorded && recorded.preview === preview && recorded.originalSha256 === result.native.sha256
            && recorded.previewSha256 === result.preview.sha256 && recorded.originalBytes === nativeBytes.length && recorded.previewBytes === previewBytes.length);
        if (!result.previewReportMatches) result.errors.push('Missing or stale native/preview packaging hash record');
        // Catch accidental writes by concurrent packaging or another process during this audit.
        await original.verifyUnchanged?.();
        result.valid = result.errors.length === 0;
    } catch (error) {
        result.errors.push(message(error));
    }
    errors.push(...result.errors.map(error => `${item.image}: ${error}`));
    warnings.push(...result.warnings.map(warning => `${item.image}: ${warning}`));
    results.push(result);
}

const report = { schemaVersion: 1, generatedAt: new Date().toISOString(), complete: errors.length === 0,
    expectedCounts: expected, counts, validCounts: Object.fromEntries(Object.keys(expected).map(group => [group, results.filter(item => item.group === group && item.valid).length])),
    sourcePolicy: 'Authoritative delivered manifests only; exact native-source SHA-256 equality. Generated originals are never modified.',
    transparencyPolicy: `RGBA, alpha includes 0 and 255, nonempty silhouette. Exact alpha>0 bounds are reported. Visible silhouette uses explicit alpha>${visibleAlphaThreshold}/255 threshold and must have free edges. Native fringe at alpha<=${visibleAlphaThreshold}/255 and visible margins below 2% are warnings; no pixels are modified.`,
    manifests, errors, warnings, entries: results,
    gifValidation: { checkedSeparately: true, report: 'assets/characters/chameleon/animations/validation.json' } };
await fs.writeFile(path.join(assetDirectory, 'qa-report.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify({ complete: report.complete, counts, validCounts: report.validCounts,
    errors: errors.length, warnings: warnings.length, report: 'assets/characters/chameleon/qa-report.json' }, null, 2));
if (!report.complete && !process.argv.includes('--allow-partial')) process.exitCode = 1;
