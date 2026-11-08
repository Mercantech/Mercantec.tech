import test from 'node:test';
import assert from 'node:assert/strict';
import { entityReference, legacyReference, createReferenceClient, createReferenceSearch, createReferenceResolver } from '../src/entity-references.mjs';

const id = '11111111-2222-4333-8444-555555555555';
const other = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
const deferred = () => { let resolve; const promise = new Promise(value => { resolve = value; }); return { promise, resolve }; };
const tick = () => new Promise(resolve => setTimeout(resolve, 5));

test('legacy page identities preserve their original domain and typed slugs stay stable', () => {
  assert.deepEqual(legacyReference('pageMention', { pageId: id }, 'notebook'), { kind: 'notebook-page', id });
  assert.deepEqual(legacyReference('pageLink', { pageId: id }), { kind: 'notion-page', id });
  assert.deepEqual(legacyReference('studentMention', { membershipId: id, holdId: other }), { kind: 'member', id });
  assert.deepEqual(entityReference('lesson', 'database/joins'), { kind: 'lesson', id: 'database/joins' });
  assert.equal(entityReference('InvalidKind', id), null);
  assert.equal(entityReference('person', '\nsecret'), null);
});

test('reader-specific resolution uses canonical target links and rejects unrelated or unsafe responses', async () => {
  const reference = { kind: 'notebook-page', id: other };
  const client = createReferenceClient({ getToken: async () => 'token', sourceKind: 'notion-page', sourceId: id, holdId: id, publishedOnly: true, fetchImpl: async (url, request) => {
    assert.equal(url, '/api/references/resolve');
    assert.equal(request.headers.Authorization, 'Bearer token');
    assert.equal(request.cache, 'no-store');
    assert.deepEqual(JSON.parse(request.body), { references: [reference], sourceKind: 'notion-page', sourceId: id, publishedOnly: true });
    return Response.json([
      { reference, title: 'Tilladt note', href: '/notebook/' + other, contextLabel: 'Delt notesbog' },
      { reference: { kind: 'notebook-page', id }, title: 'Ikke efterspurgt', href: '/notebook/' + id },
      { reference, title: 'Usikker destination', href: '//example.com/private' },
      { reference, title: 'Usikker destination', href: 'javascript:alert(1)' },
    ]);
  }});
  const result = await client.resolve([reference]);
  assert.equal(result.length, 1);
  assert.equal(result[0].href, '/notebook/' + other);
  assert.doesNotMatch(result[0].href, /hold=|workspace=/);
});

test('global search passes source preferences without treating them as navigation paths', async () => {
  const client = createReferenceClient({ getToken: async () => 'Bearer token', sourceKind: 'notebook-page', sourceId: id, holdId: other, fetchImpl: async (url, request) => {
    const parsed = new URL(url, 'https://lup.invalid');
    assert.equal(parsed.searchParams.get('q'), 'SQL & C#');
    assert.equal(parsed.searchParams.get('holdId'), other);
    assert.equal(parsed.searchParams.get('sourceKind'), 'notebook-page');
    assert.equal(parsed.searchParams.get('sourceId'), id);
    assert.equal(request.headers.Authorization, 'Bearer token');
    return Response.json({ items: [], total: 0 });
  }});
  await client.search('SQL & C#');
});

test('superseded debounced and in-flight searches settle and cannot return stale matches', async () => {
  const pending = deferred(); let calls = 0, firstSignal;
  const search = createReferenceSearch({ search: async (query, signal) => {
    calls++;
    if (query === 'first') { firstSignal = signal; return pending.promise; }
    return { items: [{ title: query }], total: 1 };
  }}, 1);
  const cancelledBeforeStart = search.search('never-starts');
  const first = search.search('first');
  await tick();
  const latest = search.search('latest');
  assert.deepEqual(await cancelledBeforeStart, { items: [], total: 0 });
  assert.deepEqual(await first, { items: [], total: 0 });
  assert.equal(firstSignal.aborted, true);
  assert.equal((await latest).items[0].title, 'latest');
  pending.resolve({ items: [{ title: 'stale secret' }], total: 1 });
  await tick(); assert.equal(calls, 2); search.destroy();
});

test('visible references batch and deduplicate; omitted targets remain unavailable without saved labels', async () => {
  const batches = [], states = [];
  const resolver = createReferenceResolver({ resolve: async references => {
    batches.push(references);
    return references.filter(ref => ref.id !== other).map(reference => ({ reference, title: 'Visible', href: '/people/' + reference.id }));
  }});
  resolver.subscribe({ kind: 'person', id }, state => states.push(state));
  resolver.subscribe({ kind: 'person', id }, () => {});
  let inaccessible;
  resolver.subscribe({ kind: 'person', id: other }, state => { inaccessible = state; });
  await tick();
  assert.equal(batches.length, 1); assert.equal(batches[0].length, 2);
  assert.equal(states.at(-1).value.title, 'Visible');
  assert.deepEqual(inaccessible, { status: 'ready', value: null });
  resolver.destroy();
});

test('reference batches stay bounded and destroyed documents ignore pending results', async () => {
  const batches = [];
  const resolver = createReferenceResolver({ resolve: async references => { batches.push(references.length); return []; } });
  for (let index = 0; index < 235; index++) resolver.subscribe({ kind: 'subject', id: String(index) }, () => {});
  await tick();
  assert.deepEqual(batches, [100, 100, 35]); resolver.destroy();
  const pending = deferred(); let callbacks = 0;
  const closed = createReferenceResolver({ resolve: () => pending.promise });
  closed.subscribe({ kind: 'person', id }, () => { callbacks++; });
  await tick(); closed.destroy(); pending.resolve([{ reference: { kind: 'person', id }, title: 'Old document' }]);
  await tick(); assert.equal(callbacks, 1);
});

test('retry discards a delayed old response and a refreshed revoked target loses its title', async () => {
  const old = deferred(), states = []; let calls = 0;
  const reference = { kind: 'person', id };
  const resolver = createReferenceResolver({ resolve: async () => ++calls === 1 ? old.promise : [] });
  resolver.subscribe(reference, state => states.push(state)); await tick();
  resolver.retry(reference); await tick();
  assert.deepEqual(states.at(-1), { status: 'ready', value: null });
  old.resolve([{ reference, title: 'Now private', href: '/people/' + id }]); await tick();
  assert.deepEqual(states.at(-1), { status: 'ready', value: null }); resolver.destroy();

  let revoked = false;
  const refreshed = createReferenceResolver({ resolve: async () => revoked ? [] : [{ reference, title: 'Previously visible', href: '/people/' + id }] }, 20);
  const updates = []; refreshed.subscribe(reference, state => updates.push(state)); await tick();
  assert.equal(updates.at(-1).value.title, 'Previously visible'); revoked = true;
  await new Promise(resolve => setTimeout(resolve, 30));
  assert.equal(updates.at(-1).value, null); refreshed.destroy();
});
