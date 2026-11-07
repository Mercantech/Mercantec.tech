import test from 'node:test';
import assert from 'node:assert/strict';
import { fetchImportImage, sniffImageType } from '../image-import.mjs';
import { ImportFailure, importDiagnostic } from '../import-errors.mjs';

const png = Uint8Array.from([137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0]);
const url = 'https://images.example.test/picture?privateToken=NEVER_COPY_ME';
test('image download uses bytes to recognize mislabeled images and omits credentials and referrer', async () => {
  const blob = await fetchImportImage(url, { fetcher: async (target, options) => {
    assert.equal(target, url); assert.equal(options.credentials, 'omit'); assert.equal(options.referrerPolicy, 'no-referrer');
    assert.equal(options.headers, undefined); return new Response(png, { headers: { 'content-type': 'text/plain' } });
  } });
  assert.equal(blob.type, 'image/png'); assert.deepEqual(new Uint8Array(await blob.arrayBuffer()), png);
});
test('HTTP failures have stable actionable codes and numeric status only', async () => {
  for (const [status, code] of [[401, '103'], [403, '104'], [404, '105'], [410, '105'], [429, '106'], [500, '107']])
    await assert.rejects(fetchImportImage(url, { fetcher: async () => new Response('private server body', { status }) }), error => error.code === `LUP-IMP-${code}` && error.context.status === status);
});
test('network, empty, HTML, SVG and truncated bodies are distinguished', async () => {
  for (const [fetcher, code] of [[async () => { throw new TypeError('secret URL'); }, '108'],
    [async () => new Response(''), '112'], [async () => new Response('<html>Login token=secret</html>'), '111'],
    [async () => new Response('<svg></svg>'), '113'], [async () => new Response(png, { headers: { 'content-length': '100' } }), '116']])
    await assert.rejects(fetchImportImage(url, { fetcher }), { code: `LUP-IMP-${code}` });
});
test('declared and streaming oversize bodies stop at the input limit', async () => {
  await assert.rejects(fetchImportImage(url, { fetcher: async () => new Response(png, { headers: { 'content-length': '9999999' } }) }), { code: 'LUP-IMP-110' });
  let cancelled = false;
  const stream = new ReadableStream({ start(controller) { controller.enqueue(new Uint8Array(8 * 1024 * 1024)); controller.enqueue(new Uint8Array(1)); }, cancel() { cancelled = true; } });
  await assert.rejects(fetchImportImage(url, { fetcher: async () => new Response(stream) }), { code: 'LUP-IMP-110' });
  assert(cancelled);
});
test('timeout includes a stalled response body, not only the initial request', async () => {
  let cancelled = false;
  const stream = new ReadableStream({ start(controller) { controller.enqueue(png); }, cancel() { cancelled = true; } });
  await assert.rejects(fetchImportImage(url, { timeoutMs: 20, fetcher: async () => new Response(stream) }), { code: 'LUP-IMP-109' });
  assert(cancelled);
});
test('copyable diagnostics cannot expose URL queries, server bodies, keys or caller text', () => {
  const value = new ImportFailure('LUP-IMP-104', { status: 403, host: 'images.example.test', method: 'direct', url, key: 'lup_SECRET', body: 'SECRET_BODY' });
  value.stack = 'SECRET_STACK'; value.message = 'SECRET_MESSAGE';
  const report = importDiagnostic(value, '0.6.0');
  assert.match(report, /LUP-IMP-104/); assert.match(report, /HTTP-status: 403/); assert.match(report, /images.example.test/);
  for (const forbidden of ['privateToken', 'NEVER_COPY', 'lup_SECRET', 'SECRET_BODY', 'SECRET_STACK', 'SECRET_MESSAGE']) assert(!report.includes(forbidden));
});
test('embedded and local source schemes never become privileged download targets', async () => {
  for (const value of ['blob:https://example.test/id', 'data:image/png;base64,AAAA', 'file:///secret', 'https://user:secret@example.test/p.png'])
    await assert.rejects(fetchImportImage(value), { code: 'LUP-IMP-008' });
  assert.equal(sniffImageType(Uint8Array.from([71, 73, 70, 56, 57, 97])), 'image/gif');
});
