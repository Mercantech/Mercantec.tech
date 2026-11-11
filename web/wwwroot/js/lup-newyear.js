(() => {
    "use strict";
    if (window.lupNewYear) return;

    const colors = ["#f6d889", "#ff927e", "#80dce8", "#c39af1"];
    const kinds = [
        { type: "bug", hp: 1, points: 50, width: 50, height: 44, color: "#ff927e", fixed: "Bug fixed" },
        { type: "syntax", hp: 1, points: 75, width: 108, height: 48, color: "#f6d889", fixed: "Syntax OK" },
        { type: "null", hp: 1, points: 100, width: 70, height: 42, color: "#c39af1", fixed: "Null-fejl rettet" },
        { type: "merge", hp: 2, points: 150, width: 102, height: 50, color: "#ffb37f", fixed: "Merged" },
        { type: "test", hp: 2, points: 200, width: 88, height: 48, color: "#ff92b7", fixed: "PASS" },
        { type: "build", hp: 4, points: 400, width: 120, height: 60, color: "#80dce8", fixed: "Build succeeded" }
    ];
    const boostKinds = [
        { type: "multishot", label: "3 skud", symbol: "×3", color: "#f6d889" },
        { type: "mega", label: "Store skud", symbol: "XL", color: "#c39af1" },
        { type: "rapid", label: "Hurtig ild", symbol: "++", color: "#ff927e" },
        { type: "comet", label: "Komet", symbol: "◆", color: "#80dce8" },
        { type: "speed", label: "Turbo", symbol: ">>", color: "#9ed7be" }
    ];
    const boostDuration = 12;
    const boosts = Object.fromEntries(boostKinds.map(kind => [kind.type, 0]));
    const limits = { falling: 28, rockets: 72, particles: 720, bursts: 12, labels: 20, pickups: 8 };
    const keys = new Set(), destroyed = new Set(), pointers = new Map();
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    const gameEffects = { particles: [], bursts: [] };
    let dialog, canvas, ctx, observer, previousFocus, playerImage, controls;
    let running = false, paused = false, frame = 0, previous = null;
    let width = 0, height = 0, playerX = 0, floor = 0, targetTop = 106;
    let elapsed = 0, score = 0, hits = 0, total = 0, level = 1, cooldown = 0, announced = 0;
    let spawnWait = 0, spawned = 0, escaped = 0, nextId = 0, dropped = 0, collected = 0;
    let targets = [], falling = [], rockets = [], labels = [], pickups = [];
    let pointerHeld = false, steering = 0;

    const query = selector => dialog?.querySelector(selector);
    const setText = (selector, value) => {
        const node = query(selector), text = String(value);
        if (node && node.textContent !== text) node.textContent = text;
    };
    const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
    const remaining = () => targets.filter(target => !target.hit && target.element.isConnected).length;
    const spawnInterval = () => Math.max(.19, 1.35 * Math.pow(.91, level - 1));
    const fallSpeed = () => 44 + (level - 1) * 9;
    function playerSize() {
        const w = Math.min(width < 700 ? 112 : 128, Math.max(60, width * .3));
        const ratio = playerImage?.naturalWidth > 0 ? playerImage.naturalHeight / playerImage.naturalWidth : .98;
        return { width: w, height: clamp(w * ratio, 62, 132) };
    }
    function playerTop() {
        const footY = playerImage?.complete && playerImage.naturalWidth > 0 ? Number(playerImage.dataset?.footY ?? 1) : 1;
        return floor - playerSize().height * clamp(footY, .1, 1);
    }
    function muzzle() {
        const size = playerSize();
        const x = Number(playerImage?.dataset?.muzzleX ?? .78);
        const y = Number(playerImage?.dataset?.muzzleY ?? .16);
        return { x: playerX + size.width * (clamp(x, 0, 1) - .5), y: playerTop() + size.height * clamp(y, 0, 1) };
    }
    function movePlayer(value) {
        const margin = playerSize().width / 2 + 6;
        playerX = clamp(value, margin, width - margin);
    }
    function aimPlayer(value) { movePlayer(value - (muzzle().x - playerX)); }
    const fireCooldown = () => boosts.rapid > 0 ? .075 : .18;
    const projectileSpeed = () => 590 * (boosts.comet > 0 ? 1.25 : 1) * (boosts.speed > 0 ? 1.35 : 1);
    const playerSpeed = () => 410 * (boosts.speed > 0 ? 1.6 : 1);
    const state = () => ({
        running, paused, score, hits, level, remaining: remaining(), total,
        fallingCount: falling.length, projectileCount: rockets.length, particleCount: gameEffects.particles.length,
        burstCount: gameEffects.bursts.length, labelCount: labels.length, pickupCount: pickups.length,
        spawned, escaped, dropped, collected, playerX, fireOriginX: muzzle().x,
        spawnInterval: spawnInterval(), fallSpeed: fallSpeed(), playerSpeed: playerSpeed(),
        projectileSpeed: projectileSpeed(), fireCooldown: fireCooldown(), boosts: { ...boosts },
        falling: falling.map(({ id, type, x, y, width, height, hp, points }) => ({ id, type, x, y, width, height, hp, points })),
        pickups: pickups.map(({ id, type, x, y, width, height }) => ({ id, type, x, y, width, height })),
        rockets: rockets.map(({ x, y, vx, vy, radius, damage, pierceLeft, kind }) => ({ x, y, vx, vy, radius, damage, pierceLeft, kind })),
        bursts: gameEffects.bursts.map(({ x, y, age, life, maxRadius, static: still }) => ({ x, y, age, life, radius: still ? maxRadius * .55 : maxRadius * (1 - Math.pow(1 - age / life, 3)), static: still })),
        labels: labels.map(({ text, x, y, life }) => ({ text, x, y, life })),
        particles: gameEffects.particles.map(({ x, y, previousX, previousY, life }) => ({ x, y, previousX, previousY, life }))
    });

    function announce(message) { setText("[data-newyear-status]", message); }
    function updateBoostHud() {
        let active = false;
        for (const kind of boostKinds) {
            const node = query('[data-newyear-boost="' + kind.type + '"]');
            if (node) {
                node.hidden = boosts[kind.type] <= 0;
                node.style?.setProperty?.("--boost-left", clamp(boosts[kind.type] / boostDuration * 100, 0, 100) + "%");
            }
            active ||= boosts[kind.type] > 0;
        }
        const empty = query("[data-newyear-boost-empty]");
        if (empty) empty.hidden = active;
    }
    function updateHud() {
        setText("[data-newyear-score]", score);
        setText("[data-newyear-level]", level);
        setText("[data-newyear-hits]", hits);
        updateBoostHud();
    }
    function restore() {
        for (const element of destroyed) element.classList.remove("newyear-destroyed");
        destroyed.clear();
    }
    function collectTargets() {
        const selector = ".lup h1,.lup h2,.lup h3,.lup p,.lup li,.lup a,.lup button,.lup .pill,.lup .context,.lup .tile,.lup .v3-next,.lup .panel";
        const candidates = Array.from(document.querySelectorAll(selector)).filter(element => {
            if (element.closest("form,dialog,[contenteditable],.newyear-launcher,[data-newyear-ignore]")) return false;
            if (element.classList.contains("newyear-destroyed") || element.querySelector("iframe,input,textarea,select,[contenteditable]")) return false;
            const style = getComputedStyle(element), rect = element.getBoundingClientRect();
            return style.visibility === "visible" && style.display !== "none" && Number(style.opacity) !== 0
                && rect.width >= 18 && rect.height >= 12 && rect.width <= Math.max(760, width * .85)
                && rect.height <= 180 && rect.left >= 0 && rect.right <= width
                && rect.top >= targetTop && rect.bottom <= playerTop() - 12;
        });
        return candidates.filter(element => !candidates.some(other => other !== element && element.contains(other)))
            .slice(0, 70).map((element, index) => {
                const rect = element.getBoundingClientRect();
                return { element, x: rect.left, y: rect.top, width: rect.width, height: rect.height, color: colors[index % colors.length], hit: false, hp: 1, points: 100 };
            });
    }
    function resizeCanvas() {
        const bounds = canvas.getBoundingClientRect();
        width = bounds.width; height = bounds.height;
        const ratio = Math.min(devicePixelRatio || 1, 2);
        canvas.width = Math.round(width * ratio); canvas.height = Math.round(height * ratio);
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
        targetTop = (query("[data-newyear-hud]")?.getBoundingClientRect().bottom || 94) + 12;
        floor = (query("[data-newyear-controls]")?.getBoundingClientRect().top || height - 80) - 8;
        playerX = width / 2;
    }
    function fire() {
        if (!running || paused || cooldown > 0 || rockets.length >= limits.rockets) return;
        const origin = muzzle(), speed = projectileSpeed();
        const angles = boosts.multishot > 0 ? [-.16, 0, .16] : [0];
        for (const angle of angles) {
            if (rockets.length >= limits.rockets) break;
            rockets.push({ x: origin.x, y: origin.y, previousX: origin.x, previousY: origin.y,
                vx: Math.sin(angle) * speed, vy: -Math.cos(angle) * speed,
                radius: boosts.mega > 0 ? 8 : 3, damage: boosts.mega > 0 ? 2 : 1,
                pierceLeft: boosts.comet > 0 ? 3 : 1, kind: boosts.comet > 0 ? "comet" : "firework", struck: new Set(), dead: false });
        }
        cooldown = fireCooldown();
    }
    function addLabel(text, x, y, color = "#9ee8bc") {
        if (labels.length >= limits.labels) labels.shift();
        labels.push({ text, x: clamp(x, 76, Math.max(76, width - 76)), y, color, life: motion.matches ? .75 : 1.25, duration: motion.matches ? .75 : 1.25 });
    }
    function burst({ particles, bursts, animate = !motion.matches }, x, y, color, destroyedTarget = true, large = false) {
        if (bursts.length >= limits.bursts) bursts.shift();
        bursts.push({ x, y, color, age: 0, life: !animate ? .45 : destroyedTarget ? (large ? 1.8 : 1.3) : .5,
            maxRadius: destroyedTarget ? (large ? 105 : 76) : 25, static: !animate, rings: destroyedTarget ? 2 : 1 });
        if (!animate) return;
        const count = destroyedTarget ? (large ? 88 : 56) : 10;
        if (particles.length + count > limits.particles) particles.splice(0, particles.length + count - limits.particles);
        for (let i = 0; i < count; i++) {
            const ring = i % 2, angle = Math.PI * 2 * Math.floor(i / 2) / (count / 2) + ring * .15;
            const speed = (ring ? 105 : 65) * (large ? 1.35 : 1) * (.85 + Math.random() * .3);
            const life = destroyedTarget ? 1.05 + Math.random() * .55 : .4;
            particles.push({ x, y, previousX: x, previousY: y, vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed, life, duration: life, seed: Math.random() * 6.28,
                color: i % 4 === 0 ? color : colors[i % colors.length] });
        }
    }
    function dropPickup(x, y) {
        if (pickups.length >= limits.pickups) return;
        const kind = boostKinds[dropped % boostKinds.length], w = Math.min(94, width - 36);
        pickups.push({ ...kind, id: ++nextId, pickup: true, hit: false,
            x: clamp(x - w / 2, 18, width - w - 18), y: clamp(y - 14, targetTop + 6, floor - 48),
            previousY: clamp(y - 14, targetTop + 6, floor - 48), width: w, height: 29 });
        dropped++;
    }
    function activateBoost(item) {
        if (item.hit) return;
        item.hit = true; boosts[item.type] = boostDuration; collected++;
        addLabel(item.label + "!", item.x + item.width / 2, item.y - 6, item.color);
        burst(gameEffects, item.x + item.width / 2, item.y + item.height / 2, item.color, false);
        announce("Boost: " + item.label + ". Aktivt i 12 sekunders spilletid.");
        updateBoostHud();
    }
    function hit(target, x, y, damage = 1) {
        target.hp = Math.max(0, target.hp - damage);
        burst(gameEffects, x, y, target.color, target.hp === 0, target.type === "build");
        if (target.hp > 0) return;
        target.hit = true;
        if (target.element) { target.element.classList.add("newyear-destroyed"); destroyed.add(target.element); }
        score += target.points; hits++;
        addLabel(target.fixed || "+" + target.points, target.x + target.width / 2, target.y, "#9ee8bc");
        if (hits === 1 || (hits - 1) % 4 === 0) dropPickup(target.x + target.width / 2, target.y + target.height / 2);
        updateHud();
        if (score - announced >= 500) { announced = score; announce(score + " point. " + hits + " fejl rettet. Skyd eller grib de lysende boosts!"); }
    }
    function spawn(lane) {
        if (falling.length >= Math.min(limits.falling, 7 + level)) return;
        const kind = kinds[spawned % kinds.length];
        const available = Math.max(0, width - kind.width - 36);
        const fraction = .08 + Math.random() * .84;
        const spawnX = typeof lane === "number" ? clamp(width * lane - kind.width / 2, 18, width - kind.width - 18) : 18 + available * fraction;
        const y = targetTop + 12;
        falling.push({ ...kind, id: ++nextId, maxHp: kind.hp, x: spawnX, y, previousY: y, hit: false, speedFactor: .85 + Math.random() * .3 });
        spawned++;
    }
    function roundRect(x, y, w, h, radius) {
        ctx.beginPath(); ctx.moveTo(x + radius, y); ctx.lineTo(x + w - radius, y);
        ctx.quadraticCurveTo(x + w, y, x + w, y + radius); ctx.lineTo(x + w, y + h - radius);
        ctx.quadraticCurveTo(x + w, y + h, x + w - radius, y + h); ctx.lineTo(x + radius, y + h);
        ctx.quadraticCurveTo(x, y + h, x, y + h - radius); ctx.lineTo(x, y + radius);
        ctx.quadraticCurveTo(x, y, x + radius, y); ctx.closePath();
    }
    function drawFalling(item) {
        const { x, y, width: w, height: h, type, color } = item;
        ctx.save(); ctx.lineWidth = 1.5; ctx.strokeStyle = color;
        if (type === "bug") {
            ctx.strokeStyle = "#ffb1a4"; ctx.lineWidth = 2;
            for (const side of [-1, 1]) for (const at of [.28, .5, .72]) {
                ctx.beginPath(); ctx.moveTo(x + w / 2 + side * 8, y + h * at); ctx.lineTo(x + w / 2 + side * 18, y + h * at - 5); ctx.stroke();
            }
            ctx.fillStyle = "#9b3f59"; ctx.beginPath(); ctx.ellipse(x + w / 2, y + h / 2, 12, 17, 0, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
            ctx.strokeStyle = "#f6d889"; ctx.beginPath(); ctx.moveTo(x + w / 2, y + 7); ctx.lineTo(x + w / 2, y + h - 7); ctx.stroke();
            ctx.fillStyle = "#fff0d4"; ctx.beginPath(); ctx.arc(x + w / 2 - 5, y + 12, 2.5, 0, Math.PI * 2); ctx.arc(x + w / 2 + 5, y + 12, 2.5, 0, Math.PI * 2); ctx.fill();
            ctx.font = "700 10px 'DM Sans',sans-serif"; ctx.textAlign = "center"; ctx.fillStyle = "#ffcdc3"; ctx.fillText("bug", x + w / 2, y + h + 10);
        } else {
            ctx.fillStyle = type === "build" ? "#292a4c" : "#33233e";
            roundRect(x, y, w, h, type === "null" ? 12 : 6); ctx.fill(); ctx.stroke();
            ctx.fillStyle = color; ctx.fillRect(x + 1, y + 8, w - 2, 2);
            for (let i = 0; i < 3; i++) { ctx.beginPath(); ctx.arc(x + 7 + i * 6, y + 5, 1.2, 0, Math.PI * 2); ctx.fill(); }
            ctx.textAlign = "center"; ctx.fillStyle = "#fff1dd";
            if (type === "syntax") {
                ctx.font = "700 12px Consolas,monospace"; ctx.fillText("SyntaxError", x + w / 2, y + 27);
                ctx.font = "10px Consolas,monospace"; ctx.fillStyle = color; ctx.fillText("missing ;", x + w / 2, y + 41);
            } else if (type === "null") {
                ctx.font = "700 20px Consolas,monospace"; ctx.fillText("null", x + w / 2, y + 31);
            } else if (type === "merge") {
                ctx.font = "700 12px Consolas,monospace"; ctx.fillText("<<<<<<<", x + w / 2, y + 27);
                ctx.font = "10px 'DM Sans',sans-serif"; ctx.fillStyle = color; ctx.fillText("Mergekonflikt", x + w / 2, y + 42);
            } else if (type === "test") {
                ctx.font = "700 19px Consolas,monospace"; ctx.fillStyle = "#ffa1b7"; ctx.fillText("FAIL", x + w / 2, y + 31);
                ctx.font = "10px Consolas,monospace"; ctx.fillStyle = "#f5cad5"; ctx.fillText("test()", x + w / 2, y + 43);
            } else {
                ctx.font = "700 12px Consolas,monospace"; ctx.fillText("Build failed", x + w / 2, y + 28);
                ctx.font = "10px 'DM Sans',sans-serif"; ctx.fillStyle = color; ctx.fillText("Ret alle fejl", x + w / 2, y + 44); ctx.fillRect(x + 12, y + h - 8, w - 24, 2);
            }
        }
        if (item.maxHp > 1) for (let i = 0; i < item.maxHp; i++) {
            ctx.fillStyle = i < item.hp ? "#fff0cb" : "#64718a"; ctx.fillRect(x + w / 2 - item.maxHp * 4 + i * 8, y - 7, 5, 3);
        }
        ctx.restore();
    }
    function drawPickup(item) {
        ctx.save(); ctx.lineWidth = 2; ctx.fillStyle = "#173d39"; ctx.strokeStyle = item.color;
        roundRect(item.x, item.y, item.width, item.height, 12); ctx.fill(); ctx.stroke();
        ctx.fillStyle = item.color; ctx.textAlign = "center"; ctx.font = "700 11px 'DM Sans',sans-serif";
        ctx.fillText("++ " + item.label, item.x + item.width / 2, item.y + 19);
        ctx.fillStyle = "#fff7dd"; ctx.fillRect(item.x + 8, item.y - 4, 3, 3); ctx.fillRect(item.x + item.width - 10, item.y + item.height + 2, 3, 3); ctx.restore();
    }
    function drawRocket(ctx, rocket, animate = !motion.matches) {
        const scale = rocket.radius > 3 ? 1.65 : 1;
        ctx.save(); ctx.translate(rocket.x, rocket.y); ctx.rotate(Math.atan2(rocket.vx, -rocket.vy));
        if (rocket.kind === "comet") {
            if (animate) for (let i = 3; i > 0; i--) {
                ctx.globalAlpha = .6 / i; ctx.fillStyle = i % 2 ? "#c39af1" : "#f6d889";
                ctx.beginPath(); ctx.moveTo(-4 * scale / i, 5); ctx.lineTo(0, 12 + i * 14); ctx.lineTo(4 * scale / i, 5); ctx.closePath(); ctx.fill();
            }
            ctx.globalAlpha = 1; ctx.fillStyle = "#ead3ff"; ctx.beginPath(); ctx.arc(0, -2, rocket.radius + 3, 0, Math.PI * 2); ctx.fill();
            ctx.strokeStyle = "#f6d889"; ctx.lineWidth = 1.5; ctx.stroke();
        } else {
            ctx.fillStyle = "#ff927e"; ctx.fillRect(-3 * scale, -9 * scale, 6 * scale, 15 * scale);
            ctx.fillStyle = "#f6d889"; ctx.fillRect(-3 * scale, -3 * scale, 6 * scale, 3 * scale);
            ctx.beginPath(); ctx.moveTo(0, -15 * scale); ctx.lineTo(5 * scale, -8 * scale); ctx.lineTo(-5 * scale, -8 * scale); ctx.closePath(); ctx.fill(); ctx.fillRect(-scale, 6 * scale, 2 * scale, 7 * scale);
            if (animate) { ctx.fillStyle = "#f6d889aa"; ctx.fillRect(-1.5 * scale, 14 * scale, 3 * scale, 9 * scale); ctx.fillStyle = "#ff927e88"; ctx.fillRect(-scale, 25 * scale, 2 * scale, 5 * scale); }
        }
        ctx.restore();
    }
    function drawBurst(ctx, effect) {
        const progress = clamp(effect.age / effect.life, 0, 1);
        const radius = effect.static ? effect.maxRadius * .55 : effect.maxRadius * (1 - Math.pow(1 - progress, 3));
        ctx.save(); ctx.globalAlpha = (1 - progress) * .85; ctx.lineWidth = effect.static ? 2 : 1.5;
        for (let ring = 0; ring < effect.rings; ring++) {
            const r = radius * (ring ? .6 : 1);
            for (let i = 0; i < 18; i++) {
                const angle = i * Math.PI / 9 + ring * .17;
                ctx.strokeStyle = i % 3 === 0 ? effect.color : colors[(i + ring) % colors.length];
                ctx.beginPath(); ctx.moveTo(effect.x + Math.cos(angle) * r * .77, effect.y + Math.sin(angle) * r * .77);
                ctx.lineTo(effect.x + Math.cos(angle) * r, effect.y + Math.sin(angle) * r); ctx.stroke();
            }
        }
        ctx.restore();
    }
    function drawEffects(ctx, { bursts, particles }) {
        for (const effect of bursts) drawBurst(ctx, effect);
        for (const particle of particles) {
            const age = particle.duration - particle.life;
            ctx.globalAlpha = clamp(particle.life / particle.duration, 0, 1) * (.4 + .6 * Math.pow(Math.sin(age * 28 + particle.seed), 2));
            ctx.strokeStyle = particle.color; ctx.lineWidth = 1.3; ctx.beginPath(); ctx.moveTo(particle.x - particle.vx * .055, particle.y - particle.vy * .055); ctx.lineTo(particle.x, particle.y); ctx.stroke();
            ctx.fillStyle = particle.color; ctx.fillRect(particle.x - 1, particle.y - 1, 2.4, 2.4);
        }
        ctx.globalAlpha = 1;
    }
    function advanceEffects(effects, step) {
        for (const particle of effects.particles) {
            particle.previousX = particle.x; particle.previousY = particle.y; particle.x += particle.vx * step; particle.y += particle.vy * step;
            particle.vx *= Math.exp(-.8 * step); particle.vy += 75 * step; particle.life -= step;
        }
        effects.particles = effects.particles.filter(particle => particle.life > 0);
        for (const effect of effects.bursts) effect.age += step;
        effects.bursts = effects.bursts.filter(effect => effect.age < effect.life);
    }
    function drawPlayer() {
        const size = playerSize(), x = playerX - size.width / 2, y = playerTop();
        let imageDrawn = false;
        if (playerImage?.complete && playerImage.naturalWidth > 0 && playerImage.naturalHeight > 0) {
            try { ctx.drawImage(playerImage, x, y, size.width, size.height); imageDrawn = true; } catch { /* A failed image still leaves a playable mascot. */ }
        }
        if (!imageDrawn) {
            // A friendly purple mascot is also available while its illustration loads.
            ctx.fillStyle = "#9253b6"; roundRect(x + size.width * .2, y + size.height * .15, size.width * .6, size.height * .6, 12); ctx.fill();
            ctx.fillRect(x + size.width * .25, y + size.height * .71, size.width * .16, size.height * .24);
            ctx.fillRect(x + size.width * .59, y + size.height * .71, size.width * .16, size.height * .24);
            roundRect(x + size.width * .2, y + size.height * .91, size.width * .25, size.height * .09, 4); ctx.fill();
            roundRect(x + size.width * .55, y + size.height * .91, size.width * .25, size.height * .09, 4); ctx.fill();
            ctx.fillRect(x + size.width * .04, y + size.height * .39, size.width * .23, size.height * .13);
            ctx.fillRect(x + size.width * .74, y + size.height * .28, size.width * .2, size.height * .13);
            ctx.fillStyle = "#fff0d4";
            for (const eye of [.39, .62]) { ctx.beginPath(); ctx.ellipse(x + size.width * eye, y + size.height * .36, size.width * .105, size.height * .12, 0, 0, Math.PI * 2); ctx.fill(); }
            ctx.fillStyle = "#231c2d";
            for (const eye of [.41, .64]) { ctx.beginPath(); ctx.arc(x + size.width * eye, y + size.height * .36, size.width * .045, 0, Math.PI * 2); ctx.fill(); }
            ctx.strokeStyle = "#40224d"; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(x + size.width * .43, y + size.height * .56); ctx.quadraticCurveTo(x + size.width * .52, y + size.height * .66, x + size.width * .62, y + size.height * .56); ctx.stroke();
            const origin = muzzle(); ctx.fillStyle = "#f6d889"; ctx.fillRect(origin.x - 4, origin.y + 1, 8, size.height * .28);
            ctx.fillStyle = "#ff927e"; ctx.fillRect(origin.x - 4, origin.y + 8, 8, 5);
        }
        ctx.strokeStyle = "#80dce84d"; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(18, floor + 2); ctx.lineTo(width - 18, floor + 2); ctx.stroke();
    }
    function draw() {
        ctx.clearRect(0, 0, width, height); ctx.lineWidth = 1;
        for (const target of targets) {
            if (target.hit || !target.element.isConnected) continue;
            ctx.strokeStyle = target.color + "88"; ctx.strokeRect(target.x - 3, target.y - 3, target.width + 6, target.height + 6);
            ctx.fillStyle = target.color; ctx.fillRect(target.x - 4, target.y - 4, 9, 2); ctx.fillRect(target.x + target.width - 5, target.y + target.height + 2, 9, 2);
        }
        for (const item of falling) if (!item.hit) drawFalling(item);
        for (const pickup of pickups) if (!pickup.hit) drawPickup(pickup);
        for (const rocket of rockets) drawRocket(ctx, rocket);
        drawEffects(ctx, gameEffects);
        for (const label of labels) {
            const w = Math.min(180, Math.max(72, label.text.length * 7 + 18));
            ctx.globalAlpha = Math.min(1, label.life * 3); ctx.fillStyle = "#102c32e8"; roundRect(label.x - w / 2, label.y - 21, w, 26, 6); ctx.fill();
            ctx.fillStyle = label.color; ctx.font = "700 12px 'DM Sans',sans-serif"; ctx.textAlign = "center"; ctx.fillText(label.text, label.x, label.y - 3);
        }
        ctx.globalAlpha = 1; drawPlayer();
    }
    function impactAt(rocket, target) {
        if (target.hit || target.element && !target.element.isConnected || rocket.struck.has(target)) return null;
        const from = [rocket.previousX - target.x, rocket.previousY - (target.previousY ?? target.y)];
        const to = [rocket.x - target.x, rocket.y - target.y];
        const lo = [-rocket.radius, -6], hi = [target.width + rocket.radius, target.height + (rocket.radius > 3 ? 22 : 15)];
        let near = 0, far = 1;
        for (let axis = 0; axis < 2; axis++) {
            const delta = to[axis] - from[axis];
            if (Math.abs(delta) < .00001) { if (from[axis] < lo[axis] || from[axis] > hi[axis]) return null; continue; }
            const a = (lo[axis] - from[axis]) / delta, b = (hi[axis] - from[axis]) / delta;
            near = Math.max(near, Math.min(a, b)); far = Math.min(far, Math.max(a, b)); if (near > far) return null;
        }
        return near >= 0 && near <= 1 ? near : null;
    }
    function tick(now) {
        frame = 0;
        if (!running || paused) return;
        if (!canvas.isConnected || !dialog.open || document.documentElement.dataset.theme !== "newyear") { stop(); return; }
        const delta = previous === null ? 0 : Math.max(0, (now - previous) / 1000), step = Math.min(delta, .05);
        previous = now; elapsed += delta; cooldown = Math.max(0, cooldown - step);
        const expired = [];
        for (const kind of boostKinds) { const before = boosts[kind.type]; boosts[kind.type] = Math.max(0, before - delta); if (before > 0 && boosts[kind.type] === 0) expired.push(kind.label); }
        if (expired.length) announce("Boost udløbet: " + expired.join(", ") + ". Skyd eller fang et nyt boost.");
        const nextLevel = Math.min(20, 1 + Math.floor(elapsed / 16) + Math.floor(hits / 12));
        if (nextLevel !== level) { level = nextLevel; announce("Niveau " + level + ". Flere kodefejl og mere fart!"); }
        const horizontal = steering || (keys.has("ArrowRight") || keys.has("d") ? 1 : 0) - (keys.has("ArrowLeft") || keys.has("a") ? 1 : 0);
        movePlayer(playerX + horizontal * playerSpeed() * step);
        if (pointerHeld || keys.has(" ") || keys.has("Enter")) fire();
        spawnWait -= delta;
        let spawnBudget = 3;
        while (spawnWait <= 0 && spawnBudget-- > 0) { spawn(); spawnWait += spawnInterval(); }
        if (spawnWait <= 0) spawnWait = spawnInterval();
        for (const item of falling) { item.previousY = item.y; item.y += fallSpeed() * item.speedFactor * step; }
        for (const pickup of pickups) { pickup.previousY = pickup.y; pickup.y += 70 * step; }
        for (const rocket of rockets) {
            rocket.previousX = rocket.x; rocket.previousY = rocket.y; rocket.x += rocket.vx * step; rocket.y += rocket.vy * step;
            const impacts = [];
            for (const candidate of [...pickups, ...falling, ...targets]) { const at = impactAt(rocket, candidate); if (at !== null) impacts.push({ candidate, at }); }
            impacts.sort((a, b) => a.at - b.at);
            for (const { candidate, at } of impacts) {
                if (candidate.hit || rocket.dead) continue; rocket.struck.add(candidate);
                if (candidate.pickup) { activateBoost(candidate); rocket.dead = true; continue; }
                hit(candidate, rocket.previousX + (rocket.x - rocket.previousX) * at, rocket.previousY + (rocket.y - rocket.previousY) * at, rocket.damage);
                if (--rocket.pierceLeft <= 0) rocket.dead = true;
            }
        }
        const size = playerSize(), playerLeft = playerX - size.width * .42, playerRight = playerX + size.width * .42, playerHead = playerTop() + size.height * .15;
        for (const pickup of pickups) if (!pickup.hit && pickup.x < playerRight && pickup.x + pickup.width > playerLeft && pickup.y + pickup.height >= playerHead && pickup.y <= floor) activateBoost(pickup);
        rockets = rockets.filter(rocket => !rocket.dead && rocket.y > -60 && rocket.x > -50 && rocket.x < width + 50);
        pickups = pickups.filter(item => !item.hit && item.y <= floor + 32);
        falling = falling.filter(item => { if (item.hit) return false; if (item.y > floor) { escaped++; return false; } return true; });
        advanceEffects(gameEffects, step);
        for (const label of labels) { label.life -= step; if (!motion.matches) label.y -= 18 * step; } labels = labels.filter(label => label.life > 0);
        updateHud(); draw(); frame = requestAnimationFrame(tick);
    }
    function listen(target, name, callback, options = {}) { target.addEventListener(name, callback, { ...options, signal: controls.signal }); }
    function updatePointers() {
        const actions = [...pointers.values()]; pointerHeld = actions.includes("fire");
        steering = Math.sign(actions.filter(action => action === "right").length - actions.filter(action => action === "left").length);
    }
    function suspend() {
        keys.clear(); pointers.clear(); pointerHeld = false; steering = 0;
        paused = true; previous = null; cancelAnimationFrame(frame); frame = 0;
    }
    function resume() {
        if (!running || document.hidden || !paused) return;
        paused = false; previous = null; frame = requestAnimationFrame(tick);
    }
    function actionOf(event) { return event.target.closest?.("[data-newyear-action]")?.dataset.newyearAction; }
    function start() {
        if (document.documentElement.dataset.theme !== "newyear") return false;
        if (running) return true;
        dialog = document.getElementById("lup-newyear-game"); canvas = document.getElementById("lup-newyear-canvas");
        playerImage = document.getElementById("lup-newyear-player");
        if (!dialog || !canvas || typeof dialog.showModal !== "function") return false;
        ctx = canvas.getContext("2d"); if (!ctx) return false;
        previousFocus = document.activeElement; restore(); keys.clear(); pointers.clear();
        targets = []; falling = []; rockets = []; labels = []; pickups = [];
        gameEffects.particles.length = gameEffects.bursts.length = 0;
        for (const kind of boostKinds) boosts[kind.type] = 0;
        score = 0; hits = 0; level = 1; total = 0; elapsed = 0; cooldown = 0; announced = 0; previous = null;
        spawnWait = .65; spawned = 0; escaped = 0; nextId = 0; dropped = 0; collected = 0; paused = false; steering = 0; pointerHeld = false;
        stopPageFireworks();
        try { dialog.showModal(); } catch { return false; }
        resizeCanvas(); targets = collectTargets(); total = targets.length;
        spawn(.13); spawn(.48); spawn(.83); running = true;
        controls = new AbortController();
        listen(dialog, "cancel", event => { event.preventDefault(); stop(); });
        listen(dialog, "close", () => { if (!dialog.open) stop(); });
        listen(dialog, "keydown", event => {
            if (event.key === "Escape") { event.preventDefault(); stop(); return; }
            const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
            if (!["ArrowLeft", "ArrowRight", "a", "d", " ", "Enter"].includes(key)) return;
            if (event.target.closest?.("button") && [" ", "Enter"].includes(key)) return;
            event.preventDefault(); keys.add(key); if (key === " " || key === "Enter") fire();
        });
        listen(window, "keyup", event => keys.delete(event.key.length === 1 ? event.key.toLowerCase() : event.key));
        listen(canvas, "pointermove", event => aimPlayer(event.clientX));
        listen(canvas, "pointerdown", event => {
            if (event.button !== 0) return;
            event.preventDefault(); canvas.focus({ preventScroll: true }); aimPlayer(event.clientX);
            pointers.set(event.pointerId, "fire"); updatePointers(); canvas.setPointerCapture?.(event.pointerId); fire();
        });
        const release = event => { pointers.delete(event.pointerId); updatePointers(); };
        listen(window, "pointerup", release); listen(window, "pointercancel", release); listen(canvas, "lostpointercapture", release);
        listen(dialog, "pointerdown", event => {
            const action = actionOf(event);
            if (!["left", "right", "fire"].includes(action) || event.button !== 0) return;
            event.preventDefault(); pointers.set(event.pointerId, action); updatePointers(); if (action === "fire") fire();
        });
        listen(window, "blur", suspend); listen(window, "focus", resume);
        listen(document, "visibilitychange", () => { if (document.hidden) suspend(); else resume(); });
        listen(window, "resize", stop); listen(window, "popstate", stop); listen(window, "hashchange", stop); listen(window, "pagehide", stop);
        observer = new MutationObserver(() => { if (document.documentElement.dataset.theme !== "newyear") stop(); });
        observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
        updateHud(); announce("Fyrværkerifesten er startet. Skyd kodefejl og dele af siden. Skyd eller fang de lysende boosts. Escape gendanner siden.");
        canvas.focus({ preventScroll: true }); draw(); frame = requestAnimationFrame(tick); return true;
    }
    function stop() {
        const wasRunning = running; running = false; paused = false;
        cancelAnimationFrame(frame); frame = 0; previous = null;
        controls?.abort(); controls = null; observer?.disconnect(); observer = null;
        keys.clear(); pointers.clear(); pointerHeld = false; steering = 0;
        restore(); targets = []; falling = []; rockets = []; labels = []; pickups = [];
        gameEffects.particles.length = gameEffects.bursts.length = 0;
        for (const kind of boostKinds) boosts[kind.type] = 0; updateBoostHud();
        if (ctx) ctx.clearRect(0, 0, width, height);
        if (dialog?.open) dialog.close();
        if (wasRunning && previousFocus?.isConnected && getComputedStyle(previousFocus).display !== "none") previousFocus.focus({ preventScroll: true });
        previousFocus = null;
    }

    // Only layout backgrounds can launch decorative fireworks. Cards, text and controls retain their normal clicks.
    const pageBackgrounds = "html,body,#app,.lup,.lup > .app,.lup #main-content,.public-home,.public-intro,"
        + ".public-home > section,.public-section-title,.public-course-browser,.public-lower,.public-lower > section";
    let pageFireworks;
    function stopPageFireworks() {
        if (!pageFireworks) return;
        const { canvas, ctx, width, height, frame, observer } = pageFireworks;
        pageFireworks = null;
        cancelAnimationFrame(frame);
        observer.disconnect();
        canvas.hidden = true;
        ctx.clearRect(0, 0, width, height);
    }
    function drawPageFireworks(now) {
        const scene = pageFireworks;
        if (!scene) return;
        if (document.documentElement.dataset.theme !== "newyear" || document.hidden || !scene.canvas.isConnected) {
            stopPageFireworks();
            return;
        }
        const step = scene.previous === null ? 0 : clamp((now - scene.previous) / 1000, 0, .05);
        scene.previous = now;
        advanceEffects(scene.effects, step);
        scene.ctx.clearRect(0, 0, scene.width, scene.height);
        scene.rockets = scene.rockets.filter(rocket => {
            rocket.age += step;
            if (rocket.age >= rocket.duration) {
                burst(scene.effects, rocket.x, rocket.targetY, rocket.color);
                return false;
            }
            const progress = Math.sin(rocket.age / rocket.duration * Math.PI / 2);
            rocket.y = rocket.startY + (rocket.targetY - rocket.startY) * progress;
            drawRocket(scene.ctx, rocket, scene.effects.animate);
            return true;
        });
        drawEffects(scene.ctx, scene.effects);
        if (scene.rockets.length || scene.effects.bursts.length || scene.effects.particles.length) {
            scene.frame = requestAnimationFrame(drawPageFireworks);
        } else {
            stopPageFireworks();
        }
    }
    function launchPageFirework(event) {
        if (running || document.documentElement.dataset.theme !== "newyear" || document.hidden
            || event.defaultPrevented || event.button !== 0 || !event.detail || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey
            || !event.target.matches?.(pageBackgrounds)
            || event.target.closest?.(".notion-ide-shell,dialog,[inert],[data-newyear-ignore]")
            || window.getSelection?.()?.isCollapsed === false
            || document.querySelector("dialog[open],.lup-header details[open],[role='dialog'][aria-modal='true']:not([hidden])")) return;
        const canvas = document.getElementById("lup-newyear-sky");
        if (!canvas) return;
        if (!pageFireworks || pageFireworks.canvas !== canvas) {
            stopPageFireworks();
            const ctx = canvas.getContext("2d");
            if (!ctx) return;
            canvas.hidden = false;
            const { width, height } = canvas.getBoundingClientRect();
            const ratio = Math.min(devicePixelRatio || 1, 2);
            canvas.width = Math.round(width * ratio);
            canvas.height = Math.round(height * ratio);
            ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
            const observer = new MutationObserver(stopPageFireworks);
            observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
            pageFireworks = { canvas, ctx, width, height, frame: 0, previous: null, observer,
                rockets: [], effects: { particles: [], bursts: [], animate: true } };
        }
        const scene = pageFireworks;
        const bounds = canvas.getBoundingClientRect();
        const x = event.clientX - bounds.left, targetY = event.clientY - bounds.top;
        const color = colors[Math.floor(Math.random() * colors.length)];
        if (scene.rockets.length < limits.bursts) {
            const startY = scene.height + 15;
            const duration = clamp((startY - targetY) / 860, .24, .84);
            scene.rockets.push({ x, targetY, startY, age: 0, duration, color, radius: 3, vx: 0, vy: -1 });
        }
        if (!scene.frame) scene.frame = requestAnimationFrame(drawPageFireworks);
    }

    // Capture the click before an open menu's outside-click handler closes it.
    document.addEventListener("click", launchPageFirework, { capture: true, passive: true });
    document.addEventListener("visibilitychange", () => { if (document.hidden) stopPageFireworks(); });
    for (const event of ["resize", "popstate", "hashchange", "pagehide"]) {
        window.addEventListener(event, stopPageFireworks);
    }

    document.addEventListener("click", event => {
        const action = actionOf(event); if (!action) return;
        if (action === "start") { event.preventDefault(); start(); }
        else if (action === "stop") { event.preventDefault(); stop(); }
        else if (action === "restart" || action === "replay") { event.preventDefault(); stop(); start(); }
        else if (running && action === "fire") { event.preventDefault(); fire(); }
        else if (running && (action === "left" || action === "right")) {
            event.preventDefault(); movePlayer(playerX + (action === "left" ? -34 : 34) * (boosts.speed > 0 ? 1.6 : 1)); draw();
        }
    });
    window.lupNewYear = { start, stop, state };
})();
