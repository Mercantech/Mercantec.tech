import { createHash } from 'node:crypto';
import { readFile, writeFile, mkdir, copyFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = dirname(fileURLToPath(import.meta.url));
const output = resolve(directory, '../web/wwwroot/image-tools');
const runtime = resolve(directory, 'node_modules/onnxruntime-web');
const modelUrl = 'https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx';
const modelMd5 = '8e83ca70e441ab06c318d82300c84806';
const modelSha256 = '309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8';
const modelPath = resolve(output, 'u2netp.onnx');
await mkdir(output, { recursive: true });
const packageInfo = JSON.parse(await readFile(resolve(runtime, 'package.json'), 'utf8'));
if (packageInfo.version !== '1.31.1') throw new Error('Unexpected ONNX Runtime version; run npm ci.');

let model;
try { model = await readFile(modelPath); } catch { }
const hash = (algorithm, bytes) => createHash(algorithm).update(bytes).digest('hex');
if (!model || hash('sha256', model) !== modelSha256) {
    const response = await fetch(modelUrl, { signal: AbortSignal.timeout(60_000) });
    if (!response.ok) throw new Error(`Model download failed: HTTP ${response.status}`);
    if (Number(response.headers.get('content-length')) > 8 * 1024 * 1024) throw new Error('Unexpected model size.');
    model = Buffer.from(await response.arrayBuffer());
}
if (model.length !== 4_574_861 || hash('md5', model) !== modelMd5 || hash('sha256', model) !== modelSha256)
    throw new Error('U2netp model checksum mismatch.');
await writeFile(modelPath, model);

for (const filename of ['ort.wasm.min.mjs', 'ort-wasm-simd-threaded.mjs', 'ort-wasm-simd-threaded.wasm'])
    await copyFile(resolve(runtime, 'dist', filename), resolve(output, filename));
await copyFile(resolve(directory, 'background-core.mjs'), resolve(output, 'background-core.mjs'));

// Keep redistribution notices beside the generated binaries and model.
for (const filename of ['U2NET-LICENSE.txt', 'REMBG-LICENSE.txt', 'ONNXRUNTIME-LICENSE.txt', 'ONNXRUNTIME-THIRD-PARTY-NOTICES.txt'])
    await copyFile(resolve(directory, 'licenses', filename), resolve(output, filename));
await copyFile(resolve(directory, 'THIRD-PARTY-NOTICES.txt'), resolve(output, 'THIRD-PARTY-NOTICES.txt'));
await writeFile(resolve(output, 'assets.json'), JSON.stringify({
    onnxruntimeWeb: packageInfo.version,
    model: { name: 'u2netp', source: modelUrl, bytes: model.length, md5: modelMd5, sha256: modelSha256 }
}, null, 2) + '\n');
console.log(`Image tools ready: ONNX Runtime ${packageInfo.version}, U2netp ${model.length} bytes.`);
