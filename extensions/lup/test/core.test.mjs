import test from 'node:test';
import assert from 'node:assert/strict';
import {
  MAX_BYTES, MAX_DOWNLOAD_BYTES, MAX_PIXELS, TTL_MS,
  validId, sanitizeName, sourceUrl, originPattern, strictBase64Size,
  validDraft, trustedDestination, validateDraftAccess, validateDraftCompletion, pageMessage,
} from '../core.mjs';

const id = 'a1234567-89ab-4cde-8f01-23456789abcd';
const otherId = 'b1234567-89ab-4cde-8f01-23456789abcd';
const now = 1_800_000_000_000;
const sender = (url = `https://lup.dk/emojis?lupImport=${id}`, tabId = 42) => ({ url, tab: { id: tabId } });
const draft = (changes = {}) => ({
  id, name: 'min_emoji', contentType: 'image/png', base64: 'YQ==',
  expiresAt: now + TTL_MS, destinationTabId: 42, ...changes,
});

test('resource limits match the LUP upload boundary and bounded source capture', () => {
  assert.equal(MAX_BYTES, 256 * 1024);
  assert.equal(MAX_DOWNLOAD_BYTES, 8 * 1024 * 1024);
  assert.equal(MAX_PIXELS, 16 * 1024 * 1024);
  assert.equal(TTL_MS, 30 * 60 * 1000);
});

test('import IDs accept canonical lowercase UUIDs and reject malformed values', () => {
  assert.equal(validId(id), true);
  assert.equal(validId('00000000-0000-0000-0000-000000000000'), true);
  for (const value of [id.toUpperCase(), id.replaceAll('-', ''), `x${id}`, `${id}x`, '', null, {}, [], 42]) {
    assert.equal(validId(value), false, String(value));
  }
});

test('suggested names transliterate Danish letters and discard a file extension', () => {
  assert.equal(sanitizeName('Åse Søren Æble.PNG'), 'ase_soren_aeble');
  assert.equal(sanitizeName('Sødt-billede.webp'), 'sodt-billede');
  assert.equal(sanitizeName('École.jpg'), 'ecole');
});

test('suggested names remain bounded safe shortcodes and have a usable fallback', () => {
  for (const value of ['a'.repeat(200), 'Æ'.repeat(100), '../../BILLEDE.png', 'C:\\temp\\billede.jpeg', 'min   emoji']) {
    const name = sanitizeName(value);
    assert.match(name, /^[a-z0-9_-]{2,32}$/);
  }
  assert.equal(sanitizeName(''), 'emoji');
  assert.equal(sanitizeName('🔥.png'), 'emoji');
  assert.equal(sanitizeName('a.png'), 'emoji');
});

test('HTTP sources produce origin permissions without copying their path or query', () => {
  const url = sourceUrl('https://images.example.com:8443/photos/one.png?token=value#preview');
  assert.ok(url instanceof URL);
  assert.equal(url.hostname, 'images.example.com');
  assert.equal(originPattern(url.href), 'https://images.example.com:8443/*');
  assert.equal(originPattern('http://images.example.com/image.jpg'), 'http://images.example.com/*');
});

test('unsafe, unsupported and unbounded source URLs are rejected', () => {
  for (const value of [
    'javascript:alert(1)', 'file:///C:/image.png', 'data:image/png;base64,YQ==',
    'blob:https://example.com/id', 'ftp://example.com/a.png',
    'https://user:password@example.com/a.png', 'https://user@example.com/a.png',
    'https://example.com/' + 'a'.repeat(8192), '', 'not a url', null, {},
  ]) {
    assert.equal(sourceUrl(value), null, String(value));
    assert.equal(originPattern(value), null, String(value));
  }
});

test('base64 must be canonical and contain bytes', () => {
  assert.equal(strictBase64Size('YQ=='), 1);
  assert.equal(strictBase64Size('YWI='), 2);
  assert.equal(strictBase64Size('YWJj'), 3);
  for (const value of ['', 'YQ', 'YQ=', 'YQ===', 'YR==', 'YQ==\n', 'YQ ==', '@@@@', 'data:image/png;base64,YQ==', null, {}]) {
    assert.equal(strictBase64Size(value), 0, String(value));
  }
});

test('drafts accept each API-supported image content type', () => {
  for (const contentType of ['image/png', 'image/jpeg', 'image/gif', 'image/webp']) {
    assert.equal(validDraft(draft({ contentType }), now), true, contentType);
  }
});

test('draft storage accepts exactly the maximum image size and rejects one excess byte', () => {
  assert.equal(validDraft(draft({ base64: Buffer.alloc(MAX_BYTES).toString('base64') }), now), true);
  assert.equal(validDraft(draft({ base64: Buffer.alloc(MAX_BYTES + 1).toString('base64') }), now), false);
});

test('drafts expire at the exact expiry boundary and cannot extend their retention', () => {
  assert.equal(validDraft(draft({ expiresAt: now + 1 }), now), true);
  for (const expiresAt of [now - 1, now, now + TTL_MS + 1, Infinity, NaN, String(now + 1)]) {
    assert.equal(validDraft(draft({ expiresAt }), now), false, String(expiresAt));
  }
});

test('invalid draft shapes are rejected before bytes can leave the extension', () => {
  for (const value of [
    null, {}, draft({ id: 'wrong' }), draft({ name: '' }), draft({ name: 'a'.repeat(33) }),
    draft({ name: 'bad name' }), draft({ contentType: 'image/svg+xml' }),
    draft({ contentType: 'text/html' }), draft({ base64: '' }), draft({ base64: 'YR==' }),
    draft({ destinationTabId: -1 }), draft({ destinationTabId: 1.5 }), draft({ destinationTabId: undefined }),
  ]) {
    assert.equal(validDraft(value, now), false);
  }
});

test('the destination allows only the canonical HTTPS LUP emoji page and exact import ID', () => {
  assert.equal(trustedDestination(sender(), id), true);
  assert.equal(trustedDestination(sender(`https://lup.dk/emojis?filter=mine&lupImport=${id}`), id), true);
  for (const url of [
    `http://lup.dk/emojis?lupImport=${id}`,
    `https://www.lup.dk/emojis?lupImport=${id}`,
    `https://lup.dk.example.com/emojis?lupImport=${id}`,
    `https://example.com/emojis?lupImport=${id}`,
    `https://lup.dk:8443/emojis?lupImport=${id}`,
    `https://lup.dk/emojis/?lupImport=${id}`,
    `https://lup.dk/login?lupImport=${id}`,
    `https://lup.dk/emojis?lupImport=${otherId}`,
    `https://lup.dk/emojis?lupImport=${id}&lupImport=${id}`,
    'https://lup.dk/emojis', 'about:blank', 'not a url',
  ]) {
    assert.equal(trustedDestination(sender(url), id), false, url);
  }
});

test('destination requests require a real tab and a well formed import ID', () => {
  assert.equal(trustedDestination({}, id), false);
  assert.equal(trustedDestination({ url: sender().url }, id), false);
  assert.equal(trustedDestination(sender(undefined, -1), id), false);
  assert.equal(trustedDestination(sender(undefined, 42.5), id), false);
  assert.equal(trustedDestination(sender(), 'invalid'), false);
});

test('draft access binds the import ID and destination tab, including after page reload', () => {
  assert.equal(validateDraftAccess(draft(), sender(), id, now), true);
  assert.equal(validateDraftAccess(draft(), { ...sender(), documentId: 'new-document' }, id, now), true);
  assert.equal(validateDraftAccess(draft(), sender(undefined, 43), id, now), false);
  assert.equal(validateDraftAccess(draft({ id: otherId }), sender(), id, now), false);
  assert.equal(validateDraftAccess(draft(), sender(), otherId, now), false);
  assert.equal(validateDraftAccess(draft(), sender(), id, now + TTL_MS), false);
});

test('completion can remove an import after the page removes its query, but remains bound to LUP and its tab', () => {
  assert.equal(validateDraftCompletion(draft(), sender(), id, now), true);
  assert.equal(validateDraftCompletion(draft(), sender('https://lup.dk/emojis'), id, now), true);
  assert.equal(validateDraftCompletion(draft(), sender('https://lup.dk/emojis?filter=mine'), id, now), true);
  for (const candidate of [
    sender('https://lup.dk/emojis', 43), sender('https://example.com/emojis'),
    sender('http://lup.dk/emojis'), sender('https://www.lup.dk/emojis'),
    sender('https://lup.dk/teacher'), {},
  ]) {
    assert.equal(validateDraftCompletion(draft(), candidate, id, now), false);
  }
  assert.equal(validateDraftCompletion(draft(), sender('https://lup.dk/emojis'), otherId, now), false);
  assert.equal(validateDraftCompletion(draft(), sender('https://lup.dk/emojis'), id, now + TTL_MS), false);
  assert.equal(validateDraftCompletion(draft({ base64: 'invalid' }), sender(), id, now), false);
  // Removing the query weakens only completion, never access to the stored bytes.
  assert.equal(validateDraftAccess(draft(), sender('https://lup.dk/emojis'), id, now), false);
});

test('the page protocol recognizes only explicit request and complete messages', () => {
  assert.equal(pageMessage({ source: 'lup-emoji-page', type: 'request', id }), true);
  assert.equal(pageMessage({ source: 'lup-emoji-page', type: 'complete', id }), true);
  for (const value of [
    null, {}, [], 'request',
    { source: 'lup-emoji-extension', type: 'request', id },
    { source: 'lup-emoji-page', type: 'upload', id },
    { source: 'lup-emoji-page', type: 'request', id: 'invalid' },
    { source: 'lup-emoji-page', type: 'request' },
  ]) {
    assert.equal(pageMessage(value), false);
  }
});
