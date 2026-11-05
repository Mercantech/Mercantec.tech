(() => {
    "use strict";
    if (window.lupNewYear) return;

    const duration = 60;
    const colors = ["#f6d889", "#ff927e", "#80dce8"];
    const keys = new Set();
    const destroyed = new Set();
    const pointers = new Map();
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    let dialog, canvas, ctx, observer, previousFocus;
    let running = false, finished = false, paused = false, frame = 0;
    let width = 0, height = 0, shipX = 0, shipY = 0, previous = 0, targetTop = 106;
    let elapsed = 0, score = 0, total = 0, cooldown = 0, announced = 0;
    let targets = [], rockets = [], particles = [], controls;
    let pointerHeld = false, steering = 0;

    const query = selector => dialog?.querySelector(selector);
    const setText = (selector, text) => { const node = query(selector); if (node) node.textContent = String(text); };
    const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
    const remaining = () => targets.filter(target => !target.hit && target.element.isConnected).length;
    const state = () => ({ running, finished, paused, score, remaining: remaining(), total, seconds: Math.max(0, Math.ceil(duration - elapsed)) });

    function announce(message) { setText("[data-newyear-status]", message); }

    function updateHud() {
        setText("[data-newyear-score]", score);
        setText("[data-newyear-time]", Math.max(0, Math.ceil(duration - elapsed)));
        setText("[data-newyear-remaining]", remaining());
    }

    function restore() {
        for (const element of destroyed) element.classList.remove("newyear-destroyed");
        destroyed.clear();
    }

    function collectTargets() {
        const selector = ".lup h1,.lup h2,.lup h3,.lup p,.lup li,.lup a,.lup button,.lup .pill,.lup .context,.lup .tile,.lup .v3-next,.lup .panel";
        const candidates = Array.from(document.querySelectorAll(selector)).filter(element => {
            if (element.closest("form,dialog,[contenteditable],.newyear-corner,[data-newyear-ignore]")) return false;
            if (element.classList.contains("newyear-destroyed")) return false;
            if (element.querySelector("iframe,input,textarea,select,[contenteditable]")) return false;
            const style = getComputedStyle(element);
            if (style.visibility !== "visible" || style.display === "none" || Number(style.opacity) === 0) return false;
            const rect = element.getBoundingClientRect();
            return rect.width >= 18 && rect.height >= 12 && rect.width <= Math.max(760, width * .85)
                && rect.height <= 180 && rect.left >= 0 && rect.right <= width
                && rect.top >= targetTop && rect.bottom <= shipY - 38;
        });
        // Only keep leaves, so destroying a card never hides another live target.
        return candidates.filter(element => !candidates.some(other => other !== element && element.contains(other)))
            .slice(0, 70).map((element, index) => {
                const rect = element.getBoundingClientRect();
                return { element, x: rect.left, y: rect.top, width: rect.width, height: rect.height, color: colors[index % colors.length], hit: false };
            });
    }

    function resizeCanvas() {
        const bounds = canvas.getBoundingClientRect();
        width = bounds.width; height = bounds.height;
        const ratio = Math.min(devicePixelRatio || 1, 2);
        canvas.width = Math.round(width * ratio); canvas.height = Math.round(height * ratio);
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
        const hudBottom = query("[data-newyear-hud]")?.getBoundingClientRect().bottom || 94;
        const controlsTop = query("[data-newyear-controls]")?.getBoundingClientRect().top || height - 80;
        targetTop = hudBottom + 12;
        shipX = width / 2; shipY = Math.max(targetTop + 44, controlsTop - 28);
    }

    function fire() {
        if (!running || finished || paused || cooldown > 0) return;
        rockets.push({ x: shipX, y: shipY - 24, previousY: shipY - 24 });
        cooldown = .18;
    }

    function burst(x, y, color) {
        if (motion.matches) return;
        for (let i = 0; i < 24; i++) {
            const angle = Math.PI * 2 * i / 24;
            const speed = 45 + Math.random() * 95;
            particles.push({ x, y, vx: Math.cos(angle) * speed, vy: Math.sin(angle) * speed, life: .65 + Math.random() * .3, color });
        }
    }

    function hit(target, x, y) {
        target.hit = true;
        target.element.classList.add("newyear-destroyed");
        destroyed.add(target.element);
        score += 100;
        burst(x, y, target.color);
        updateHud();
        if (score - announced >= 500) {
            announced = score;
            announce(score + " point. " + remaining() + " mål tilbage.");
        }
    }

    function drawRocket(rocket) {
        ctx.fillStyle = "#f6d889";
        ctx.beginPath(); ctx.moveTo(rocket.x, rocket.y - 13); ctx.lineTo(rocket.x + 4, rocket.y - 6);
        ctx.lineTo(rocket.x + 4, rocket.y + 5); ctx.lineTo(rocket.x - 4, rocket.y + 5);
        ctx.lineTo(rocket.x - 4, rocket.y - 6); ctx.closePath(); ctx.fill();
        ctx.fillStyle = "#ff927e";
        ctx.fillRect(rocket.x - 2, rocket.y + 6, 4, motion.matches ? 5 : 11);
        ctx.fillStyle = "#80dce8"; ctx.fillRect(rocket.x - 1, rocket.y - 5, 2, 4);
    }

    function drawShip() {
        ctx.fillStyle = "#e9f7fa";
        ctx.beginPath(); ctx.moveTo(shipX, shipY - 22); ctx.lineTo(shipX + 17, shipY + 12);
        ctx.lineTo(shipX + 7, shipY + 8); ctx.lineTo(shipX - 7, shipY + 8);
        ctx.lineTo(shipX - 17, shipY + 12); ctx.closePath(); ctx.fill();
        ctx.fillStyle = "#80dce8"; ctx.fillRect(shipX - 3, shipY - 5, 6, 8);
        ctx.fillStyle = "#ff927e"; ctx.fillRect(shipX - 4, shipY + 11, 8, 8);
        ctx.strokeStyle = "#80dce84d"; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.moveTo(18, shipY + 30); ctx.lineTo(width - 18, shipY + 30); ctx.stroke();
    }

    function draw() {
        ctx.clearRect(0, 0, width, height);
        ctx.lineWidth = 1;
        for (const target of targets) {
            if (target.hit || !target.element.isConnected) continue;
            ctx.strokeStyle = target.color + "88";
            ctx.strokeRect(target.x - 3, target.y - 3, target.width + 6, target.height + 6);
            // Corner ticks make the existing interface read as the invader formation.
            ctx.fillStyle = target.color;
            ctx.fillRect(target.x - 4, target.y - 4, 9, 2);
            ctx.fillRect(target.x + target.width - 5, target.y + target.height + 2, 9, 2);
        }
        for (const rocket of rockets) drawRocket(rocket);
        for (const particle of particles) {
            ctx.globalAlpha = Math.max(0, Math.min(1, particle.life * 1.5));
            ctx.fillStyle = particle.color; ctx.fillRect(particle.x - 1.5, particle.y - 1.5, 3, 3);
        }
        ctx.globalAlpha = 1;
        if (!finished) drawShip();
    }

    function finish() {
        if (finished) return;
        finished = true; keys.clear(); pointers.clear(); pointerHeld = false; steering = 0;
        cancelAnimationFrame(frame); frame = 0;
        rockets = []; particles = []; draw(); updateHud();
        const cleared = remaining() === 0 && total > 0;
        const title = !total ? "Ingen mål i sigte" : cleared ? "Godt nytår! Banen er ryddet" : "Tiden er gået";
        const copy = !total ? "Afslut spillet og prøv en side med mere indhold." : score + " point · " + (score / 100) + " af " + total + " mål ramt. Spil igen, eller gendan siden.";
        setText("[data-newyear-result-title]", title);
        setText("[data-newyear-result-copy]", copy);
        const result = query("[data-newyear-result]"); if (result) result.hidden = false;
        announce(title + ". " + copy);
        query('[data-newyear-result] [data-newyear-action="replay"]')?.focus();
    }

    function tick(now) {
        frame = 0;
        if (!running || finished || paused) return;
        if (!canvas.isConnected || !dialog.open || document.documentElement.dataset.theme !== "newyear") { stop(); return; }
        const step = previous ? Math.min((now - previous) / 1000, .05) : 0;
        elapsed += previous ? Math.max(0, (now - previous) / 1000) : 0;
        previous = now; cooldown = Math.max(0, cooldown - step);
        const horizontal = steering || (keys.has("ArrowRight") || keys.has("d") ? 1 : 0) - (keys.has("ArrowLeft") || keys.has("a") ? 1 : 0);
        shipX = clamp(shipX + horizontal * 410 * step, 22, width - 22);
        if (pointerHeld || keys.has(" ") || keys.has("Enter")) fire();
        for (const rocket of rockets) {
            rocket.previousY = rocket.y; rocket.y -= 590 * step;
            // Swept collision handles small links even at a low frame rate.
            let target = null;
            for (const candidate of targets) {
                if (candidate.hit || !candidate.element.isConnected || rocket.x < candidate.x - 5 || rocket.x > candidate.x + candidate.width + 5) continue;
                if (rocket.y - 13 <= candidate.y + candidate.height && rocket.previousY + 5 >= candidate.y
                    && (!target || candidate.y + candidate.height > target.y + target.height)) target = candidate;
            }
            if (target) { hit(target, rocket.x, Math.max(target.y, Math.min(rocket.y, target.y + target.height))); rocket.y = -100; }
        }
        rockets = rockets.filter(rocket => rocket.y > -30);
        for (const particle of particles) {
            particle.x += particle.vx * step; particle.y += particle.vy * step;
            particle.vy += 80 * step; particle.life -= step;
        }
        particles = particles.filter(particle => particle.life > 0);
        updateHud(); draw();
        if (elapsed >= duration || remaining() === 0) finish();
        else frame = requestAnimationFrame(tick);
    }

    function listen(target, name, callback, options = {}) {
        target.addEventListener(name, callback, { ...options, signal: controls.signal });
    }

    function updatePointers() {
        const actions = [...pointers.values()];
        pointerHeld = actions.includes("fire");
        steering = Math.sign(actions.filter(action => action === "right").length - actions.filter(action => action === "left").length);
    }

    function suspend() {
        keys.clear(); pointers.clear(); pointerHeld = false; steering = 0;
        paused = true; previous = 0; cancelAnimationFrame(frame); frame = 0;
    }

    function resume() {
        if (!running || finished || document.hidden || !paused) return;
        paused = false; previous = 0; frame = requestAnimationFrame(tick);
    }

    function actionOf(event) { return event.target.closest?.("[data-newyear-action]")?.dataset.newyearAction; }

    function start() {
        if (document.documentElement.dataset.theme !== "newyear") return false;
        if (running) return true;
        dialog = document.getElementById("lup-newyear-game");
        canvas = document.getElementById("lup-newyear-canvas");
        if (!dialog || !canvas || typeof dialog.showModal !== "function") return false;
        ctx = canvas.getContext("2d"); if (!ctx) return false;
        previousFocus = document.activeElement;
        restore(); keys.clear(); rockets = []; particles = [];
        score = 0; total = 0; elapsed = 0; cooldown = 0; announced = 0; previous = 0;
        finished = false; paused = false; pointers.clear(); steering = 0; pointerHeld = false;

        const result = query("[data-newyear-result]"); if (result) result.hidden = true;
        try { dialog.showModal(); } catch { targets = []; return false; }
        resizeCanvas(); targets = collectTargets(); total = targets.length;
        running = true;
        controls = new AbortController();
        listen(dialog, "cancel", event => { event.preventDefault(); stop(); });
        listen(dialog, "close", () => { if (!dialog.open) stop(); });
        listen(dialog, "keydown", event => {
            if (event.key === "Escape") { event.preventDefault(); stop(); return; }
            const key = event.key.length === 1 ? event.key.toLowerCase() : event.key;
            if (!["ArrowLeft", "ArrowRight", "a", "d", " ", "Enter"].includes(key) || finished) return;
            if (event.target.closest?.("button") && [" ", "Enter"].includes(key)) return;
            event.preventDefault(); keys.add(key);
            if (key === " " || key === "Enter") fire();
        });
        listen(window, "keyup", event => keys.delete(event.key.length === 1 ? event.key.toLowerCase() : event.key));
        listen(canvas, "pointermove", event => { if (!finished) shipX = clamp(event.clientX, 22, width - 22); });
        listen(canvas, "pointerdown", event => {
            if (event.button !== 0 || finished) return;
            event.preventDefault(); canvas.focus({ preventScroll: true });
            shipX = clamp(event.clientX, 22, width - 22);
            pointers.set(event.pointerId, "fire"); updatePointers();
            canvas.setPointerCapture?.(event.pointerId); fire();
        });
        const release = event => { pointers.delete(event.pointerId); updatePointers(); };
        listen(window, "pointerup", release); listen(window, "pointercancel", release);
        listen(canvas, "lostpointercapture", release);
        listen(dialog, "pointerdown", event => {
            const action = actionOf(event);
            if ((action !== "left" && action !== "right" && action !== "fire") || finished || event.button !== 0) return;
            event.preventDefault();
            pointers.set(event.pointerId, action); updatePointers();
            if (action === "fire") fire();
        });
        listen(window, "blur", suspend); listen(window, "focus", resume);
        listen(document, "visibilitychange", () => { if (document.hidden) suspend(); else resume(); });
        listen(window, "resize", stop); listen(window, "popstate", stop); listen(window, "hashchange", stop); listen(window, "pagehide", stop);
        observer = new MutationObserver(() => { if (document.documentElement.dataset.theme !== "newyear") stop(); });
        observer.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
        updateHud(); announce("Nytårsspil startet. Flyt med piletaster eller A og D, og skyd med mellemrum. Escape gendanner siden.");
        canvas.focus({ preventScroll: true }); draw();
        if (!total) finish(); else frame = requestAnimationFrame(tick);
        return true;
    }

    function stop() {
        const wasRunning = running;
        running = false; finished = false; paused = false;
        cancelAnimationFrame(frame); frame = 0; previous = 0;
        controls?.abort(); controls = null; observer?.disconnect(); observer = null;
        keys.clear(); pointers.clear(); pointerHeld = false; steering = 0;
        restore(); targets = []; rockets = []; particles = [];
        if (ctx) ctx.clearRect(0, 0, width, height);
        if (dialog?.open) dialog.close();
        const result = query("[data-newyear-result]"); if (result) result.hidden = true;
        if (wasRunning && previousFocus?.isConnected && getComputedStyle(previousFocus).display !== "none") previousFocus.focus({ preventScroll: true });
        previousFocus = null;
    }

    document.addEventListener("click", event => {
        const action = actionOf(event);
        if (!action) return;
        if (action === "start") { event.preventDefault(); start(); }
        else if (action === "stop") { event.preventDefault(); stop(); }
        else if (action === "replay") { event.preventDefault(); stop(); start(); }
        else if (running && !finished && action === "fire") { event.preventDefault(); fire(); }
        else if (running && !finished && (action === "left" || action === "right")) {
            event.preventDefault(); shipX = clamp(shipX + (action === "left" ? -34 : 34), 22, width - 22); draw();
        }
    });

    window.lupNewYear = { start, stop, state };
})();
