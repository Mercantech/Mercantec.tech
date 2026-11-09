import { readFileSync } from 'node:fs';
import { mkdir, realpath, rename, unlink, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { pathToFileURL } from 'node:url';
import { browserSeed, fixtureResponse, FIXED_TIME } from './fixtures.mjs';

export const scenarios = Object.freeze(JSON.parse(readFileSync(new URL('./scenarios.json', import.meta.url), 'utf8'))
  .map(scenario => Object.freeze(scenario)));
const byId = new Map(scenarios.map(scenario => [scenario.id, scenario]));
const navigationPaths = new Set([...scenarios.map(scenario => new URL(scenario.route, 'http://fixture.invalid').pathname), '/student']);
export const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
export const VIEWPORT = Object.freeze({ width: 1440, height: 1100 });
const ROUTE_TIMEOUT = 25_000;
const WHOLE_TIMEOUT = 280_000;
const MAX_STATIC_BYTES = 48 * 1024 * 1024;
const STATIC_CACHE_BYTES = 128 * 1024 * 1024;
const knownReasons = new Set(['capture_unavailable', 'capture_timeout', 'capture_not_ready', 'image_too_large', 'capture_budget_exhausted']);

class CaptureFailure extends Error {
  constructor(code) { super(code); this.code = code; }
}

export function captureBase(value) {
  let url;
  try { url = new URL(value); } catch { throw new TypeError('A capture base must be an HTTP origin.'); }
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.pathname !== '/' || url.search || url.hash)
    throw new TypeError('A capture base must be an HTTP origin.');
  return url;
}

/** No request headers, cookies, API keys or browser tokens are ever forwarded to the web host. */
export function classifyCaptureRequest({ url: input, method = 'GET', navigation = false }, baseUrl) {
  const base = captureBase(baseUrl);
  let url;
  try { url = new URL(input); } catch { return 'blocked'; }
  if (url.origin !== base.origin || url.username || url.password || url.hash || /[\\%]/.test(url.pathname)) return 'blocked';
  if (/^\/api(?:\/|$)/i.test(url.pathname) || /^\/hubs(?:\/|$)/i.test(url.pathname)) return 'fixture';
  if (/^\/appsettings(?:\.[A-Za-z0-9_-]+)?\.json$/.test(url.pathname)) return 'settings';
  if (!['GET', 'HEAD'].includes(method)) return 'blocked';
  if (/^\/service-worker(?:-assets|\.published)?\.js$/i.test(url.pathname)) return 'blocked';
  if (navigation) return navigationPaths.has(url.pathname) ? 'static' : 'blocked';
  const prefix = /^\/(?:_framework|_content|css|js|images|lup|assets|fonts|lib|notion-editor|image-tools|content)\//;
  const extension = /\.(?:js|mjs|css|json|wasm|dll|pdb|dat|bin|woff2?|ttf|otf|svg|png|jpe?g|gif|webp|ico|avif|md|txt|html)$/i;
  const rootAsset = /^\/(?:[A-Za-z0-9_.-]+\.styles\.css|favicon\.(?:png|ico)|icon-\d+\.png|manifest\.webmanifest)$/;
  if (!(prefix.test(url.pathname) && extension.test(url.pathname)) && !rootAsset.test(url.pathname)) return 'blocked';
  // Cache-busting is sufficient for static assets; access-token or arbitrary query channels are not forwarded.
  for (const [key, value] of url.searchParams) if (!['v', 'version', 'hash'].includes(key) || !/^[A-Za-z0-9_.-]{0,256}$/.test(value)) return 'blocked';
  return 'static';
}

async function limitedBody(response) {
  if (Number(response.headers.get('content-length')) > MAX_STATIC_BYTES) throw new CaptureFailure('capture_unavailable');
  if (!response.body?.getReader) {
    const body = Buffer.from(await response.arrayBuffer());
    if (body.length > MAX_STATIC_BYTES) throw new CaptureFailure('capture_unavailable');
    return body;
  }
  const reader = response.body.getReader(), chunks = [];
  let size = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.length;
      if (size > MAX_STATIC_BYTES) { await reader.cancel(); throw new CaptureFailure('capture_unavailable'); }
      chunks.push(Buffer.from(value));
    }
  } finally { reader.releaseLock(); }
  return Buffer.concat(chunks, size);
}

export function createStaticReader(baseUrl, fetchImpl = fetch) {
  const cache = new Map();
  let cachedBytes = 0;
  return async url => {
    // This is a second gate, including fixture images which name a public static source.
    if (classifyCaptureRequest({ url, method: 'GET', navigation: navigationPaths.has(new URL(url).pathname) }, baseUrl) !== 'static')
      throw new CaptureFailure('capture_unavailable');
    if (cache.has(url)) return cache.get(url);
    const pending = (async () => {
      const response = await fetchImpl(url, { method: 'GET', redirect: 'error', credentials: 'omit',
        headers: { 'accept-encoding': 'identity', accept: '*/*' }, signal: AbortSignal.timeout(8_000) });
      if (!response.ok) throw new CaptureFailure('capture_unavailable');
      const body = await limitedBody(response);
      cachedBytes += body.length;
      while (cachedBytes > STATIC_CACHE_BYTES && cache.size > 1) {
        const oldest = cache.keys().next().value;
        const old = cache.get(oldest);
        cache.delete(oldest);
        old.then(value => { cachedBytes -= value.body.length; }).catch(() => {});
      }
      return { status: response.status, contentType: response.headers.get('content-type') || 'application/octet-stream',
        headers: { 'cache-control': 'no-store' }, body };
    })();
    cache.set(url, pending);
    try { return await pending; } catch (error) { cache.delete(url); throw error; }
  };
}

export function createCaptureRouteHandler({ baseUrl, readStatic, fixtures = fixtureResponse }) {
  const base = captureBase(baseUrl);
  return async route => {
    try {
      const request = route.request();
      const url = request.url(), method = request.method();
      const kind = classifyCaptureRequest({ url, method, navigation: request.isNavigationRequest() }, base);
      if (kind === 'blocked') return await route.abort('blockedbyclient');
      if (kind === 'settings') return await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
        ApiBaseUrl: `${base.origin}/api/`, Auth: { ClientId: 'capture-fixture-only', Authority: 'https://invalid.example', RedirectPath: '/auth/callback' }
      }) });
      if (kind === 'fixture') {
        const value = fixtures(url, method);
        if (value?.staticPath) {
          const source = new URL(value.staticPath, base);
          if (classifyCaptureRequest({ url: source.href, method: 'GET' }, base) !== 'static') return await route.abort('blockedbyclient');
          const asset = await readStatic(source.href);
          return await route.fulfill({ status: value.status, contentType: value.contentType, body: asset.body });
        }
        return await route.fulfill(value || { status: 404, contentType: 'application/json', body: '{"error":"capture_fixture_route_missing"}' });
      }
      return await route.fulfill(await readStatic(url));
    } catch {
      // Network errors and closed pages must not expose a URL, token, header or stack to release content.
      try { await route.abort('failed'); } catch { /* The page may have closed at its route deadline. */ }
    }
  };
}

export function captureBounds(rect, viewport = VIEWPORT, heightLimit = viewport.height) {
  if (!rect || ![rect.x, rect.y, rect.width, rect.height].every(Number.isFinite) || rect.width <= 0 || rect.height <= 0)
    throw new CaptureFailure('capture_not_ready');
  const x = Math.max(0, Math.floor(rect.x)), y = Math.max(0, Math.floor(rect.y));
  const right = Math.min(viewport.width, Math.ceil(rect.x + rect.width));
  const bottom = Math.min(viewport.height, Math.ceil(rect.y + rect.height), y + Math.floor(heightLimit));
  if (right - x < 120 || bottom - y < 80) throw new CaptureFailure('capture_not_ready');
  return { x, y, width: right - x, height: bottom - y };
}

export function pngDimensions(buffer) {
  if (!Buffer.isBuffer(buffer) || buffer.length < 33 || !buffer.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
    || buffer.toString('ascii', 12, 16) !== 'IHDR') throw new CaptureFailure('capture_not_ready');
  const width = buffer.readUInt32BE(16), height = buffer.readUInt32BE(20);
  if (!width || !height || width > VIEWPORT.width || height > VIEWPORT.height) throw new CaptureFailure('capture_not_ready');
  return { width, height };
}

function initializeBrowser(seed) {
  localStorage.clear(); sessionStorage.clear();
  localStorage.setItem('lup-theme', seed.theme);
  localStorage.setItem('lup-course', seed.course);
  if (seed.token) {
    sessionStorage.setItem('mt_access_token', seed.token);
    sessionStorage.setItem('mt_refresh_token', '');
    sessionStorage.setItem('mt_expires_at', seed.expiresAt);
  }
  let state = 424242;
  Math.random = () => { state ^= state << 13; state ^= state >>> 17; state ^= state << 5; return (state >>> 0) / 4294967296; };
}

async function prepare(page, id) {
  if (id === 'editor') await page.locator('.notion-editor-root').getByText('Forbind sensoren, send målingen med MQTT', { exact: false }).waitFor();
  if (id === 'themes') await page.locator('.theme-trigger').first().click();
  if (id === 'lectio') await page.getByRole('tab', { name: 'Elever', exact: true }).click();
  if (id === 'lectio-linking') {
    await page.getByRole('tab', { name: 'Kobling', exact: true }).click();
    await page.getByRole('heading', { name: 'Manuel Auth ↔ Lectio-kobling', exact: true }).waitFor();
    const columns = page.locator('.lectio-link-col');
    await columns.nth(0).getByPlaceholder('Søg i elevlisten eller indtast Lectio-id').fill('Anna');
    await columns.nth(0).locator('.lectio-pick').filter({ hasText: 'Anna Nielsen' }).click();
    await columns.nth(1).getByPlaceholder('Søg navn eller e-mail').fill('Anna');
    await columns.nth(1).locator('.lectio-pick').filter({ hasText: 'Anna Nielsen' }).click();
  }
  if (id === 'notebook-graph') {
    await page.getByRole('button', { name: 'Åbn forbindelser og graf', exact: true }).click();
  }
  if (id === 'notebook-graph' || id === 'page-graph') {
    await page.locator('.reference-canvas canvas').waitFor({ state: 'visible' });
    for (let index = 0; index < 3; index++) await page.getByRole('button', { name: 'Zoom ind', exact: true }).click();
  }
  if (id === 'assignment-feedback') {
    const detail = page.locator('.assignment-revision').first();
    if (!await detail.evaluate(element => element.open)) await detail.locator('summary').click();
  }
}

async function regionReady(page, selector) {
  const locator = page.locator(selector).first();
  await locator.waitFor({ state: 'visible' });
  await locator.evaluate(element => window.scrollTo(0, Math.max(0, window.scrollY + element.getBoundingClientRect().top - 12)));
  await page.evaluate(async () => {
    await Promise.race([document.fonts.ready, new Promise(resolve => setTimeout(resolve, 500))]);
    const visible = [...document.images].filter(image => { const rect = image.getBoundingClientRect(); return rect.top < innerHeight && rect.bottom > 0; });
    await Promise.race([Promise.all(visible.map(image => image.decode().catch(() => {}))), new Promise(resolve => setTimeout(resolve, 1_500))]);
    await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  });
  await page.waitForFunction(selector => {
    const element = document.querySelector(selector);
    if (!element) return false;
    const statuses = [...element.querySelectorAll('[role=status], .updates-message, .nb-loading')];
    return !statuses.some(status => /^(?:Indlæser|Henter|Forbinder|Åbner)(?:\b|…|\.)/i.test(status.textContent.trim()));
  }, selector);
  const invalid = await locator.evaluate(element => {
    const text = element.innerText || '';
    const denied = /Du (?:skal være|er ikke) logget ind|Log ind som underviser|Denne side er kun for undervisere|Adgang nægtet|An unhandled error has occurred/i.test(text);
    const visible = node => { const r = node.getBoundingClientRect(); return r.width > 0 && r.height > 0; };
    return denied || [...element.querySelectorAll('[role=alert], .callout.bad, .nb-error, .notion-error')].some(node => visible(node) && node.textContent.trim())
      || [...element.querySelectorAll('img')].some(image => { const r = image.getBoundingClientRect(); return r.width > 0 && r.height > 0 && r.top < innerHeight && r.bottom > 0 && (!image.complete || !image.naturalWidth); });
  });
  if (invalid || await page.locator('#blazor-error-ui').isVisible().catch(() => false)) throw new CaptureFailure('capture_not_ready');
  return locator;
}

async function saveImage(page, locator, outputDir, id) {
  const region = await locator.boundingBox();
  // The profile's third panel is a raw diagnostic response, below the two user-facing panels.
  if (id === 'profile') {
    const info = await locator.locator('.panel').nth(1).boundingBox();
    if (!info) throw new CaptureFailure('capture_not_ready');
    region.height = Math.min(region.height, info.y + info.height - region.y);
  }
  let clip = captureBounds(region);
  let bytes;
  for (let attempt = 0; attempt < 5; attempt++) {
    bytes = await page.screenshot({ type: 'png', clip, animations: 'disabled', caret: 'hide', scale: 'css' });
    pngDimensions(bytes);
    if (bytes.length <= MAX_IMAGE_BYTES) break;
    if (clip.height <= 180) throw new CaptureFailure('image_too_large');
    clip = { ...clip, height: Math.max(180, Math.floor(clip.height * 0.7)) };
  }
  if (bytes.length > MAX_IMAGE_BYTES) throw new CaptureFailure('image_too_large');
  const destination = path.join(outputDir, `${id}.png`), temporary = path.join(outputDir, `.${id}-${randomUUID()}.png`);
  try { await writeFile(temporary, bytes, { flag: 'wx' }); await rename(temporary, destination); }
  finally { await unlink(temporary).catch(() => {}); }
  return destination;
}

async function boundedClose(value) {
  if (!value) return;
  await Promise.race([value.close().catch(() => {}), new Promise(resolve => setTimeout(resolve, 2_000))]);
}

async function captureOne(browser, scenario, base, outputDir, readStatic, timeout) {
  let context, timer;
  const work = async () => {
    context = await browser.newContext({ viewport: VIEWPORT, deviceScaleFactor: 1, locale: 'da-DK', timezoneId: 'Europe/Copenhagen',
      serviceWorkers: 'block', reducedMotion: 'reduce', colorScheme: 'light' });
    await context.route('**/*', createCaptureRouteHandler({ baseUrl: base, readStatic }));
    if (typeof context.routeWebSocket !== 'function') throw new CaptureFailure('capture_unavailable');
    await context.routeWebSocket('**/*', socket => socket.close());
    await context.addInitScript(initializeBrowser, browserSeed({ guest: scenario.id === 'public-home' }));
    const page = await context.newPage();
    page.setDefaultTimeout(Math.min(timeout, ROUTE_TIMEOUT));
    page.setDefaultNavigationTimeout(Math.min(timeout, ROUTE_TIMEOUT));
    if (!page.clock?.setFixedTime) throw new CaptureFailure('capture_unavailable');
    await page.clock.setFixedTime(new Date(FIXED_TIME));
    await page.goto(new URL(scenario.route, base).href, { waitUntil: 'domcontentloaded' });
    await page.locator(scenario.ready || scenario.selector).first().waitFor({ state: 'visible' });
    await prepare(page, scenario.id);
    const locator = await regionReady(page, scenario.selector);
    const destination = await saveImage(page, locator, outputDir, scenario.id);
    return { path: destination, alt: `Skærmbillede af ${scenario.title}. Optaget med isolerede testdata.`, ok: true };
  };
  try {
    return await Promise.race([work(), new Promise((_, reject) => { timer = setTimeout(() => reject(new CaptureFailure('capture_timeout')), timeout); })]);
  } catch (error) {
    return { ok: false, reason: knownReasons.has(error?.code) ? error.code : error?.name === 'TimeoutError' ? 'capture_timeout' : 'capture_not_ready' };
  } finally { clearTimeout(timer); await boundedClose(context); }
}

/** Captures only registered, real UI scenarios. A failed or unknown scenario never supplies a substitute image. */
export async function captureScenarios({ baseUrl, outputDir, scenarioIds } = {}) {
  const requested = scenarioIds === undefined ? scenarios.map(scenario => scenario.id) : Array.isArray(scenarioIds) ? [...new Set(scenarioIds)] : [];
  const result = Object.create(null);
  for (const id of requested) if (typeof id === 'string') result[id] = { ok: false, reason: byId.has(id) ? 'capture_unavailable' : 'unsupported_scenario' };
  const selected = requested.filter(id => typeof id === 'string' && byId.has(id)).map(id => byId.get(id));
  if (!selected.length) return result;
  const deadline = Date.now() + WHOLE_TIMEOUT;
  let browser;
  try {
    const base = captureBase(baseUrl);
    if (typeof outputDir !== 'string' || !outputDir.trim()) throw new CaptureFailure('capture_unavailable');
    await mkdir(outputDir, { recursive: true });
    const directory = await realpath(outputDir);
    const module = process.env.PLAYWRIGHT_MODULE
      ? await import(pathToFileURL(path.resolve(process.env.PLAYWRIGHT_MODULE)).href) : await import('playwright');
    browser = await module.chromium.launch({ headless: true, args: ['--no-proxy-server'],
      ...(process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {}) });
    const readStatic = createStaticReader(base);
    let next = 0;
    const run = async () => {
      while (next < selected.length) {
        const scenario = selected[next++];
        const remaining = deadline - Date.now() - 3_000;
        result[scenario.id] = remaining < 1_000 ? { ok: false, reason: 'capture_budget_exhausted' }
          : await captureOne(browser, scenario, base, directory, readStatic, Math.min(ROUTE_TIMEOUT, remaining));
      }
    };
    await Promise.all([run(), run()]);
  } catch { /* Global setup failures return the explicit failed results; they never create a false "after" image. */ }
  finally { await boundedClose(browser); }
  return result;
}
