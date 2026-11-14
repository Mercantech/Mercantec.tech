// Run against the isolated notebook fixture, never against a deployed API.
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const base = 'http://localhost:9098';
const health = await fetch('http://127.0.0.1:8088/_qa/health');
assert.equal((await health.json()).fixture, 'fictional-notebook-browser-qa');
const { chromium } = await import(pathToFileURL(process.env.PLAYWRIGHT_MODULE
  || resolve(tmpdir(), 'mercantec-graph-qa/node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch();
const errors = [];
const screenshotFolder = process.env.WORDLE_QA_SCREENSHOTS;
if (screenshotFolder) await mkdir(screenshotFolder, { recursive: true });

async function newClient(options = {}) {
  const context = await browser.newContext({ serviceWorkers: 'block', ...options });
  await context.route('**/*', route => {
    const url = new URL(route.request().url());
    return ['blob:', 'data:'].includes(url.protocol) || ['localhost', '127.0.0.1'].includes(url.hostname)
      ? route.continue() : route.abort('blockedbyclient');
  });
  const page = await context.newPage();
  page.setDefaultTimeout(20000);
  page.on('pageerror', error => errors.push(error.message));
  return page;
}

async function signIn(page, user) {
  await page.goto(base + '/_qa/login?user=' + user);
  await page.waitForURL(url => !url.pathname.startsWith('/_qa/'));
  await page.goto(base + '/games/wordle');
}

async function guess(page, word) {
  const input = page.getByRole('textbox', { name: 'Dit gæt på fem bogstaver' });
  await input.fill('');
  await input.pressSequentially(word);
  const response = page.waitForResponse(res => res.url().endsWith('/api/games/wordle/guess'));
  await input.press('Enter');
  return response;
}

try {
  const guest = await newClient();
  await guest.goto(base + '/games');
  await guest.getByRole('link', { name: 'Spil dagens Wordle' }).click();
  await guest.getByRole('heading', { name: 'Dagens Wordle', exact: true }).waitFor();
  const login = guest.getByRole('link', { name: 'Log ind og spil' });
  const loginUrl = new URL(await login.getAttribute('href'), base);
  assert.equal(loginUrl.searchParams.get('returnUrl'), '/games/wordle');
  assert.equal(await guest.getByRole('textbox').count(), 0);
  assert.equal(await guest.getByText('Dagens vært', { exact: true }).isVisible(), true);
  const metadata = await (await guest.request.get(base + '/api/games/wordle/daily')).json();
  assert.equal('answer' in metadata, false);
  assert.equal((await guest.request.get(base + '/api/games/wordle/round')).status(), 401);

  const desktop = await newClient({ viewport: { width: 1440, height: 1000 } });
  await signIn(desktop, 'pupil');
  await desktop.getByText('0 af 6 forsøg').waitFor();
  assert.equal((await guess(desktop, 'ZZZZZ')).status(), 422);
  await desktop.getByText('Ordet er ikke i ordlisten.', { exact: false }).waitFor();
  assert.equal(await desktop.getByText('0 af 6 forsøg').isVisible(), true);

  let firstResult;
  for (const word of ['tabel', 'scene', 'model', 'input', 'pixel', 'nøgle']) {
    const response = await guess(desktop, word);
    assert.equal(response.status(), 200, word);
    const round = await response.json();
    if (!firstResult) {
      firstResult = round;
      if (round.status === 0) {
        assert.equal(round.answer, null);
        assert.equal(round.explanation, null);
        await desktop.getByText('1 af 6 forsøg').waitFor();
        await desktop.reload();
        await desktop.getByText('1 af 6 forsøg').waitFor();
        if (screenshotFolder) await desktop.screenshot({ path: resolve(screenshotFolder, 'desktop.png'), fullPage: true });
      }
    }
    if (round.status !== 0) {
      assert.ok(round.answer && round.explanation);
      assert.equal(round.streak, 1);
      await desktop.getByText('Dagens ord var', { exact: false }).waitFor();
      await desktop.getByText(round.explanation, { exact: true }).waitFor();
      await desktop.reload();
      await desktop.getByText(round.explanation, { exact: true }).waitFor();
      break;
    }
  }

  for (const width of [390, 320]) {
    const mobile = await newClient({ viewport: { width, height: 844 }, isMobile: true, hasTouch: true });
    await signIn(mobile, 'peer');
    await mobile.getByText('0 af 6 forsøg').waitFor();
    assert.equal(await mobile.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    for (const key of ['N', 'Ø', 'G', 'L', 'E']) await mobile.getByRole('button', { name: key, exact: true }).tap();
    assert.equal(await mobile.getByRole('textbox', { name: 'Dit gæt på fem bogstaver' }).inputValue(), 'NØGLE');
    if (width === 390 && screenshotFolder)
      await mobile.screenshot({ path: resolve(screenshotFolder, 'mobile.png'), fullPage: true });
    if (width === 320) {
      await mobile.getByRole('button', { name: 'Gæt', exact: true }).tap();
      await mobile.getByText('1 af 6 forsøg').waitFor();
    }
  }

  assert.deepEqual(errors, []);
  console.log('Wordle: gæstevisning, fiktivt login, desktop, mobil 390/320, genåbning og kontoadskillelse bestod.');
} finally {
  await browser.close();
}
