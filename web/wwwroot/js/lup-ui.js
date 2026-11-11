(() => {
    const themes = new Set(["system", "light", "dark", "christmas", "newyear", "hacker"]);
    const read = (key, fallback = null) => { try { return localStorage.getItem(key) ?? fallback; } catch { return fallback; } };
    const save = (key, value) => { try { localStorage.setItem(key, value); } catch { /* Themes also work without storage. */ } };
    const normalize = value => themes.has(value) ? value : "light";

    let brandArtwork;
    function syncBrandFavicon() {
        const icon = document.head?.querySelector("link[data-lup-brand-icon]");
        if (!icon) return;
        brandArtwork ??= fetch(icon.dataset.lupBrandIcon)
            .then(response => {
                if (!response.ok) throw new Error("Logo could not be loaded");
                return response.text();
            })
            .then(source => {
                const svg = new DOMParser().parseFromString(source, "image/svg+xml");
                const mark = svg.querySelector("g#mark");
                if (!mark) throw new Error("Logo artwork is missing");
                return mark.outerHTML;
            })
            .catch(() => {
                brandArtwork = undefined;
                return null;
            });
        brandArtwork.then(mark => {
            if (!mark || !icon.isConnected) return;
            const style = getComputedStyle(document.documentElement);
            const colors = ["l", "u", "p", "tile"].map(part => style.getPropertyValue(`--lup-logo-${part}`).trim());
            if (!colors.every(color => /^#[0-9a-f]{6}$/i.test(color))) return;
            const [l, u, p, tile] = colors;
            const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="-2 -2 14 14"
                style="--lup-logo-l:${l};--lup-logo-u:${u};--lup-logo-p:${p}">
                <rect x="-2" y="-2" width="14" height="14" rx="3" fill="${tile}"/>
                <g shape-rendering="crispEdges">${mark}</g>
            </svg>`;
            const href = "data:image/svg+xml," + encodeURIComponent(svg);
            if (icon.getAttribute("href") !== href) icon.setAttribute("href", href);
        });
    }
    // HeadOutlet replaces the default favicon while a notebook supplies its own.
    if (document.head && typeof MutationObserver === "function") {
        new MutationObserver(syncBrandFavicon).observe(document.head, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ["data-lup-brand-icon"]
        });
    }

    let dockFrame = 0, dockFooter, dockObserver, dockClearance = -1;
    function updateSeasonalDock() {
        dockFrame = 0;
        const footer = dockFooter?.isConnected ? dockFooter : document.querySelector?.(".lup .footer");
        const rect = footer?.getBoundingClientRect();
        const clearance = rect && rect.top < innerHeight && rect.bottom > 0
            ? Math.ceil(innerHeight - rect.top) : 0;
        if (clearance === dockClearance) return;
        dockClearance = clearance;
        document.documentElement.style?.setProperty("--lup-footer-clearance", clearance + "px");
    }
    function scheduleSeasonalDock() {
        if (!dockFrame) dockFrame = requestAnimationFrame(updateSeasonalDock);
    }
    function mountSeasonalDock() {
        const footer = document.querySelector?.(".lup .footer");
        if (footer !== dockFooter) {
            dockObserver?.disconnect();
            dockFooter = footer;
            if (footer && typeof ResizeObserver === "function") {
                dockObserver = new ResizeObserver(scheduleSeasonalDock);
                dockObserver.observe(footer);
                for (const selector of [".lup .body", ".lup .lup-header", ".lup .app"]) {
                    const region = document.querySelector(selector);
                    if (region) dockObserver.observe(region);
                }
            }
        }
        updateSeasonalDock();
    }

    let canvas, context, frame = 0, flakes = [], previous = 0, wind = 0, pointerX = -1000, pointerY = -1000;
    let width = 0, height = 0;
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
        syncSnow();
    }
    function draw(time) {
        const step = previous ? Math.min((time - previous) / 1000, .04) : 0;
        previous = time;
        context.clearRect(0, 0, width, height);
        context.fillStyle = "#e8fff6";
        for (const flake of flakes) {
            const dx = flake.x - pointerX, dy = flake.y - pointerY;
            const distance = Math.hypot(dx, dy);
            const push = distance > 0 && distance < 110 ? (110 - distance) * .8 : 0;
            flake.x += (wind * 18 + Math.sin(time / 1800 + flake.phase) * 9 + (dx / (distance || 1)) * push) * step;
            flake.y += (flake.speed + (dy / (distance || 1)) * push) * step;
            if (flake.y > height + 5) { flake.y = -5; flake.x = Math.random() * width; }
            if (flake.x > width + 5) flake.x = -5;
            if (flake.x < -5) flake.x = width + 5;
            context.globalAlpha = .28 + flake.r / 8;
            context.beginPath(); context.arc(flake.x, flake.y, flake.r, 0, Math.PI * 2); context.fill();
        }
        context.globalAlpha = 1;
        frame = requestAnimationFrame(draw);
    }
    function syncSnow() {
        cancelAnimationFrame(frame); frame = 0; previous = 0;
        if (!canvas) return;
        context.clearRect(0, 0, width, height);
        if (document.documentElement.dataset.theme !== "christmas" || document.hidden) return;
        frame = requestAnimationFrame(draw);
    }
    window.lupTheme = {
        get() {
            const stored = read("lup-theme");
            return normalize(stored ?? "light");
        },
        resolved(theme) {
            const value = normalize(theme || this.get());
            return ["dark", "christmas", "newyear", "hacker"].includes(value) ? "dark" : value === "light" ? "light" : matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
        },
        apply(theme) {
            const value = normalize(theme || this.get());
            document.documentElement.dataset.theme = value;
            document.documentElement.dataset.bsTheme = this.resolved(value);
            syncBrandFavicon();
            updateSeasonalDock();
            syncSnow();
        },
        set(value) { const theme = normalize(value); save("lup-theme", theme); this.apply(theme); },
        mount() {
            mountSeasonalDock();
            const host = document.getElementById("lup-snow-host");
            if (!host || canvas?.isConnected) return;
            canvas = document.createElement("canvas"); context = canvas.getContext("2d");
            if (!context) { canvas = undefined; return; }
            host.appendChild(canvas); resize();
        }
    };
    addEventListener("scroll", scheduleSeasonalDock, { passive: true });
    addEventListener("resize", scheduleSeasonalDock, { passive: true });
    addEventListener("resize", resize, { passive: true });
    addEventListener("pointermove", event => {
        pointerX = event.clientX; pointerY = event.clientY;
        wind = (event.clientX / Math.max(innerWidth, 1) - .5) * 2;
    }, { passive: true });
    addEventListener("pointerout", event => { if (!event.relatedTarget) { pointerX = -1000; pointerY = -1000; } }, { passive: true });
    document.addEventListener("visibilitychange", syncSnow);
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
    close(id) { const dialog = document.getElementById(id); if (dialog?.open) dialog.close(); },
    focus(id, selector) {
        const dialog = document.getElementById(id);
        if (!dialog?.open) return;
        const target = dialog.querySelector(selector);
        if (!target) return;
        if (selector === "#gift-feedback" || selector === "#gift-error") dialog.scrollTop = 0;
        target.focus({ preventScroll: true });
        target.scrollIntoView({ block: "nearest", behavior: "auto" });
    }
};

window.lupCyberLabScroll = log => {
    if (log) log.scrollTop = log.scrollHeight;
};

window.lupCharacterUi = {
    async scrollToSection(id) {
        // Wait for route rendering and FocusOnNavigate before moving to the gallery.
        await new Promise(resolve => requestAnimationFrame(resolve));
        if (window.location.hash !== "#" + id) return;
        const heading = document.getElementById(id);
        if (!heading) return;
        heading.setAttribute("tabindex", "-1");
        heading.scrollIntoView({ block: "start", behavior: "instant" });
        heading.focus({ preventScroll: true });
    }
};

/** Kopiér tekst til udklipsholder — bruges af kursus-kodeblokke. */
window.courseCode = {
    async copy(text) {
        const value = text ?? "";
        try {
            if (navigator.clipboard?.writeText) {
                await navigator.clipboard.writeText(value);
                return true;
            }
        } catch { /* fald tilbage til execCommand */ }
        try {
            const ta = document.createElement("textarea");
            ta.value = value;
            ta.setAttribute("readonly", "");
            ta.style.position = "fixed";
            ta.style.left = "-9999px";
            document.body.appendChild(ta);
            ta.select();
            const ok = document.execCommand("copy");
            ta.remove();
            return !!ok;
        } catch {
            return false;
        }
    }
};
