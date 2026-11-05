(() => {
    "use strict";
    if (window.lupNewYear) return;

    const colors = ["#f6d889", "#ff927e", "#80dce8", "#c39af1"];
    const kinds = [
        { type: "balloon", hp: 1, points: 50, width: 34, height: 45, color: "#ff927e" },
        { type: "star", hp: 1, points: 75, width: 38, height: 38, color: "#f6d889" },
        { type: "gift", hp: 2, points: 150, width: 40, height: 38, color: "#c39af1" },
        { type: "disco", hp: 3, points: 250, width: 42, height: 42, color: "#80dce8" },
        { type: "hat", hp: 1, points: 100, width: 38, height: 44, color: "#ffb37f" },
        { type: "window", hp: 2, points: 200, width: 60, height: 42, color: "#9ed7be" }
    ];
    const limits = { falling: 28, rockets: 36, particles: 360 };
    const keys = new Set(), destroyed = new Set(), pointers = new Map();
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    let dialog, canvas, ctx, observer, previousFocus, playerImage, controls;
    let running = false, paused = false, frame = 0, previous = null;
    let width = 0, height = 0, playerX = 0, floor = 0, targetTop = 106;
    let elapsed = 0, score = 0, hits = 0, total = 0, level = 1, cooldown = 0, announced = 0;
    let spawnWait = 0, spawned = 0, escaped = 0;
    let targets = [], falling = [], rockets = [], particles = [];
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
    const state = () => ({
        running, paused, score, hits, level, remaining: remaining(), total,
        fallingCount: falling.length, projectileCount: rockets.length, particleCount: particles.length,
        spawned, escaped, playerX, fireOriginX: muzzle().x, spawnInterval: spawnInterval(), fallSpeed: fallSpeed(),
        falling: falling.map(({ type, x, y, width, height, hp, points }) => ({ type, x, y, width, height, hp, points }))
    });

    function announce(message) { setText("[data-newyear-status]", message); }
    function updateHud() {
        setText("[data-newyear-score]", score);
        setText("[data-newyear-level]", level);
        setText("[data-newyear-hits]", hits);
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
        const origin = muzzle();
        rockets.push({ x: origin.x, y: origin.y, previousY: origin.y });
        cooldown = .18;
    }
    function burst(x, y, color, count = 24) {
        if (motion.matches) return;
        for (let i = 0; i < count && particles.length < limits.particles; i++) {
            const angle = Math.PI * 2 * i / count, speed = 45 + Math.random() * 95;
            particles.push({ x, y, vx: Math.cos(angle) * speed, vy: Math.sin(angle) * speed, life: .65 + Math.random() * .3, color });
        }
    }
    function hit(target, x, y) {
        target.hp--;
        burst(x, y, target.color, target.hp > 0 ? 7 : 24);
        if (target.hp > 0) return;
        target.hit = true;
        if (target.element) {
            target.element.classList.add("newyear-destroyed");
            destroyed.add(target.element);
        }
        score += target.points; hits++;
        updateHud();
        if (score - announced >= 500) {
            announced = score;
            announce(score + " point. " + hits + " mål ramt. Der kommer flere!");
        }
    }
    function spawn(lane) {
        if (falling.length >= Math.min(limits.falling, 7 + level)) return;
        const kind = kinds[spawned % kinds.length];
        const available = Math.max(0, width - kind.width - 36);
        const fraction = typeof lane === "number" ? lane : .08 + Math.random() * .84;
        const y = targetTop + 12;
        falling.push({ ...kind, maxHp: kind.hp, x: 18 + available * fraction, y, previousY: y, hit: false, speedFactor: .85 + Math.random() * .3 });
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
        ctx.fillStyle = color; ctx.strokeStyle = "#f0efffcc"; ctx.lineWidth = 1.5;
        if (type === "balloon") {
            ctx.beginPath(); ctx.ellipse(x + w / 2, y + h * .35, w * .42, h * .32, 0, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
            ctx.fillStyle = "#fff9"; ctx.beginPath(); ctx.ellipse(x + w * .37, y + h * .25, 2.5, 5, .3, 0, Math.PI * 2); ctx.fill();
            ctx.strokeStyle = "#f6d889"; ctx.beginPath(); ctx.moveTo(x + w / 2, y + h * .68); ctx.quadraticCurveTo(x + w * .2, y + h * .82, x + w * .55, y + h); ctx.stroke();
        } else if (type === "star") {
            ctx.beginPath();
            for (let i = 0; i < 10; i++) {
                const angle = i * Math.PI / 5 - Math.PI / 2, radius = w * (i % 2 ? .23 : .49);
                const px = x + w / 2 + Math.cos(angle) * radius, py = y + h / 2 + Math.sin(angle) * radius;
                if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
            }
            ctx.closePath(); ctx.fill(); ctx.stroke();
            ctx.fillStyle = "#a26b28"; ctx.fillRect(x + 13, y + 16, 3, 3); ctx.fillRect(x + 23, y + 16, 3, 3);
        } else if (type === "gift") {
            roundRect(x + 2, y + 9, w - 4, h - 9, 4); ctx.fill(); ctx.stroke();
            ctx.fillStyle = "#f6d889"; ctx.fillRect(x + w / 2 - 3, y + 9, 6, h - 9); ctx.fillRect(x, y + 9, w, 7);
            ctx.strokeStyle = "#f6d889"; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(x + w / 2, y + 9);
            ctx.quadraticCurveTo(x + 4, y - 5, x + 9, y + 5); ctx.quadraticCurveTo(x + 14, y + 10, x + w / 2, y + 9);
            ctx.quadraticCurveTo(x + w - 4, y - 5, x + w - 9, y + 5); ctx.quadraticCurveTo(x + w - 14, y + 10, x + w / 2, y + 9); ctx.stroke();
        } else if (type === "disco") {
            const r = w / 2 - 2;
            ctx.save(); ctx.beginPath(); ctx.arc(x + w / 2, y + h / 2, r, 0, Math.PI * 2); ctx.fill(); ctx.clip();
            ctx.strokeStyle = "#254e70"; ctx.lineWidth = 1;
            for (let i = 1; i < 5; i++) {
                ctx.beginPath(); ctx.moveTo(x, y + i * h / 5); ctx.lineTo(x + w, y + i * h / 5); ctx.stroke();
                ctx.beginPath(); ctx.ellipse(x + w / 2, y + h / 2, i * w / 10, r, 0, 0, Math.PI * 2); ctx.stroke();
            }
            ctx.fillStyle = "#fffa"; ctx.fillRect(x + 11, y + 9, 6, 6); ctx.fillRect(x + 24, y + 23, 5, 5);
            ctx.restore(); ctx.strokeStyle = "#eaffff"; ctx.beginPath(); ctx.arc(x + w / 2, y + h / 2, r, 0, Math.PI * 2); ctx.stroke();
        } else if (type === "hat") {
            ctx.beginPath(); ctx.moveTo(x + w / 2, y + 3); ctx.lineTo(x + w - 2, y + h - 4); ctx.lineTo(x + 2, y + h - 4); ctx.closePath(); ctx.fill(); ctx.stroke();
            ctx.fillStyle = "#c39af1"; ctx.beginPath(); ctx.arc(x + w / 2, y + 3, 4, 0, Math.PI * 2); ctx.fill();
            ctx.strokeStyle = "#8f52ad"; ctx.lineWidth = 5; ctx.beginPath(); ctx.moveTo(x + 9, y + h - 17); ctx.lineTo(x + w - 9, y + h - 17); ctx.stroke();
            ctx.fillStyle = "#f6d889"; ctx.fillRect(x, y + h - 5, w, 5);
        } else {
            ctx.fillStyle = "#1d3850"; roundRect(x, y, w, h, 6); ctx.fill(); ctx.strokeStyle = color; ctx.stroke();
            ctx.fillStyle = color; ctx.fillRect(x + 1, y + 8, w - 2, 2);
            for (let i = 0; i < 3; i++) { ctx.beginPath(); ctx.arc(x + 7 + i * 6, y + 5, 1.3, 0, Math.PI * 2); ctx.fill(); }
            ctx.fillStyle = "#80dce8"; ctx.fillRect(x + 7, y + 17, 12, 15);
            ctx.fillStyle = "#f6d889"; ctx.fillRect(x + 25, y + 17, w - 32, 3); ctx.fillRect(x + 25, y + 25, w - 38, 3);
        }
        if (item.maxHp > 1) {
            for (let i = 0; i < item.maxHp; i++) {
                ctx.fillStyle = i < item.hp ? "#edf3ff" : "#66728b";
                ctx.fillRect(x + w / 2 - item.maxHp * 4 + i * 8, y - 7, 5, 3);
            }
        }
    }
    function drawRocket(rocket) {
        ctx.fillStyle = "#ff927e"; ctx.fillRect(rocket.x - 3, rocket.y - 9, 6, 15);
        ctx.fillStyle = "#f6d889";
        ctx.beginPath(); ctx.moveTo(rocket.x, rocket.y - 15); ctx.lineTo(rocket.x + 5, rocket.y - 8); ctx.lineTo(rocket.x - 5, rocket.y - 8); ctx.closePath(); ctx.fill();
        ctx.fillRect(rocket.x - 1, rocket.y + 6, 2, 7);
        if (!motion.matches) {
            ctx.fillStyle = "#f6d889aa"; ctx.fillRect(rocket.x - 1.5, rocket.y + 14, 3, 9);
            ctx.fillStyle = "#ff927e88"; ctx.fillRect(rocket.x - 1, rocket.y + 25, 2, 5);
        }
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
        for (const rocket of rockets) drawRocket(rocket);
        for (const particle of particles) {
            ctx.globalAlpha = clamp(particle.life * 1.5, 0, 1); ctx.fillStyle = particle.color;
            ctx.fillRect(particle.x - 1.5, particle.y - 1.5, 3, 3);
        }
        ctx.globalAlpha = 1; drawPlayer();
    }
    function impactAt(rocket, target) {
        if (target.hit || target.element && !target.element.isConnected || rocket.x < target.x - 4 || rocket.x > target.x + target.width + 4) return null;
        const start = rocket.previousY - (target.previousY ?? target.y), end = rocket.y - target.y;
        const velocity = end - start, low = -5, high = target.height + 15;
        if (start >= low && start <= high) return 0;
        if (velocity >= 0 || start < low || end > high) return null;
        const at = (high - start) / velocity;
        return at >= 0 && at <= 1 ? at : null;
    }
    function tick(now) {
        frame = 0;
        if (!running || paused) return;
        if (!canvas.isConnected || !dialog.open || document.documentElement.dataset.theme !== "newyear") { stop(); return; }
        const delta = previous === null ? 0 : Math.max(0, (now - previous) / 1000), step = Math.min(delta, .05);
        previous = now; elapsed += delta; cooldown = Math.max(0, cooldown - step);
        const nextLevel = Math.min(20, 1 + Math.floor(elapsed / 16) + Math.floor(hits / 12));
        if (nextLevel !== level) { level = nextLevel; announce("Niveau " + level + ". Flere mål og mere fart!"); }
        const horizontal = steering || (keys.has("ArrowRight") || keys.has("d") ? 1 : 0) - (keys.has("ArrowLeft") || keys.has("a") ? 1 : 0);
        movePlayer(playerX + horizontal * 410 * step);
        if (pointerHeld || keys.has(" ") || keys.has("Enter")) fire();
        spawnWait -= delta;
        let spawnBudget = 3;
        while (spawnWait <= 0 && spawnBudget-- > 0) { spawn(); spawnWait += spawnInterval(); }
        if (spawnWait <= 0) spawnWait = spawnInterval();
        for (const item of falling) { item.previousY = item.y; item.y += fallSpeed() * item.speedFactor * step; }
        for (const rocket of rockets) {
            rocket.previousY = rocket.y; rocket.y -= 590 * step;
            let target = null, firstImpact = 2;
            for (const candidate of [...falling, ...targets]) {
                const impact = impactAt(rocket, candidate);
                if (impact !== null && impact < firstImpact) { target = candidate; firstImpact = impact; }
            }
            if (target) {
                hit(target, rocket.x, rocket.previousY + (rocket.y - rocket.previousY) * firstImpact);
                rocket.y = -100;
            }
        }
        rockets = rockets.filter(rocket => rocket.y > -35);
        falling = falling.filter(item => {
            if (item.hit) return false;
            if (item.y > floor) { escaped++; return false; }
            return true;
        });
        for (const particle of particles) { particle.x += particle.vx * step; particle.y += particle.vy * step; particle.vy += 80 * step; particle.life -= step; }
        particles = particles.filter(particle => particle.life > 0);
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
        targets = []; falling = []; rockets = []; particles = [];
        score = 0; hits = 0; level = 1; total = 0; elapsed = 0; cooldown = 0; announced = 0; previous = null;
        spawnWait = .65; spawned = 0; escaped = 0; paused = false; steering = 0; pointerHeld = false;
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
        updateHud(); announce("Fyrværkerifesten er startet. Skyd faldende festting og dele af siden. Der kommer flere og hurtigere mål. Escape gendanner siden.");
        canvas.focus({ preventScroll: true }); draw(); frame = requestAnimationFrame(tick); return true;
    }
    function stop() {
        const wasRunning = running; running = false; paused = false;
        cancelAnimationFrame(frame); frame = 0; previous = null;
        controls?.abort(); controls = null; observer?.disconnect(); observer = null;
        keys.clear(); pointers.clear(); pointerHeld = false; steering = 0;
        restore(); targets = []; falling = []; rockets = []; particles = [];
        if (ctx) ctx.clearRect(0, 0, width, height);
        if (dialog?.open) dialog.close();
        if (wasRunning && previousFocus?.isConnected && getComputedStyle(previousFocus).display !== "none") previousFocus.focus({ preventScroll: true });
        previousFocus = null;
    }
    document.addEventListener("click", event => {
        const action = actionOf(event); if (!action) return;
        if (action === "start") { event.preventDefault(); start(); }
        else if (action === "stop") { event.preventDefault(); stop(); }
        else if (action === "restart" || action === "replay") { event.preventDefault(); stop(); start(); }
        else if (running && action === "fire") { event.preventDefault(); fire(); }
        else if (running && (action === "left" || action === "right")) {
            event.preventDefault(); movePlayer(playerX + (action === "left" ? -34 : 34)); draw();
        }
    });
    window.lupNewYear = { start, stop, state };
})();
