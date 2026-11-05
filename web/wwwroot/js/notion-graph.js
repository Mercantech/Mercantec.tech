const sessions = new WeakMap();

export function mount(host, dotnet, payload) {
    destroy(host);
    const session = create(host, dotnet, payload);
    if (session) sessions.set(host, session);
}

export function destroy(host) {
    const session = sessions.get(host);
    if (!session) return;
    session.dispose();
    sessions.delete(host);
}

export function zoom(host, direction) {
    const session = sessions.get(host);
    if (!session) return;
    if (direction === 0) session.fit();
    else session.zoomBy(direction > 0 ? 1.15 : 1 / 1.15);
    session.draw();
}

function create(host, dotnet, payload) {
    const nodes = Array.isArray(payload?.nodes) ? payload.nodes : [];
    const edges = Array.isArray(payload?.edges) ? payload.edges : [];
    if (nodes.length === 0 || !host) return null;
    const stage = host.parentElement ?? host;
    const canvas = document.createElement("canvas");
    canvas.setAttribute("aria-hidden", "true");
    host.replaceChildren(canvas);
    const ctx = canvas.getContext("2d");
    if (!ctx) return null;

    const inbound = new Map(nodes.map((node) => [String(node.id), 0]));
    const links = [];
    for (const edge of edges) {
        const from = String(edge.fromId);
        const to = String(edge.toId);
        if (!inbound.has(from) || !inbound.has(to) || from === to) continue;
        links.push({ from, to });
        inbound.set(to, inbound.get(to) + 1);
    }

    const points = nodes.map((node, index) => {
        const count = nodes.length;
        const turn = (index / count) * Math.PI * 2 - Math.PI / 2;
        const ring = 70 + count * 7 + (index % 3) * 18;
        return {
            id: String(node.id),
            title: String(node.title || "Uden titel"),
            r: Math.min(54, 14 + inbound.get(String(node.id)) * 12),
            x: Math.cos(turn) * ring,
            y: Math.sin(turn) * ring,
            vx: 0,
            vy: 0
        };
    });
    const byId = new Map(points.map((point) => [point.id, point]));
    settle(points, links, byId);

    let width = 0;
    let height = 0;
    let dpr = 1;
    let zoom = 1;
    let panX = 0;
    let panY = 0;
    let originX = 0;
    let originY = 0;
    let fitted = false;
    let hover = null;
    let drag = null;
    const ac = new AbortController();
    const signal = ac.signal;

    function bounds() {
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        for (const point of points) {
            minX = Math.min(minX, point.x - point.r - 70);
            maxX = Math.max(maxX, point.x + point.r + 70);
            minY = Math.min(minY, point.y - point.r - 36);
            maxY = Math.max(maxY, point.y + point.r + 42);
        }
        return { minX, minY, maxX, maxY, cx: (minX + maxX) / 2, cy: (minY + maxY) / 2, w: Math.max(1, maxX - minX), h: Math.max(1, maxY - minY) };
    }

    function screenOf(point) {
        return {
            x: (point.x - originX + panX) * zoom + width / 2,
            y: (point.y - originY + panY) * zoom + height / 2,
            r: Math.max(8, point.r * zoom)
        };
    }

    function worldAt(sx, sy) {
        return {
            x: (sx - width / 2) / zoom - panX + originX,
            y: (sy - height / 2) / zoom - panY + originY
        };
    }

    function zoomAt(sx, sy, factor) {
        const before = worldAt(sx, sy);
        zoom = clamp(zoom * factor, 0.25, 4);
        panX = (sx - width / 2) / zoom - (before.x - originX);
        panY = (sy - height / 2) / zoom - (before.y - originY);
    }

    function fit() {
        const box = bounds();
        originX = box.cx;
        originY = box.cy;
        panX = 0;
        panY = 0;
        zoom = clamp(Math.min((width - 72) / box.w, (height - 72) / box.h), 0.25, 1.6);
    }

    function resize() {
        width = host.clientWidth || stage.clientWidth;
        height = host.clientHeight || stage.clientHeight;
        if (width < 2 || height < 2) return;
        dpr = Math.min(window.devicePixelRatio || 1, 2);
        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        canvas.style.width = width + "px";
        canvas.style.height = height + "px";
        if (!fitted) { fit(); fitted = true; }
        draw();
    }

    function hit(event) {
        const rect = canvas.getBoundingClientRect();
        const x = event.clientX - rect.left;
        const y = event.clientY - rect.top;
        for (let i = points.length - 1; i >= 0; i--) {
            const at = screenOf(points[i]);
            if (Math.hypot(at.x - x, at.y - y) <= at.r + 3) return points[i];
        }
        return null;
    }

    function related(id) {
        const set = new Set([id]);
        for (const link of links) {
            if (link.from === id) set.add(link.to);
            if (link.to === id) set.add(link.from);
        }
        return set;
    }

    const themeRoot = host.closest(".lup") ?? document.documentElement;
    const themeProbe = document.createElement("span");
    themeProbe.hidden = true;
    themeProbe.style.cssText = "position:absolute;visibility:hidden;pointer-events:none;";
    themeRoot.appendChild(themeProbe);

    function themeColors() {
        const style = themeProbe.style;
        const read = (name, prop) => {
            style.color = prop === "color" ? `var(${name})` : "";
            style.backgroundColor = prop === "backgroundColor" ? `var(${name})` : "";
            const computed = getComputedStyle(themeProbe);
            const value = prop === "backgroundColor" ? computed.backgroundColor : computed.color;
            return value && value !== "rgba(0, 0, 0, 0)" ? value : "";
        };
        const stageBg = getComputedStyle(stage).backgroundColor;
        return {
            paper: stageBg && stageBg !== "rgba(0, 0, 0, 0)" ? stageBg : read("--paper", "backgroundColor") || "#ffffff",
            ink: read("--ink", "color") || "#17243b",
            muted: read("--muted", "color") || "#63728b",
            blue: read("--blue", "color") || "#2452e8",
            rule: read("--rule", "backgroundColor") || read("--rule", "color") || "#dfe7f2",
            tint: read("--tint", "backgroundColor") || read("--tint", "color") || "#eaf0ff"
        };
    }

    function draw() {
        if (width < 2 || height < 2) return;
        const { paper, ink, muted, blue, rule, tint } = themeColors();
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = paper;
        ctx.fillRect(0, 0, width, height);
        const gap = 48 * zoom;
        if (gap >= 14) {
            ctx.fillStyle = rule;
            ctx.globalAlpha = 0.45;
            const origin = screenOf({ x: 0, y: 0, r: 0 });
            for (let x = origin.x % gap; x < width; x += gap) {
                for (let y = origin.y % gap; y < height; y += gap) {
                    ctx.fillRect(x, y, 1.2, 1.2);
                }
            }
            ctx.globalAlpha = 1;
        }
        const focus = hover ? related(hover) : null;
        for (const link of links) {
            const from = byId.get(link.from);
            const to = byId.get(link.to);
            if (!from || !to) continue;
            const hot = focus?.has(link.from) && focus.has(link.to);
            const a = screenOf(from);
            const b = screenOf(to);
            const dx = b.x - a.x;
            const dy = b.y - a.y;
            const len = Math.hypot(dx, dy) || 1;
            ctx.globalAlpha = focus && !hot ? 0.12 : hot ? 1 : 0.72;
            ctx.strokeStyle = hot ? blue : muted;
            ctx.lineWidth = hot ? 2.5 : 1.5;
            ctx.beginPath();
            ctx.moveTo(a.x + (dx / len) * a.r, a.y + (dy / len) * a.r);
            ctx.lineTo(b.x - (dx / len) * b.r, b.y - (dy / len) * b.r);
            ctx.stroke();
        }
        const limit = points.length > 24 ? 14 : 22;
        for (const point of points) {
            const at = screenOf(point);
            const hot = focus?.has(point.id) === true;
            const dim = focus && !hot;
            ctx.globalAlpha = dim ? 0.18 : 1;
            ctx.beginPath();
            ctx.arc(at.x, at.y, at.r, 0, Math.PI * 2);
            ctx.fillStyle = point.id === hover ? tint : paper;
            ctx.fill();
            ctx.lineWidth = point.id === hover ? 2.75 : 1.6;
            ctx.strokeStyle = blue;
            ctx.stroke();
            if (zoom < 0.62 && !hot) continue;
            const label = point.title.length > limit ? point.title.slice(0, limit - 1) + "…" : point.title;
            ctx.font = '600 12px "DM Sans", sans-serif';
            ctx.lineWidth = 4;
            ctx.strokeStyle = paper;
            ctx.fillStyle = ink;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            const y = at.y + at.r + 14;
            ctx.strokeText(label, at.x, y);
            ctx.fillText(label, at.x, y);
        }
        if (hover && byId.has(hover)) {
            const point = byId.get(hover);
            const at = screenOf(point);
            const refs = inbound.get(point.id) || 0;
            const extra = refs === 0 ? "Ingen sider linker hertil" : refs === 1 ? "1 side linker hertil" : refs + " sider linker hertil";
            ctx.globalAlpha = 1;
            ctx.font = '600 13px "DM Sans", sans-serif';
            const titleWidth = ctx.measureText(point.title).width;
            ctx.font = '500 11px "DM Sans", sans-serif';
            const extraWidth = ctx.measureText(extra).width;
            const pad = 10;
            const chipW = Math.min(Math.max(titleWidth, extraWidth) + pad * 2, width - 24);
            let chipX = at.x + at.r + 14;
            if (chipX + chipW > width - 8) chipX = at.x - at.r - 14 - chipW;
            chipX = clamp(chipX, 8, Math.max(8, width - chipW - 8));
            const chipY = clamp(at.y - 21, 8, Math.max(8, height - 50));
            ctx.fillStyle = paper;
            ctx.strokeStyle = rule;
            ctx.lineWidth = 1;
            roundRect(ctx, chipX, chipY, chipW, 42, 7);
            ctx.fill();
            ctx.stroke();
            ctx.textAlign = "left";
            ctx.textBaseline = "middle";
            ctx.save();
            ctx.beginPath();
            ctx.rect(chipX + pad, chipY, chipW - pad * 2, 42);
            ctx.clip();
            ctx.font = '600 13px "DM Sans", sans-serif';
            ctx.fillStyle = ink;
            ctx.fillText(point.title, chipX + pad, chipY + 14);
            ctx.font = '500 11px "DM Sans", sans-serif';
            ctx.fillStyle = muted;
            ctx.fillText(extra, chipX + pad, chipY + 30);
            ctx.restore();
        }
        ctx.globalAlpha = 1;
    }

    function local(event) {
        const rect = canvas.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    canvas.addEventListener("pointerdown", (event) => {
        if (event.button !== 0 && event.button !== 1) return;
        canvas.setPointerCapture(event.pointerId);
        drag = { id: event.pointerId, x: event.clientX, y: event.clientY, moved: false, button: event.button };
        stage.focus({ preventScroll: true });
    }, { signal });
    canvas.addEventListener("pointermove", (event) => {
        if (!drag || drag.id !== event.pointerId) {
            hover = hit(event)?.id ?? null;
            canvas.style.cursor = hover ? "pointer" : "grab";
            draw();
            return;
        }
        const dx = event.clientX - drag.x;
        const dy = event.clientY - drag.y;
        if (!drag.moved && Math.hypot(dx, dy) <= 4) return;
        drag.moved = true;
        panX += dx / zoom;
        panY += dy / zoom;
        drag.x = event.clientX;
        drag.y = event.clientY;
        canvas.style.cursor = "grabbing";
        draw();
    }, { signal });
    canvas.addEventListener("pointerup", (event) => {
        if (!drag || drag.id !== event.pointerId) return;
        const opened = !drag.moved && drag.button === 0 ? hit(event) : null;
        drag = null;
        hover = hit(event)?.id ?? null;
        canvas.style.cursor = hover ? "pointer" : "grab";
        draw();
        if (opened) call(dotnet, "OnGraphOpen", opened.id);
    }, { signal });
    canvas.addEventListener("pointercancel", () => { drag = null; }, { signal });
    canvas.addEventListener("pointerleave", () => {
        if (drag) return;
        hover = null;
        canvas.style.cursor = "grab";
        draw();
    }, { signal });
    canvas.addEventListener("contextmenu", (event) => {
        event.preventDefault();
        const found = hit(event);
        call(dotnet, "OnGraphMenu", found ? found.id : "", Math.min(event.clientX, window.innerWidth - 220), Math.min(event.clientY, window.innerHeight - 180));
    }, { signal });
    stage.addEventListener("wheel", (event) => {
        if (!host.contains(event.target) && event.target !== stage) return;
        event.preventDefault();
        const at = local(event);
        zoomAt(at.x, at.y, event.deltaY < 0 ? 1.12 : 1 / 1.12);
        draw();
    }, { signal, passive: false });
    stage.addEventListener("keydown", (event) => {
        if (event.target.closest("button, a, input, select, textarea")) return;
        const step = (event.shiftKey ? 220 : 56) / zoom;
        let used = true;
        if (event.key === "ArrowLeft") panX -= step;
        else if (event.key === "ArrowRight") panX += step;
        else if (event.key === "ArrowUp") panY -= step;
        else if (event.key === "ArrowDown") panY += step;
        else if (event.key === "+" || event.key === "=") zoomAt(width / 2, height / 2, 1.15);
        else if (event.key === "-" || event.key === "_") zoomAt(width / 2, height / 2, 1 / 1.15);
        else if (event.key === "0") fit();
        else used = false;
        if (!used) return;
        event.preventDefault();
        draw();
    }, { signal });

    const observer = new ResizeObserver(resize);
    observer.observe(host);
    observer.observe(stage);
    const theme = new MutationObserver(draw);
    theme.observe(document.documentElement, { attributes: true, attributeFilter: ["class", "data-theme", "style"] });
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    media.addEventListener("change", draw, { signal });
    resize();

    return {
        draw,
        fit,
        zoomBy(factor) { zoomAt(width / 2, height / 2, factor); },
        dispose() {
            ac.abort();
            observer.disconnect();
            theme.disconnect();
            themeProbe.remove();
            if (host.contains(canvas)) canvas.remove();
        }
    };
}

function settle(points, links, byId) {
    const steps = Math.min(360, 90 + points.length * 8);
    for (let step = 0; step < steps; step++) {
        for (let i = 0; i < points.length; i++) {
            for (let j = i + 1; j < points.length; j++) {
                const a = points[i];
                const b = points[j];
                let dx = a.x - b.x;
                let dy = a.y - b.y;
                let dist = Math.hypot(dx, dy) || 0.01;
                const min = a.r + b.r + 42;
                const push = dist < min ? (min - dist) * 0.09 : 0;
                const force = (5400 / (dist * dist) + push) / dist;
                a.vx += dx * force;
                a.vy += dy * force;
                b.vx -= dx * force;
                b.vy -= dy * force;
            }
        }
        for (const link of links) {
            const a = byId.get(link.from);
            const b = byId.get(link.to);
            if (!a || !b) continue;
            const dx = b.x - a.x;
            const dy = b.y - a.y;
            const dist = Math.hypot(dx, dy) || 0.01;
            const force = 0.05 * (dist - 168) / dist;
            a.vx += dx * force;
            a.vy += dy * force;
            b.vx -= dx * force;
            b.vy -= dy * force;
        }
        for (const point of points) {
            point.vx = (point.vx - point.x * 0.012) * 0.72;
            point.vy = (point.vy - point.y * 0.012) * 0.72;
            const speed = Math.hypot(point.vx, point.vy);
            if (speed > 16) { point.vx *= 16 / speed; point.vy *= 16 / speed; }
            point.x += point.vx;
            point.y += point.vy;
        }
    }
}

function call(dotnet, method, ...args) {
    dotnet.invokeMethodAsync(method, ...args).catch(() => {});
}

function clamp(value, min, max) { return Math.max(min, Math.min(max, value)); }

function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
}
