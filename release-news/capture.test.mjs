import test from 'node:test';
import assert from 'node:assert/strict';
import { browserSeed, fixtureResponse, ids, FIXED_TIME } from './fixtures.mjs';
import { captureBase, classifyCaptureRequest, createCaptureRouteHandler, createStaticReader, captureBounds,
  pngDimensions, captureScenarios, scenarios, VIEWPORT, MAX_IMAGE_BYTES } from './capture.mjs';

const base = 'http://web:3000/';
function requestRoute(url, method = 'GET', navigation = false) {
  const seen = {};
  return { seen, request: () => ({ url: () => url, method: () => method, isNavigationRequest: () => navigation,
    headers: () => ({ authorization: 'Bearer must-not-forward', cookie: 'must-not-forward', 'x-service-key': 'must-not-forward' }) }),
  fulfill: async response => { seen.fulfilled = response; }, abort: async reason => { seen.aborted = reason; } };
}

test('only the configured web origin and registered routes/public static prefixes can reach a web fetch', () => {
  assert.equal(classifyCaptureRequest({ url: 'http://web:3000/_framework/dotnet.native.wasm' }, base), 'static');
  assert.equal(classifyCaptureRequest({ url: 'http://web:3000/js/notion-graph.js?v=connections-20261108' }, base), 'static');
  // EditorAssets combines the JS and CSS SHA256 hashes into one 129-character cache version.
  assert.equal(classifyCaptureRequest({ url: `http://web:3000/notion-editor/editor.js?v=${'A'.repeat(64)}-${'B'.repeat(64)}` }, base), 'static');
  assert.equal(classifyCaptureRequest({ url: 'http://web:3000/settings', navigation: true }, base), 'static');
  for (const url of ['https://mercantec.tech/api/users', 'http://other:3000/images/avatar.png', 'http://web:3000/admin',
    'http://web:3000/images/%2f..%2fapi/users', 'http://web:3000/images/x.png?access_token=sensitive',
    'http://web:3000/service-worker.js', 'http://web:3000/unknown-route']) {
    assert.equal(classifyCaptureRequest({ url }, base), 'blocked', url);
  }
  assert.equal(classifyCaptureRequest({ url: 'http://web:3000/settings', method: 'POST', navigation: true }, base), 'blocked');
  for (const value of ['file:///etc/passwd', 'http://user:password@web:3000/', 'http://web:3000/api/', 'http://web:3000/?key=value'])
    assert.throws(() => captureBase(value));
});

test('API reads and mutations are local fixtures; unknown endpoints never call the web API', async () => {
  const fetched = [];
  const handler = createCaptureRouteHandler({ baseUrl: base, readStatic: async url => { fetched.push(url); throw new Error('not expected'); } });
  for (const [path, method] of [['api/me/avatars', 'GET'], ['api/notebooks/mine', 'POST'], ['api/references/graph', 'POST'],
    ['api/private-user-data', 'GET'], ['api/private-user-data', 'DELETE']]) {
    const route = requestRoute(new URL(path, base).href, method);
    await handler(route);
    assert.ok(route.seen.fulfilled, `${method} ${path}`);
    assert.equal(route.seen.aborted, undefined);
  }
  assert.deepEqual(fetched, []);
  assert.equal(fixtureResponse('/api/private-user-data', 'GET').status, 404);
  assert.equal(fixtureResponse('/api/private-user-data', 'POST').status, 404);
});

test('static transport never forwards browser credentials or follows a redirect', async () => {
  const calls = [];
  const readStatic = createStaticReader(base, async (url, init) => { calls.push({ url, init });
    return new Response('export const rendered=true;', { headers: { 'content-type': 'text/javascript' } }); });
  const handler = createCaptureRouteHandler({ baseUrl: base, readStatic });
  const route = requestRoute('http://web:3000/js/lup-ui.js');
  await handler(route);
  assert.equal(calls.length, 1);
  assert.equal(calls[0].init.method, 'GET');
  assert.equal(calls[0].init.credentials, 'omit');
  assert.equal(calls[0].init.redirect, 'error');
  assert.deepEqual(Object.keys(calls[0].init.headers).sort(), ['accept', 'accept-encoding']);
  assert.equal(JSON.stringify(calls).includes('must-not-forward'), false);
  assert.equal(route.seen.fulfilled.contentType, 'text/javascript');
  await assert.rejects(readStatic('http://web:3000/api/users'));
});

test('external requests and authentication endpoints are aborted without even reading a fixture', async () => {
  let fixtureCalls = 0, staticCalls = 0;
  const handler = createCaptureRouteHandler({ baseUrl: base, fixtures: () => { fixtureCalls++; }, readStatic: () => { staticCalls++; } });
  for (const url of ['https://auth.example/oauth/authorize', 'https://mercantec.tech/api/me', 'http://web:3000/auth/callback?code=private']) {
    const route = requestRoute(url, 'GET', true);
    await handler(route);
    assert.equal(route.seen.aborted, 'blockedbyclient');
    assert.equal(route.seen.fulfilled, undefined);
  }
  assert.equal(fixtureCalls, 0); assert.equal(staticCalls, 0);
});

test('even fixture image indirection must name a recognized public static resource', async () => {
  const calls = [];
  const handler = createCaptureRouteHandler({ baseUrl: base, fixtures: () => ({ status: 200, staticPath: '/api/users', contentType: 'image/webp' }),
    readStatic: async url => { calls.push(url); return { body: Buffer.alloc(1) }; } });
  const route = requestRoute('http://web:3000/api/platform-updates/images/example');
  await handler(route);
  assert.equal(route.seen.aborted, 'blockedbyclient');
  assert.deepEqual(calls, []);
});

test('rejected static reads never place their URL, headers or error in a browser response', async () => {
  const handler = createCaptureRouteHandler({ baseUrl: base, readStatic: async () => {
    throw new Error('sensitive-host-details and worker-service-key must stay private');
  } });
  const route = requestRoute('http://web:3000/notion-editor/editor.js');
  await handler(route);
  assert.deepEqual(route.seen, { aborted: 'failed' });
  const settings = requestRoute('http://web:3000/appsettings.Production.json');
  await handler(settings);
  const data = JSON.parse(settings.seen.fulfilled.body);
  assert.equal(data.ApiBaseUrl, `${base}api/`);
  assert.equal(data.Auth.Authority, 'https://invalid.example');
  assert.equal(JSON.stringify(data).includes('ServiceKey'), false);
});

test('browser state contains only the fixed fake identity and never a worker service key', () => {
  const before = process.env.RELEASE_NEWS_SERVICE_KEY;
  process.env.RELEASE_NEWS_SERVICE_KEY = 'canary-worker-service-key-not-for-browser';
  try {
    const seed = browserSeed();
    const claims = JSON.parse(Buffer.from(seed.token.split('.')[1], 'base64url').toString());
    assert.equal(claims.sub, ids.teacher); assert.equal(claims.role, 'Teacher');
    assert.equal(claims.email, 'underviser@example.test');
    assert.equal(seed.fixedTime, FIXED_TIME);
    assert.equal(JSON.stringify(seed).includes(process.env.RELEASE_NEWS_SERVICE_KEY), false);
    assert.deepEqual(seed, browserSeed());
    assert.equal(browserSeed({ guest: true }).token, null);
  } finally {
    if (before === undefined) delete process.env.RELEASE_NEWS_SERVICE_KEY; else process.env.RELEASE_NEWS_SERVICE_KEY = before;
  }
});

test('focused clips stay inside the DPR-one viewport and never exceed 1100 pixels high', () => {
  assert.deepEqual(captureBounds({ x: -30, y: -20, width: 1600, height: 9000 }), { x: 0, y: 0, width: 1440, height: 1100 });
  assert.deepEqual(captureBounds({ x: 120.4, y: 40.8, width: 700.2, height: 3000 }, VIEWPORT, 600), { x: 120, y: 40, width: 701, height: 600 });
  assert.throws(() => captureBounds({ x: 1450, y: 10, width: 500, height: 100 }));
  assert.throws(() => captureBounds({ x: 10, y: 10, width: 0, height: 0 }));
  assert.equal(MAX_IMAGE_BYTES, 2 * 1024 * 1024);
});

test('PNG validation rejects broken images and oversized or empty raster dimensions', () => {
  const png = Buffer.alloc(33); Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]).copy(png);
  png.write('IHDR', 12); png.writeUInt32BE(800, 16); png.writeUInt32BE(600, 20);
  assert.deepEqual(pngDimensions(png), { width: 800, height: 600 });
  png.writeUInt32BE(3000, 20); assert.throws(() => pngDimensions(png));
  assert.throws(() => pngDimensions(Buffer.from('not a screenshot')));
});

test('unknown scenario IDs never launch a browser, leak an error, write a file or supply an unrelated image', async () => {
  const result = await captureScenarios({ baseUrl: 'http://invalid.example/', outputDir: '/not-used',
    scenarioIds: ['not-registered', '../private', '__proto__'] });
  assert.equal(Object.getPrototypeOf(result), null);
  for (const value of Object.values(result)) assert.deepEqual(value, { ok: false, reason: 'unsupported_scenario' });
  assert.equal(Object.values(result).some(value => value.path), false);
});

test('fixtures and metadata use matching hold/workspace IDs and safe stable filenames', () => {
  assert.equal(scenarios.length, 35);
  assert.equal(new Set(scenarios.map(scenario => scenario.id)).size, scenarios.length);
  for (const scenario of scenarios) assert.match(scenario.id, /^[a-z0-9]+(?:-[a-z0-9]+)*$/);
  const route = scenarios.find(scenario => scenario.id === 'hold-overview').route;
  assert.equal(route, `/courses/${ids.workspace}`);
  const workspace = JSON.parse(fixtureResponse(`/api/hold-workspace/${ids.workspace}`).body);
  assert.equal(workspace.holdId, ids.hold);
  assert.equal(JSON.parse(fixtureResponse('/api/notebooks/mine', 'POST').body).ownerSub, ids.teacher);
});
