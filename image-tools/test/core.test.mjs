import test from 'node:test';
import assert from 'node:assert/strict';
import { readImageDataUrl, normalizeRgb, normalizeMask, maskToRgba, multiplyAlpha,
    outputDimensions, MODEL_SIZE, MAX_SOURCE_BYTES } from '../background-core.mjs';

test('input accepts only supported bounded data URLs, never remote fetch URLs', () => {
    const source = readImageDataUrl('data:image/png;base64,AQID');
    assert.equal(source.contentType, 'image/png');
    assert.deepEqual([...source.bytes], [1, 2, 3]);
    for (const value of ['https://example.test/image.png', 'data:image/svg+xml;base64,AQID',
        'data:image/png;base64,', 'data:image/png;base64,AB==', 'data:image/png;base64,AAAA!'])
        assert.throws(() => readImageDataUrl(value));
    assert.throws(() => readImageDataUrl('data:image/png;base64,' + 'A'.repeat(Math.ceil(MAX_SOURCE_BYTES / 3) * 4 + 4)));
});

test('RGB normalization matches rembg global maximum, channel means and NCHW order', () => {
    const rgba = new Uint8ClampedArray(MODEL_SIZE * MODEL_SIZE * 4);
    rgba.set([255, 128, 0, 37]);
    const value = normalizeRgb(rgba), pixels = MODEL_SIZE * MODEL_SIZE;
    assert(Math.abs(value[0] - (1 - .485) / .229) < 1e-6);
    assert(Math.abs(value[pixels] - (128 / 255 - .456) / .224) < 1e-6);
    assert(Math.abs(value[2 * pixels] - (0 - .406) / .225) < 1e-6);
    assert.equal(value.length, pixels * 3);
    assert(normalizeRgb(new Uint8ClampedArray(rgba.length)).every(Number.isFinite));
    assert.throws(() => normalizeRgb(new Uint8ClampedArray(4)));
});

test('constant, empty and nonfinite masks cannot replace the original image', () => {
    for (const constant of [0, .8, 1])
        assert.throws(() => normalizeMask(new Float32Array(MODEL_SIZE * MODEL_SIZE).fill(constant)));
    const invalid = new Float32Array(MODEL_SIZE * MODEL_SIZE);
    invalid[0] = NaN;
    assert.throws(() => normalizeMask(invalid));
    assert.throws(() => normalizeMask(new Float32Array(0)));
});

test('mask normalization has the rembg 0-255 range without hard thresholding', () => {
    const input = new Float32Array(MODEL_SIZE * MODEL_SIZE).fill(.5);
    input[0] = 0; input[1] = 1;
    const mask = normalizeMask(input);
    assert.deepEqual([...mask.slice(0, 3)], [0, 255, 127]);
});

test('existing transparency is multiplied, never replaced by an opaque model mask', () => {
    const pixels = new Uint8ClampedArray([1, 2, 3, 0, 4, 5, 6, 128, 7, 8, 9, 255]);
    const result = multiplyAlpha(pixels, maskToRgba(new Uint8ClampedArray([255, 128, 0])));
    assert.deepEqual([...result], [1, 2, 3, 0, 4, 5, 6, 64, 7, 8, 9, 0]);
    assert.throws(() => multiplyAlpha(pixels, new Uint8ClampedArray(4)));
});

test('output is bounded to emoji dimensions and retains aspect ratio', () => {
    assert.deepEqual(outputDimensions(1600, 900), { width: 256, height: 144 });
    assert.deepEqual(outputDimensions(20, 30), { width: 20, height: 30 });
    assert.deepEqual(outputDimensions(1600, 900, 128), { width: 128, height: 72 });
    for (const [width, height] of [[0, 1], [1.1, 2], [5000, 5000]])
        assert.throws(() => outputDimensions(width, height));
});
