(() => {
    const themes = new Set(["system", "light", "dark", "christmas", "hacker"]);
    const read = (key, fallback = null) => { try { return localStorage.getItem(key) ?? fallback; } catch { return fallback; } };
    const save = (key, value) => { try { localStorage.setItem(key, value); } catch { /* Themes also work without storage. */ } };
    const signedIn = () => { try { return !!sessionStorage.getItem("mt_access_token"); } catch { return false; } };
    const normalize = value => themes.has(value) ? value : "light";
    const motion = matchMedia("(prefers-reduced-motion: reduce)");
    let canvas, context, frame = 0, flakes = [], previous = 0, wind = 0, pointerX = -1000, pointerY = -1000;
    let width = 0, height = 0;
    const snowMode = () => {
        const mode = read("lup-snow-mode");
        return ["auto", "on", "off"].includes(mode) ? mode : read("lup-snow-paused", "false") === "true" ? "off" : "auto";
    };
    const snowPaused = () => snowMode() === "off" || (snowMode() === "auto" && motion.matches);
    const snowState = () => ({ mode: snowMode(), paused: snowPaused(), reducedMotion: motion.matches, hidden: document.hidden });
    const snowObservers = new Map();
    let snowObserverId = 0;
    function notifySnow() {
        for (const [id, observer] of snowObservers) observer.invokeMethodAsync("SnowChanged", snowState()).catch(() => snowObservers.delete(id));
    }
    function resize() {
        if (!canvas) return;
        width = innerWidth; height = innerHeight;
        const ratio = Math.min(devicePixelRatio || 1, 2);
        canvas.width = width * ratio; canvas.height = height * ratio;
        context.setTransform(ratio, 0, 0, ratio, 0, 0);
        flakes = Array.from({ length: Math.min(90, Math.floor(width / 13)) }, () => ({
            x: Math.random() * width, y: Math.random() * height, r: 1 + Math.random() * 2.2,
            speed: 12 + Math.random() * 28, phase: Math.random() * Math.PI * 2
        }));
        draw(0, true);
    }
    function draw(time, still = false) {
        const step = previous ? Math.min((time - previous) / 1000, .04) : 0;
        previous = time;
        context.clearRect(0, 0, width, height);
        context.fillStyle = "#e8fff6";
        for (const flake of flakes) {
            if (!still) {
                const dx = flake.x - pointerX, dy = flake.y - pointerY;
                const distance = Math.hypot(dx, dy);
                const push = distance > 0 && distance < 110 ? (110 - distance) * .8 : 0;
                flake.x += (wind * 18 + Math.sin(time / 1800 + flake.phase) * 9 + (dx / (distance || 1)) * push) * step;
                flake.y += (flake.speed + (dy / (distance || 1)) * push) * step;
                if (flake.y > height + 5) { flake.y = -5; flake.x = Math.random() * width; }
                if (flake.x > width + 5) flake.x = -5;
                if (flake.x < -5) flake.x = width + 5;
            }
            context.globalAlpha = .28 + flake.r / 8;
            context.beginPath(); context.arc(flake.x, flake.y, flake.r, 0, Math.PI * 2); context.fill();
        }
        context.globalAlpha = 1;
        if (!still) frame = requestAnimationFrame(draw);
    }
    function syncSnow() {
        cancelAnimationFrame(frame); frame = 0; previous = 0;
        notifySnow();
        if (!canvas) return;
        context.clearRect(0, 0, width, height);
        if (document.documentElement.dataset.theme !== "christmas" || document.hidden) return;
        if (snowPaused()) draw(0, true);
        else frame = requestAnimationFrame(draw);
    }
    window.lupTheme = {
        get() {
            const stored = read("lup-theme");
            if (!signedIn() && (!stored || stored === "system")) return "light";
            return normalize(stored ?? "light");
        },
        resolved(theme) {
            const value = normalize(theme || this.get());
            return ["dark", "christmas", "hacker"].includes(value) ? "dark" : value === "light" ? "light" : matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        },
        apply(theme) {
            const value = normalize(theme || this.get());
            document.documentElement.dataset.theme = value;
            document.documentElement.dataset.bsTheme = this.resolved(value);
            syncSnow();
        },
        set(value) { const theme = normalize(value); save("lup-theme", theme); this.apply(theme); },
        mount() {
            const host = document.getElementById("lup-snow-host");
            if (!host || canvas?.isConnected) return;
            canvas = document.createElement("canvas"); context = canvas.getContext("2d");
            if (!context) return;
            host.appendChild(canvas); resize(); syncSnow();
        },
        snowPaused,
        snowState,
        observeSnow(observer) { const id = ++snowObserverId; snowObservers.set(id, observer); return id; },
        unobserveSnow(id) { snowObservers.delete(id); },
        setSnowMode(mode) {
            save("lup-snow-mode", ["auto", "on", "off"].includes(mode) ? mode : "auto");
            save("lup-snow-paused", "false");
            syncSnow();
            return snowState();
        },
        toggleSnow() { this.setSnowMode(snowPaused() ? "on" : "off"); return snowPaused(); }
    };
    addEventListener("resize", resize, { passive: true });
    addEventListener("pointermove", event => {
        pointerX = event.clientX; pointerY = event.clientY;
        wind = (event.clientX / Math.max(innerWidth, 1) - .5) * 2;
    }, { passive: true });
    addEventListener("pointerout", event => { if (!event.relatedTarget) { pointerX = -1000; pointerY = -1000; } }, { passive: true });
    document.addEventListener("visibilitychange", syncSnow);
    motion.addEventListener("change", syncSnow);
    matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
        if (window.lupTheme.get() === "system") window.lupTheme.apply("system");
    });
    document.addEventListener("click", event => {
        for (const details of document.querySelectorAll(".lup-header details[open]")) {
            if (!details.contains(event.target)) details.open = false;
        }
        if (event.target.closest(".lup-header a")) {
            document.querySelectorAll(".lup-header details[open]").forEach(details => details.open = false);
        }
    });
    document.addEventListener("keydown", event => {
        if (event.target.closest?.(".avatar-game") && ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key)) event.preventDefault();
        if (event.key === "Escape") {
            const open = [...document.querySelectorAll(".lup-header details[open]")];
            open.forEach(details => details.open = false);
            open.at(-1)?.querySelector("summary")?.focus();
        }
    });
    window.lupCourse = {
        get() { return read("lup-demo-course"); },
        set(value) { save("lup-demo-course", value || ""); dispatchEvent(new CustomEvent("lup:course", { detail: value || "" })); }
    };
    window.lupIcons = () => { if (window.lucide?.createIcons) window.lucide.createIcons(); };
})();

for (const [name, prefix] of [["lupAppearance", "lup-appearance-v1:"], ["lupRoom", "lup-room-v1:"]]) {
    window[name] = {
        get(subject) { try { return localStorage.getItem(prefix + (subject || "guest")); } catch { return null; } },
        set(subject, json) { try { localStorage.setItem(prefix + (subject || "guest"), json); return true; } catch { return false; } }
    };
}

window.lupDemo = {
    get() { try { return localStorage.getItem("lup-demo-world"); } catch { return null; } },
    set(value) { try { localStorage.setItem("lup-demo-world", value || ""); } catch { /* Local interactions remain available without storage. */ } }
};

window.lupGiftUi = {
    open(id) { const dialog = document.getElementById(id); if (dialog && !dialog.open) dialog.showModal(); },
    close(id) { const dialog = document.getElementById(id); if (dialog?.open) dialog.close(); }
};
