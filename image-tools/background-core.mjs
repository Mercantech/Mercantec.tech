export const MAX_SOURCE_BYTES = 8 * 1024 * 1024;
export const MAX_PIXELS = 16 * 1024 * 1024;
export const MAX_RESULT_BYTES = 256 * 1024;
export const MODEL_SIZE = 320;

export function readImageDataUrl(value) {
    if (typeof value !== 'string' || value.length > Math.ceil(MAX_SOURCE_BYTES / 3) * 4 + 50)
        throw new Error('Billedet må højst fylde 8 MB.');
    const match = /^data:(image\/(?:png|jpeg|gif|webp));base64,([A-Za-z0-9+/]+={0,2})$/.exec(value);
    if (!match || match[2].length % 4 !== 0) throw new Error('Vælg et PNG-, JPEG-, GIF- eller WebP-billede.');
    const binary = atob(match[2]);
    if (!binary.length || binary.length > MAX_SOURCE_BYTES || btoa(binary) !== match[2])
        throw new Error('Billedet er tomt, for stort eller kunne ikke læses.');
    const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
    return { bytes, contentType: match[1] };
}

export function normalizeRgb(rgba) {
    if (rgba.length !== MODEL_SIZE * MODEL_SIZE * 4) throw new Error('Billedets format kunne ikke klargøres.');
    const pixels = MODEL_SIZE * MODEL_SIZE;
    let maximum = 0;
    for (let index = 0; index < rgba.length; index += 4)
        maximum = Math.max(maximum, rgba[index], rgba[index + 1], rgba[index + 2]);
    const divisor = Math.max(maximum, 1e-6);
    const mean = [0.485, 0.456, 0.406], std = [0.229, 0.224, 0.225];
    const data = new Float32Array(pixels * 3);
    for (let pixel = 0; pixel < pixels; pixel++)
        for (let channel = 0; channel < 3; channel++)
            data[channel * pixels + pixel] = (rgba[pixel * 4 + channel] / divisor - mean[channel]) / std[channel];
    return data;
}

export function normalizeMask(values) {
    if (values.length !== MODEL_SIZE * MODEL_SIZE) throw new Error('Baggrundsmodellen gav et ugyldigt svar.');
    let minimum = Infinity, maximum = -Infinity;
    for (const value of values) {
        if (!Number.isFinite(value)) throw new Error('Baggrundsmodellen gav et ugyldigt svar.');
        minimum = Math.min(minimum, value); maximum = Math.max(maximum, value);
    }
    if (maximum - minimum < 1e-6)
        throw new Error('Der blev ikke fundet et tydeligt motiv. Det oprindelige billede er bevaret.');
    return Uint8ClampedArray.from(values, value => Math.trunc((value - minimum) / (maximum - minimum) * 255));
}

export function maskToRgba(mask) {
    const rgba = new Uint8ClampedArray(mask.length * 4);
    for (let index = 0; index < mask.length; index++) {
        rgba[index * 4] = rgba[index * 4 + 1] = rgba[index * 4 + 2] = 255;
        rgba[index * 4 + 3] = mask[index];
    }
    return rgba;
}

export function multiplyAlpha(rgba, maskRgba) {
    if (rgba.length !== maskRgba.length || rgba.length % 4 !== 0)
        throw new Error('Billedets gennemsigtighed kunne ikke beregnes.');
    for (let index = 3; index < rgba.length; index += 4)
        rgba[index] = Math.round(rgba[index] * maskRgba[index] / 255);
    return rgba;
}

export function outputDimensions(width, height, maximum = 256) {
    if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 || width * height > MAX_PIXELS)
        throw new Error('Billedet må højst have 16 megapixel.');
    const scale = Math.min(1, maximum / width, maximum / height);
    return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}
