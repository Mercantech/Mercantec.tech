import assert from 'node:assert/strict';
import test from 'node:test';
import { readFile } from 'node:fs/promises';

const moduleUrl = source => 'data:text/javascript;base64,' + Buffer.from(source).toString('base64');
const js = file => readFile(new URL('../web/wwwroot/js/' + file, import.meta.url), 'utf8');
let rendererSource = await js('arena-renderer.js');
for (const file of ['arena-avatar-art.js', 'arena-effect-art.js', 'arena-motion.js']) {
    const expression = new RegExp("'\\./" + file.replace('.', '\\.') + "\\?v=\\d+'");
    rendererSource = rendererSource.replace(expression, JSON.stringify(moduleUrl(await js(file))));
}
const { ArenaRenderer } = await import(moduleUrl(rendererSource));

const stages = ['rooftops', 'workshop', 'skyline'].map(id => ({
    id, accent: '#98bbff', platforms: [{ x: 220, y: 565, width: 840, height: 35 }]
}));

function fixture(t, mode = 'loaded') {
    const requests = [], text = [], draws = [], circles = [];
    const oldImage = globalThis.Image, oldMatchMedia = globalThis.matchMedia;
    globalThis.matchMedia = () => ({ matches: true });
    globalThis.Image = class {
        set src(url) {
            requests.push(url);
            if (mode === 'stalled') return;
            queueMicrotask(() => {
                if (mode === 'failed') { this.onerror?.(); return; }
                this.complete = true;
                this.naturalWidth = url.includes('platform-atlas') ? 1254 : 1024;
                this.naturalHeight = url.includes('platform-atlas') ? 1254 : 512;
                this.onload?.();
            });
        }
    };
    const context = new Proxy({
        fillText(value) { text.push({ value, font: this.font }); },
        drawImage(...args) { draws.push(args); },
        arc(...args) { circles.push(args); },
        createLinearGradient: () => ({ addColorStop() {} }),
        createRadialGradient: () => ({ addColorStop() {} })
    }, { get(target, key) { return key in target ? target[key] : () => {}; } });
    const renderer = new ArenaRenderer({ width: 1280, height: 720, getContext: () => context },
        { stages, selectedAvatarId: 'bip01' });
    t.after(() => { renderer.dispose(); globalThis.Image = oldImage; globalThis.matchMedia = oldMatchMedia; });
    return { renderer, requests, text, draws, circles };
}

test('the menu and a simultaneous stage selection request only their own art', async t => {
    const { renderer, requests } = fixture(t);
    assert.equal(await renderer.preload(), true);
    assert.equal(requests.length, 4);
    assert.ok(requests.some(url => url.endsWith('/bip-01/sprites/idle.png')));
    assert.ok(!requests.some(url => url.includes('/effects/')));
    await Promise.all([
        renderer.prepareMatch('workshop', ['raymond', 'raymond']),
        renderer.prepareMatch('workshop', [{ avatarId: 'raymond' }])
    ]);
    assert.equal(renderer.stageId, 'rooftops', 'preparation cannot replace the visible preview');
    assert.equal(requests.filter(url => url.includes('/raymond/sprites/')).length, 17);
    assert.equal(requests.filter(url => url.includes('/effects/raymond-')).length, 10);
    assert.ok(requests.every(url => !/skyline|santa|lupp/.test(url)));
    assert.equal(new Set(requests).size, requests.length, 'pending requests are shared');
});

test('a stalled art download never gates the menu or the match indefinitely', async t => {
    const { renderer } = fixture(t, 'stalled');
    assert.equal(await renderer.preload(), true);
    const start = performance.now();
    assert.equal(await renderer.prepareMatch('workshop', ['raymond']), true);
    assert.ok(performance.now() - start < 2400);
    assert.ok(renderer.pendingImages.size > 0, 'match readiness does not wait for the downloads to settle');
    renderer.dispose();
    assert.equal(renderer.pendingImages.size, 0);
    assert.equal(await renderer.prepareMatch('workshop', ['raymond']), false);
});

test('missing artwork keeps a visible battlefield and a readable mobile capture HUD', async t => {
    const { renderer, text, draws, circles } = fixture(t, 'failed');
    await renderer.prepareMatch('rooftops', ['starter', 'raymond']);
    renderer.setViewport(390, 219);
    const fighter = {
        id: 'own', avatarId: 'starter', name: 'Dig', x: 500, y: 565, vx: 0, vy: 0,
        color: '#64b5ff', stocks: 3, damage: 20, facing: 1, action: 'idle', grounded: true,
        connected: true, combat: { capturedBy: 'ray', captureRemaining: .4, escapePresses: 2 }
    };
    const ray = { ...fighter, id: 'ray', avatarId: 'raymond', name: 'Raymond', x: 555, combat: null };
    const state = {
        tick: 100, stageId: 'rooftops', phase: 'playing', isPracticeSandbox: true,
        fighters: [fighter, ray],
        projectiles: [{ id: 1, ownerId: 'ray', kind: 'pulse', x: 600, y: 520, vx: 240, vy: 0, radius: 12 }]
    };
    renderer.draw(1000, 1 / 60, state, null, 1000, 0, 'own', true);
    assert.equal(draws.length, 0);
    assert.ok(circles.length >= 7, 'fighter silhouettes, projectile glow and escape pips render');
    assert.ok(text.some(item => item.value === '∞'));
    assert.ok(text.some(item => item.value === 'Hop fri · 0.4s' && / 11px /.test(item.font)));
    assert.ok(text.some(item => item.value === '20%' && / 16px /.test(item.font)));
    assert.ok(renderer.assetsFailed.some(url => url.endsWith('platform-atlas.webp')));
});
