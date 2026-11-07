import * as ort from '/image-tools/ort.wasm.min.mjs';
import { readImageDataUrl, normalizeRgb, normalizeMask, maskToRgba, multiplyAlpha,
    outputDimensions, MODEL_SIZE, MAX_RESULT_BYTES } from '/image-tools/background-core.mjs';

ort.env.wasm.wasmPaths = '/image-tools/';
ort.env.wasm.numThreads = 1;
ort.env.wasm.proxy = false;
let sessionPromise = null;
let busy = false;

function session() {
    sessionPromise ??= ort.InferenceSession.create('/image-tools/u2netp.onnx', {
        executionProviders: ['wasm'], graphOptimizationLevel: 'all'
    }).catch(error => { sessionPromise = null; throw error; });
    return sessionPromise;
}

async function processImage(dataUrl) {
    const { bytes, contentType } = readImageDataUrl(dataUrl);
    let bitmap;
    try { bitmap = await createImageBitmap(new Blob([bytes], { type: contentType })); }
    catch { throw new Error('Billedet kunne ikke læses. Vælg et andet billede.'); }
    try {
        outputDimensions(bitmap.width, bitmap.height);
        const modelCanvas = new OffscreenCanvas(MODEL_SIZE, MODEL_SIZE);
        const modelContext = modelCanvas.getContext('2d', { willReadFrequently: true });
        modelContext.imageSmoothingQuality = 'high';
        modelContext.drawImage(bitmap, 0, 0, MODEL_SIZE, MODEL_SIZE);
        const data = normalizeRgb(modelContext.getImageData(0, 0, MODEL_SIZE, MODEL_SIZE).data);
        let inference;
        try { inference = await session(); }
        catch { throw new Error('Baggrundsmodellen kunne ikke indlæses. Kontrollér forbindelsen, og prøv igen.'); }
        const tensor = new ort.Tensor('float32', data, [1, 3, MODEL_SIZE, MODEL_SIZE]);
        let predictions;
        let mask;
        try {
            predictions = await inference.run({ [inference.inputNames[0]]: tensor }, [inference.outputNames[0]]);
            mask = normalizeMask(predictions[inference.outputNames[0]].data);
        } finally {
            tensor.dispose?.();
            if (predictions) for (const output of Object.values(predictions)) output.dispose?.();
        }
        const maskCanvas = new OffscreenCanvas(MODEL_SIZE, MODEL_SIZE);
        maskCanvas.getContext('2d').putImageData(new ImageData(maskToRgba(mask), MODEL_SIZE, MODEL_SIZE), 0, 0);
        for (const maximum of [256, 192, 128]) {
            const size = outputDimensions(bitmap.width, bitmap.height, maximum);
            const output = new OffscreenCanvas(size.width, size.height);
            const context = output.getContext('2d', { willReadFrequently: true });
            context.imageSmoothingQuality = 'high';
            context.drawImage(bitmap, 0, 0, size.width, size.height);
            const original = context.getImageData(0, 0, size.width, size.height);
            const resizedMask = new OffscreenCanvas(size.width, size.height);
            const maskContext = resizedMask.getContext('2d', { willReadFrequently: true });
            maskContext.imageSmoothingQuality = 'high';
            maskContext.drawImage(maskCanvas, 0, 0, size.width, size.height);
            const maskPixels = maskContext.getImageData(0, 0, size.width, size.height);
            multiplyAlpha(original.data, maskPixels.data);
            context.putImageData(original, 0, 0);
            const blob = await output.convertToBlob({ type: 'image/png' });
            if (blob.size > MAX_RESULT_BYTES) continue;
            const result = new Uint8Array(await blob.arrayBuffer());
            let binary = '';
            for (const byte of result) binary += String.fromCharCode(byte);
            return { base64: btoa(binary), contentType: 'image/png' };
        }
        throw new Error('Det behandlede billede kunne ikke gøres mindre end 256 KB. Det oprindelige billede er bevaret.');
    } finally { bitmap.close(); }
}

self.addEventListener('message', async event => {
    const request = event.data;
    if (!request || request.type !== 'remove' || typeof request.jobId !== 'string') return;
    if (busy) { self.postMessage({ jobId: request.jobId, error: 'En anden baggrundsfjernelse er i gang.' }); return; }
    busy = true;
    try { self.postMessage({ jobId: request.jobId, result: await processImage(request.dataUrl) }); }
    catch (error) {
        const message = error instanceof Error && /^(Billed|Vælg|Baggrund|Der blev|Det behandlede)/.test(error.message)
            ? error.message : 'Baggrunden kunne ikke fjernes. Det oprindelige billede er bevaret.';
        self.postMessage({ jobId: request.jobId, error: message });
    } finally { busy = false; }
});
