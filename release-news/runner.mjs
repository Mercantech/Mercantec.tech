import fs from 'node:fs/promises';
import path from 'node:path';
import { execFile } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { promisify } from 'node:util';
import { pathToFileURL } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import {
  COMMIT, REVISION, MAX_DIFF_BYTES, readJson, writeJson, truncateUtf8,
  isReviewableFile, isBinaryFile, redactSecrets, canonicalAuthors,
  normalizeEntries, publicationImage, validCursor, pruneCaches, classifyWorkerError,
} from './runner-core.mjs';

const exec = promisify(execFile);
const REPOSITORY = 'https://github.com/Mercantech/Mercantec.tech.git';

/** One durable cursor per worker volume. Dependencies are injected by tests. */
export class ReleaseNewsWorker {
  constructor({ dataDir, webBaseUrl, dependencies, scenarios, now = () => new Date().toISOString(), logger = console }) {
    this.dataDir = path.resolve(dataDir);
    this.webBaseUrl = webBaseUrl;
    this.deps = dependencies;
    this.scenarios = scenarios.map(({ id, title }) => ({ id, title }));
    this.scenarioIds = new Set(this.scenarios.map(item => item.id));
    if (!this.scenarios.length || this.scenarios.length > 80 || this.scenarioIds.size !== this.scenarios.length
        || this.scenarios.some(item => !/^[a-z0-9][a-z0-9-]{0,79}$/.test(item.id) || typeof item.title !== 'string' || !item.title.trim())) {
      throw new Error('Invalid screenshot scenario inventory.');
    }
    this.now = now;
    this.logger = logger;
    this.running = false;
  }

  file(...segments) { return path.join(this.dataDir, ...segments); }

  async heartbeat(phase, errorCode = null) {
    if (phase !== undefined) { this.phase = phase; this.errorCode = errorCode; }
    const value = { updatedAtUtc: this.now(), phase: this.phase ?? 'idle', errorCode: this.errorCode ?? null };
    this.heartbeatWrites = (this.heartbeatWrites ?? Promise.resolve()).catch(() => {}).then(() =>
      writeJson(this.file('heartbeat.json'), value, { mode: 0o644 }));
    await this.heartbeatWrites;
  }

  async deploymentIs(revision) {
    const deployment = await this.deps.getDeployment();
    return deployment?.revision === revision && REVISION.test(revision);
  }

  async state() {
    const local = await readJson(this.file('state.json'));
    if (local) {
      if (local.version !== 1 || !validCursor(local.cursor)) throw new Error('Invalid worker cursor.');
      return local;
    }
    const remote = await this.deps.getAutomationState();
    if (remote?.baselineRecorded === true && validCursor(remote)) {
      const restored = { version: 1, cursor: { ...remote, capturesDir: null }, history: {}, remoteSyncPending: false };
      await writeJson(this.file('state.json'), restored);
      return restored;
    }
    return null;
  }

  async captures(revision, { refresh = false } = {}) {
    await this.heartbeat('capturing');
    const directory = this.file('captures', revision);
    const manifestPath = path.join(directory, '.runner-manifest.json');
    const previous = await readJson(manifestPath);
    if (previous && !refresh) {
      if (previous.revision !== revision || !Number.isFinite(Date.parse(previous.capturedAtUtc)) || !previous.items) throw new Error('Invalid capture manifest.');
      const valid = {};
      for (const scenario of this.scenarios) {
        if (await publicationImage(previous.items[scenario.id], directory)) valid[scenario.id] = previous.items[scenario.id];
      }
      if (Object.keys(valid).length) return { directory, items: valid, capturedAtUtc: previous.capturedAtUtc };
    }
    await fs.mkdir(directory, { recursive: true, mode: 0o700 });
    const result = await this.deps.captureScenarios({ baseUrl: this.webBaseUrl, outputDir: directory });
    if (!result || typeof result !== 'object' || Array.isArray(result)) throw new Error('Capture did not complete.');
    const items = {};
    for (const scenario of this.scenarios) {
      const capture = result[scenario.id];
      if (await publicationImage(capture, directory)) items[scenario.id] = capture;
    }
    if (this.scenarios.length && !Object.keys(items).length) {
      const error = new Error('No valid screenshots were captured.');
      error.code = Object.values(result).map(item => item?.reason)
        .find(reason => ['browser_cache_not_writable', 'browser_launch_failed'].includes(reason));
      throw error;
    }
    const capturedAtUtc = this.now();
    await writeJson(manifestPath, { revision, capturedAtUtc, items });
    return { directory, items, capturedAtUtc };
  }

  async record(state) {
    state.remoteSyncPending = true;
    await writeJson(this.file('state.json'), state);
    await this.syncRemote(state);
  }

  async cleanup(state) {
    try { await (this.deps.pruneCaches ?? pruneCaches)(this.dataDir, state); }
    catch { this.logger.warn('Release news cache cleanup will retry.'); }
  }

  async syncRemote(state) {
    if (!state.remoteSyncPending || !await this.deploymentIs(state.cursor.revision)) return;
    const { commit, revision, capturedAtUtc, captureSuccessCount, scenarioCount } = state.cursor;
    await this.heartbeat('recording');
    try {
      await this.deps.recordAutomationState({ commit, revision, capturedAtUtc, status: 'ready', baselineRecorded: true,
        lastProcessedCommit: commit, lastProcessedRevision: revision, captureSuccessCount, scenarioCount });
      state.remoteSyncPending = false;
      await writeJson(this.file('state.json'), state);
      await this.cleanup(state);
    } catch (error) {
      // A newer deployment may win between the marker check and the state POST.
      if (error.status !== 409) throw error;
    }
  }

  async advance(state, candidate, captures) {
    const manifest = await readJson(path.join(captures.directory, '.runner-manifest.json'));
    const cursor = { ...candidate, capturesDir: captures.directory, capturedAtUtc: captures.capturedAtUtc,
      captureSuccessCount: Object.keys(captures.items ?? manifest?.items ?? {}).length, scenarioCount: this.scenarios.length };
    const next = { version: 1, cursor, previousRevision: state?.cursor.revision ?? null,
      history: { ...(state?.history ?? {}), [candidate.revision]: cursor }, remoteSyncPending: true };
    await this.record(next);
  }

  async tick() {
    if (this.running) return { status: 'busy' };
    this.running = true;
    let heartbeat;
    try {
      await this.heartbeat('polling');
      heartbeat = setInterval(() => { void this.heartbeat().catch(() => {}); }, 20_000);
      heartbeat.unref();
      const result = await this.process();
      await this.heartbeat('idle');
      return result;
    }
    catch (error) {
      await this.heartbeat(this.phase ?? 'polling', classifyWorkerError(error, this.phase));
      throw error;
    }
    finally {
      clearInterval(heartbeat);
      this.running = false;
    }
  }

  async process() {
    const deployment = await this.deps.getDeployment();
    if (!deployment || !REVISION.test(deployment.revision)) return { status: 'waiting-for-matching-deployment' };
    const revision = deployment.revision;
    await this.heartbeat('state');
    let state = await this.state();
    if (state?.remoteSyncPending) await this.syncRemote(state);
    if (state?.cursor.revision === revision && state.cursor.capturesDir) {
      await this.cleanup(state);
      return { status: 'unchanged' };
    }

    await this.heartbeat('resolving-source');
    const commit = await this.deps.resolveCommit(revision);
    if (!COMMIT.test(commit)) throw new Error('Deployed source commit was not found.');
    const candidate = { revision, commit };
    if (!await this.deploymentIs(revision)) return { status: 'stale' };

    if (!state || state.cursor.revision === revision) {
      const captures = await this.captures(revision, { refresh: true });
      if (!await this.deploymentIs(revision)) return { status: 'stale' };
      await this.advance(state, candidate, captures);
      return { status: 'baseline-recorded' };
    }

    // Re-deploying an already documented release refreshes the cursor without a duplicate post.
    if (validCursor(state.history?.[revision])) {
      const captures = await this.captures(revision, { refresh: true });
      if (!await this.deploymentIs(revision)) return { status: 'stale' };
      await this.advance(state, candidate, captures);
      return { status: 'already-processed' };
    }

    const beforeCommit = state.cursor.commit;
    const outboxPath = this.file('outbox', `${revision}.json`);
    let outbox = await readJson(outboxPath);
    if (outbox && (outbox.payload.commit !== commit || outbox.payload.beforeCommit !== beforeCommit)) outbox = null;
    if (!outbox) {
      const analysisPath = this.file('analysis', `${beforeCommit}-${commit}.json`);
      let analysis = await readJson(analysisPath);
      if (!analysis) {
        await this.heartbeat('analyzing');
        const changes = await this.deps.collectChanges(beforeCommit, commit);
        if (!await this.deploymentIs(revision)) return { status: 'stale' };
        const response = changes.changedFiles.length
          ? await this.deps.analyze({ beforeCommit, commit, diff: truncateUtf8(changes.diff, MAX_DIFF_BYTES),
            changedFiles: changes.changedFiles, scenarios: this.scenarios, authors: changes.authors })
          : { entries: [] };
        analysis = { entries: normalizeEntries(response, this.scenarioIds), authors: changes.authors };
        await writeJson(analysisPath, analysis);
      }
      const captures = await this.captures(revision);
      const before = state.cursor.capturesDir ? await readJson(path.join(state.cursor.capturesDir, '.runner-manifest.json')) : null;
      const entries = [];
      for (const entry of analysis.entries) {
        const published = { id: entry.id, title: entry.title, description: entry.description, authors: analysis.authors };
        if (entry.scenarioId) {
          const afterImage = await publicationImage(captures.items[entry.scenarioId], captures.directory);
          if (afterImage) {
            published.after = afterImage;
            const beforeImage = state.cursor.capturesDir
              ? await publicationImage(before?.items?.[entry.scenarioId], state.cursor.capturesDir) : undefined;
            if (beforeImage) published.before = beforeImage;
          }
        }
        entries.push(published);
      }
      outbox = { capturesDir: captures.directory, payload: { beforeCommit, commit, revision,
        capturedAtUtc: captures.capturedAtUtc, entries } };
      await writeJson(outboxPath, outbox);
    }
    if (!await this.deploymentIs(revision)) return { status: 'stale' };
    if (!outbox.deliveryAcknowledged) {
      await this.heartbeat('publishing');
      if (outbox.payload.entries.length) await this.deps.publish(outbox.payload);
      outbox.deliveryAcknowledged = true;
      await writeJson(outboxPath, outbox);
    }
    await this.advance(state, candidate, { directory: outbox.capturesDir, capturedAtUtc: outbox.payload.capturedAtUtc });
    return { status: outbox.payload.entries.length ? 'published' : 'no-user-facing-changes' };
  }
}

function endpointBase(value) {
  const url = new URL(value);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error('Invalid worker endpoint configuration.');
  return url.href.replace(/\/$/, '');
}

export function configuration(env = process.env) {
  return {
    dataDir: env.LUP_UPDATES_DATA_DIR || '/data',
    webBaseUrl: endpointBase(env.LUP_UPDATES_WEB_BASE_URL || 'https://lup.dk'),
    apiBaseUrl: endpointBase(env.LUP_UPDATES_API_BASE_URL || 'http://mercantec-tech-api:8080'),
    keyFile: env.LUP_UPDATES_KEY_FILE || '/run/lup-updates/service-key',
    pollIntervalMs: Math.max(5, Math.min(300, Number(env.LUP_UPDATES_POLL_SECONDS) || 30)) * 1000,
  };
}

export async function git(args, { timeout = 120_000, maxBuffer = 4 * 1024 * 1024 } = {}) {
  const result = await exec('git', ['-c', 'core.autocrlf=false', '-c', 'core.hooksPath=/dev/null', ...args], {
    timeout, maxBuffer, windowsHide: true, env: { ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_LFS_SKIP_SMUDGE: '1' },
  });
  return result.stdout;
}

export function repositoryDependencies({ dataDir, fingerprint, executeGit = git }) {
  const repo = path.join(path.resolve(dataDir), 'repo');
  const cacheFile = path.join(path.resolve(dataDir), 'commit-fingerprints-v2.json');
  async function prepare() {
    await fs.mkdir(dataDir, { recursive: true, mode: 0o700 });
    try { await fs.access(path.join(repo, '.git')); }
    catch {
      const temporary = path.join(path.resolve(dataDir), `repo-clone-${randomUUID()}`);
      try {
        await executeGit(['clone', '--filter=blob:none', '--no-checkout', '--single-branch', '--branch', 'main', REPOSITORY, temporary], { timeout: 300_000 });
        await fs.rename(temporary, repo);
      } finally {
        // Only this attempt's generated directory, directly beneath the private worker volume.
        if (path.dirname(temporary) === path.resolve(dataDir) && path.basename(temporary).startsWith('repo-clone-')) {
          await fs.rm(temporary, { recursive: true, force: true });
        }
      }
    }
  }
  return {
    async resolveCommit(revision) {
      if (!REVISION.test(revision)) throw new Error('Invalid deployed revision.');
      await prepare();
      const cache = await readJson(cacheFile) ?? {};
      const cached = Object.entries(cache).find(([commit, hash]) => COMMIT.test(commit) && hash === revision);
      if (cached) return cached[0];
      await executeGit(['-C', repo, 'fetch', '--quiet', 'origin', 'main'], { timeout: 120_000 });
      const history = (await executeGit(['-C', repo, 'rev-list', '--max-count=100', 'refs/remotes/origin/main']))
        .trim().split(/\s+/).filter(commit => COMMIT.test(commit));
      for (const commit of history) {
        if (!REVISION.test(cache[commit])) {
          await executeGit(['-C', repo, 'checkout', '--detach', '--force', commit], { timeout: 300_000 });
          cache[commit] = await fingerprint(repo);
          if (!REVISION.test(cache[commit])) throw new Error('Invalid source fingerprint.');
          await writeJson(cacheFile, cache);
        }
        if (cache[commit] === revision) return commit;
      }
      throw new Error('Deployed revision is not in the bounded source history.');
    },
    async collectChanges(beforeCommit, commit) {
      if (!COMMIT.test(beforeCommit) || !COMMIT.test(commit)) throw new Error('Invalid source range.');
      const files = (await executeGit(['-C', repo, 'diff', '--no-ext-diff', '--no-textconv', '--name-only', '-z', beforeCommit, commit, '--']))
        .split('\0').filter(isReviewableFile).slice(0, 300);
      const subjects = redactSecrets(await executeGit(['-C', repo, 'log', '--no-merges', '--max-count=50', '--format=%s', `${beforeCommit}..${commit}`, '--']));
      const identities = await executeGit(['-C', repo, 'log', '--no-merges', '--max-count=100', '--format=%aN%x00%aE', `${beforeCommit}..${commit}`, '--']);
      const authors = canonicalAuthors(identities.trim().split('\n').filter(Boolean).map(line => { const [name, email] = line.split('\0'); return { name, email }; }));
      let diff = `Commit subjects (untrusted repository text):\n${truncateUtf8(subjects, 12_000)}\n\n`;
      const priority = file => /^api\/Updates\/Notes\/.*\.json$/i.test(file) ? 0
        : /^web\/(Pages|Components|Layout)\//i.test(file) ? 1 : /^editor\/src\//i.test(file) ? 2
          : /^shared\//i.test(file) ? 3 : isBinaryFile(file) ? 6 : /^api\//i.test(file) ? 4 : 5;
      for (const file of [...files].sort((left, right) => priority(left) - priority(right) || left.localeCompare(right))) {
        if (Buffer.byteLength(diff) >= MAX_DIFF_BYTES - 100) break;
        let section;
        if (isBinaryFile(file)) section = `\nBinary asset changed; contents omitted: ${file}\n`;
        else {
          try {
            section = await executeGit(['-C', repo, 'diff', '--no-ext-diff', '--no-textconv', '--unified=3', beforeCommit, commit, '--', file], { maxBuffer: 2 * 1024 * 1024 });
            if (section.includes('\0')) section = `\nBinary asset changed; contents omitted: ${file}\n`;
          } catch { section = `\nChanged file is too large to include: ${file}\n`; }
        }
        diff += truncateUtf8(redactSecrets(section), Math.min(20_000, MAX_DIFF_BYTES - Buffer.byteLength(diff)));
      }
      return { changedFiles: files, diff: truncateUtf8(diff, MAX_DIFF_BYTES), authors };
    },
  };
}

export async function runtimeDependencies(config, { fingerprint, captureScenarios, fetchImpl = fetch }) {
  async function request(url, { keyed = false, body, timeoutMs = 20_000 } = {}) {
    const headers = { Accept: 'application/json' };
    if (keyed) {
      const key = (await fs.readFile(config.keyFile, 'utf8')).trim();
      if (key.length < 32 || key.length > 4096 || /[\r\n]/.test(key)) throw new Error('Worker service credential is unavailable.');
      headers['X-LUP-Updates-Key'] = key;
    }
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    const response = await fetchImpl(url, { method: body === undefined ? 'GET' : 'POST', headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }), redirect: 'error', signal: AbortSignal.timeout(timeoutMs) });
    if (!response.ok) { const error = new Error('Worker API request failed.'); error.status = response.status; throw error; }
    if (response.status === 204) return null;
    try { return await response.json(); }
    catch (cause) { const error = new Error('Worker response was not JSON.', { cause }); error.code = 'response_not_json'; throw error; }
  }
  const automation = `${config.apiBaseUrl}/api/platform-updates/automation`;
  async function metadata(url, source) {
    try { return await request(url); }
    catch (error) {
      error.metadataSource = source;
      if (!error.code) error.code = `${source}_metadata_unavailable`;
      throw error;
    }
  }
  return {
    ...repositoryDependencies({ dataDir: config.dataDir, fingerprint }), captureScenarios,
    async getDeployment() {
      const [web, api] = await Promise.all([
        metadata(`${config.webBaseUrl}/release.json`, 'web'),
        metadata(`${config.webBaseUrl}/api/platform-updates/deployment`, 'api'),
      ]);
      return REVISION.test(web?.revision) && web.revision === api?.revision ? { revision: web.revision } : null;
    },
    getAutomationState: () => request(`${automation}/state`, { keyed: true }),
    recordAutomationState: body => request(`${automation}/state`, { keyed: true, body }),
    analyze: body => request(`${automation}/analyze`, { keyed: true, body, timeoutMs: 180_000 }),
    publish: body => request(`${automation}/publish`, { keyed: true, body, timeoutMs: 180_000 }),
  };
}

export async function runForever(worker, { pollIntervalMs = 30_000, signal, logger = console } = {}) {
  let failures = 0;
  while (!signal?.aborted) {
    try {
      const result = await worker.tick(); failures = 0;
      if (['baseline-recorded', 'published', 'no-user-facing-changes'].includes(result.status)) logger.info(`Release news: ${result.status}.`);
    } catch { failures++; logger.warn('Release news worker will retry.'); }
    try { await delay(Math.min(300_000, pollIntervalMs * 2 ** Math.min(failures, 4)), undefined, { signal }); }
    catch (error) { if (signal?.aborted) break; throw error; }
  }
}

export async function main() {
  // Chrome's profile/crash cache must be writable even though the image itself is read-only.
  if (process.env.HOME === '/tmp/lup-release-news') {
    for (const directory of ['', '/cache', '/config', '/runtime'])
      await fs.mkdir('/tmp/lup-release-news' + directory, { recursive: true, mode: 0o700 });
  }
  const config = configuration();
  const { fingerprint } = await import('./fingerprint.mjs');
  const { captureScenarios, scenarios } = await import('./capture.mjs');
  const dependencies = await runtimeDependencies(config, { fingerprint, captureScenarios });
  const worker = new ReleaseNewsWorker({ ...config, dependencies, scenarios });
  const controller = new AbortController();
  process.once('SIGTERM', () => controller.abort());
  process.once('SIGINT', () => controller.abort());
  await runForever(worker, { pollIntervalMs: config.pollIntervalMs, signal: controller.signal });
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  main().catch(() => { console.error('Release news worker could not start.'); process.exitCode = 1; });
}
