import { forceCollide, forceLink, forceManyBody, forceSimulation, forceX, forceY } from "./vendor/d3-force.bundle.js";

const sessions = new WeakMap();
const defaults = {
    activeId: null,
    showLabels: true,
    showArrows: false,
    labelSize: 1,
    labelWeight: 500,
    highlightConnections: true,
    dimUnrelated: true,
    labelThreshold: 0.2,
    nodeSize: 1,
    linkThickness: 1,
    centerForce: 1,
    repelForce: 1,
    linkForce: 1,
    linkDistance: 1
};
const minZoom = 0.03;
const maxZoom = 7;
const fontFamily = '"DM Sans", system-ui, sans-serif';
const settingsKey = "lup.connections.graph.v1";
const visualOptions = Object.keys(defaults).filter(name => name !== "activeId");
const fullCircle = 2 * Math.PI;

export function mount(host, dotnet, payload) {
    destroy(host);
    sessions.set(host, create(host, dotnet, payload));
}

export function update(host, payload) {
    sessions.get(host)?.update(payload);
}

export function configure(host, options) {
    sessions.get(host)?.configure(options);
}

// Only visual preferences are persisted. References, selected nodes and graph data never enter storage.
export function readSettings() {
    try { return savedOptions(JSON.parse(window.localStorage.getItem(settingsKey) || "{}")); }
    catch { return savedOptions({}); }
}

export function saveSettings(options) {
    const value = savedOptions(options);
    try { window.localStorage.setItem(settingsKey, JSON.stringify(value)); } catch { /* Private browsing may disable storage. */ }
    return value;
}

export function resetSettings() {
    try { window.localStorage.removeItem(settingsKey); } catch { /* Defaults work without storage. */ }
    return savedOptions({});
}

export function focus(host, id) {
    sessions.get(host)?.focus(String(id));
}

export function zoom(host, direction) {
    const session = sessions.get(host);
    if (direction === 0) session?.fit();
    else session?.zoomBy(direction > 0 ? 1.25 : 1 / 1.25);
}

export function destroy(host) {
    sessions.get(host)?.dispose();
    sessions.delete(host);
}

function create(host, dotnet, payload) {
    const stage = host.closest(".notion-graph-stage") ?? host.parentElement ?? host;
    const canvas = document.createElement("canvas");
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("Canvas 2D is not available");
    canvas.setAttribute("aria-hidden", "true");
    Object.assign(canvas.style, { width: "100%", height: "100%", display: "block", touchAction: "none" });

    const announcer = document.createElement("span");
    announcer.setAttribute("role", "status");
    announcer.style.cssText = "position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap";
    host.replaceChildren(canvas, announcer);

    const controller = new AbortController();
    const { signal } = controller;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const colorScheme = window.matchMedia("(prefers-color-scheme: dark)");
    const cache = new Map();
    const emojiImages = new Map();
    const pointers = new Map();
    const springs = forceLink();
    const charge = forceManyBody().theta(0.9).distanceMin(16);
    const collision = forceCollide();
    const centerX = forceX();
    const centerY = forceY();
    // One frame loop drives the physics, camera and drawing. D3's own timer stays stopped.
    const simulation = forceSimulation().stop()
        .alphaMin(0.018).alphaDecay(0.045).velocityDecay(0.42)
        .force("links", springs).force("charge", charge).force("collision", collision)
        .force("centerX", centerX).force("centerY", centerY).alpha(0);

    let nodes = [];
    let links = [];
    let byId = new Map();
    let labels = [];
    let settings = defaults;
    let palette;
    let width = 0;
    let height = 0;
    let dpr = 1;
    let frame = 0;
    let lastFrame = 0;
    let lastTick = -Infinity;
    let settleTicks = 0;
    let camera = { x: 0, y: 0, scale: 1 };
    let transition = null;
    let autoFit = true;
    let hasFit = false;
    let pointer = null;
    let drag = null;
    let pressTimer;
    let hovered = null;
    let selected = null;
    let emphasis = null;
    let emphasisOpacity = 0;
    let reportedEmojiError = false;

    function applyForces() {
        const center = 0.032 * settings.centerForce;
        centerX.strength(center);
        centerY.strength(center);
        charge.strength(point => (point.neighbors.size ? -150 : -100) * settings.repelForce);
        collision.radius(point => point.radius + 7);
        springs.distance(link => (57 + link.source.radius + link.target.radius) * settings.linkDistance);
        springs.strength(link => 0.72 * settings.linkForce
            / Math.min(link.source.links.length, link.target.links.length));
    }

    function setRadii() {
        for (const point of nodes) {
            const incoming = point.totalInbound ?? point.inbound;
            point.radius = Math.min(19, 4.1 + Math.sqrt(incoming) * 2.3) * settings.nodeSize;
        }
    }

    function reheat(alpha) {
        if (nodes.length < 2) return;
        simulation.alpha(Math.max(alpha, simulation.alpha()));
        if (reducedMotion.matches) settleTicks = Math.max(settleTicks, 32);
        requestFrame();
    }

    function configure(options) {
        const previous = settings;
        settings = graphOptions(previous, options);
        if (settings.labelSize !== previous.labelSize || settings.labelWeight !== previous.labelWeight) {
            for (const point of cache.values()) point.labelRect = null;
        }
        if (forcesChanged(previous, settings)) {
            setRadii();
            applyForces();
            reheat(0.48);
        }
        requestFrame();
    }

    function updateGraph(data) {
        const previousNodes = nodes;
        const previousLinks = links;
        const nextById = new Map();
        for (const raw of data?.nodes ?? []) {
            if (raw.id == null) continue;
            const id = String(raw.id);
            if (nextById.has(id)) continue;
            const point = cache.get(id) ?? { id };
            const title = String(raw.title || "Uden titel");
            const titleParts = Array.isArray(raw.titleParts) ? raw.titleParts.filter(part => typeof part?.text === "string") : [];
            if (title !== point.title || point.titleParts?.length || titleParts.length) point.label = null;
            point.title = title;
            point.titleParts = titleParts;
            point.spokenTitle = titleParts.map(part => part.imageUrl && part.alt ? ` ${part.alt} ` : part.text)
                .join("").replace(/\s+/g, " ").trim() || title;
            point.color = typeof raw.color === "string" && CSS.supports("color", raw.color) ? raw.color : null;
            point.totalInbound = typeof raw.inbound === "number" && raw.inbound >= 0 ? raw.inbound : null;
            point.inbound = 0;
            point.links = [];
            point.neighbors = new Set();
            cache.set(id, point);
            nextById.set(id, point);
        }

        const seen = new Map();
        links = [];
        for (const raw of data?.edges ?? []) {
            const source = nextById.get(String(raw.fromId));
            const target = nextById.get(String(raw.toId));
            if (!source || !target || source === target) continue;
            let destinations = seen.get(source);
            if (!destinations) {
                destinations = new Set();
                seen.set(source, destinations);
            }
            if (destinations.has(target)) continue;
            destinations.add(target);
            const link = { source, target };
            links.push(link);
            source.links.push(link);
            target.links.push(link);
            source.neighbors.add(target);
            target.neighbors.add(source);
            target.inbound++;
        }

        byId = nextById;
        nodes = [...byId.values()];
        labels = [...nodes].sort((a, b) => (b.totalInbound ?? b.inbound) - (a.totalInbound ?? a.inbound) ||
            b.links.length - a.links.length);
        if (selected && !byId.has(selected.id)) select(null);
        else if (selected && announcer.textContent !== selected.spokenTitle) announcer.textContent = selected.spokenTitle;
        if (emphasis && !byId.has(emphasis.id)) {
            emphasis = null;
            emphasisOpacity = 0;
        }
        if (drag?.node && !byId.has(drag.node.id)) {
            clearTimeout(pressTimer);
            releaseNode();
            drag = null;
        }

        const sameTopology = nodes.length === previousNodes.length && links.length === previousLinks.length &&
            nodes.every((point, index) => point === previousNodes[index]) &&
            links.every((link, index) => link.source === previousLinks[index].source && link.target === previousLinks[index].target);
        const previousSettings = settings;
        settings = graphOptions(settings, data?.options);
        const physicsChanged = forcesChanged(previousSettings, settings);
        const previousRadii = nodes.map(point => point.radius);
        setRadii();
        if (!sameTopology) {
            // Detach old links before D3 changes the shared node indices.
            springs.links([]);
            simulation.nodes(nodes);
            springs.links(links);
        }
        const radiiChanged = nodes.some((point, index) => point.radius !== previousRadii[index]);
        if (!sameTopology || physicsChanged || radiiChanged) {
            applyForces();
            reheat(!hasFit ? 0.9 : physicsChanged ? 0.48 : 0.22);
        }
        if (nodes.length < 2) {
            simulation.alpha(0);
            settleTicks = 0;
        }
        requestFrame();
    }

    function fitCamera() {
        if (!nodes.length) return null;
        let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
        for (const point of nodes) {
            left = Math.min(left, point.x - point.radius);
            right = Math.max(right, point.x + point.radius);
            top = Math.min(top, point.y - point.radius);
            bottom = Math.max(bottom, point.y + point.radius);
        }
        const padding = Math.min(112, Math.min(width, height) * 0.28);
        const controls = width < 700 ? 48 : 32;
        const scale = clamp(Math.min((width - padding) / (right - left),
            (height - padding - controls) / (bottom - top)), minZoom, 1.45);
        return {
            x: (left + right) / 2,
            y: (top + bottom) / 2 + controls / (2 * scale),
            scale
        };
    }

    function stopCamera() {
        autoFit = false;
        transition = null;
    }

    function moveCamera(destination) {
        stopCamera();
        if (reducedMotion.matches) camera = destination;
        else transition = { from: camera, to: destination, start: performance.now() };
        requestFrame();
    }

    function fit() {
        const destination = fitCamera();
        if (destination) moveCamera(destination);
    }

    function local(event) {
        const rect = canvas.getBoundingClientRect();
        return { x: event.clientX - rect.left, y: event.clientY - rect.top };
    }

    function worldAt(x, y) {
        return { x: camera.x + (x - width / 2) / camera.scale, y: camera.y + (y - height / 2) / camera.scale };
    }

    function zoomAt(x, y, factor) {
        const before = worldAt(x, y);
        const scale = clamp(camera.scale * factor, minZoom, maxZoom);
        camera = { x: before.x - (x - width / 2) / scale, y: before.y - (y - height / 2) / scale, scale };
    }

    function zoomBy(factor) {
        const current = transition?.to ?? camera;
        moveCamera({ ...current, scale: clamp(current.scale * factor, minZoom, maxZoom) });
    }

    function pan(x, y) {
        camera = { ...camera, x: camera.x - x / camera.scale, y: camera.y - y / camera.scale };
    }

    function select(point) {
        selected = point;
        announcer.textContent = point?.spokenTitle ?? "";
        requestFrame();
    }

    function focusNode(id) {
        if (!id) {
            pointer = hovered = emphasis = null;
            emphasisOpacity = 0;
            select(null);
            return;
        }
        const point = byId.get(id);
        if (!point) return;
        select(point);
        moveCamera({ x: point.x, y: point.y, scale: Math.max(0.9, camera.scale) });
    }

    function refreshTheme() {
        const color = (name, fallback) => {
            announcer.style.color = "var(--graph-" + name + ", " + fallback + ")";
            return getComputedStyle(announcer).color || fallback;
        };
        palette = {
            node: color("node", "#63728b"),
            edge: color("edge", "#9ba8bc"),
            active: color("active", "#2452e8"),
            label: color("label", "#17243b"),
            surface: color("surface", "#ffffff")
        };
        requestFrame();
    }

    function resize() {
        const nextDpr = Math.min(window.devicePixelRatio || 1, 2);
        if (width === host.clientWidth && height === host.clientHeight && dpr === nextDpr) return;
        width = host.clientWidth;
        height = host.clientHeight;
        dpr = nextDpr;
        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        requestFrame();
    }

    function requestFrame() {
        if (!frame && !signal.aborted) frame = requestAnimationFrame(drawFrame);
    }

    function drawFrame(now) {
        frame = 0;
        if (width < 2 || height < 2) return;
        const elapsed = Math.min(32, now - lastFrame);
        lastFrame = now;

        if (settleTicks && reducedMotion.matches) {
            const deadline = performance.now() + 7;
            do {
                simulation.tick();
                settleTicks--;
            } while (settleTicks && performance.now() < deadline);
            if (settleTicks) {
                requestFrame();
                return;
            }
            simulation.alpha(0);
        }
        const moving = !reducedMotion.matches && simulation.alpha() > simulation.alphaMin();
        if (moving && now - lastTick >= 14) {
            simulation.tick();
            lastTick = now;
        }

        if (autoFit) {
            const fitted = fitCamera();
            if (fitted) {
                camera = !hasFit || reducedMotion.matches ? fitted : blendCamera(camera, fitted, 0.18);
                hasFit = true;
                if (!moving) {
                    camera = fitted;
                    autoFit = false;
                }
            }
        }
        if (transition) {
            const t = clamp((now - transition.start) / 230, 0, 1);
            camera = blendCamera(transition.from, transition.to, 1 - (1 - t) ** 3);
            if (t === 1) transition = null;
        }

        for (const point of nodes) {
            point.sx = (point.x - camera.x) * camera.scale + width / 2;
            point.sy = (point.y - camera.y) * camera.scale + height / 2;
            point.sr = Math.max(1.7, point.radius * camera.scale);
            point.visible = point.sx + point.sr > -15 && point.sx - point.sr < width + 15 &&
                point.sy + point.sr > -15 && point.sy - point.sr < height + 15;
        }

        hovered = drag?.moved ? drag.node : pointer ? hit(pointer) : null;
        const nextEmphasis = settings.highlightConnections ? hovered ?? selected ?? byId.get(settings.activeId) : null;
        if (nextEmphasis) emphasis = nextEmphasis;
        const goal = nextEmphasis ? 1 : 0;
        emphasisOpacity = reducedMotion.matches ? goal
            : approach(emphasisOpacity, goal, Math.max(elapsed, 8) / 115);
        if (emphasisOpacity === 0) emphasis = null;
        const cursor = drag?.moved || pointers.size > 1 ? "grabbing" : hovered ? "pointer" : "grab";
        if (canvas.style.cursor !== cursor) canvas.style.cursor = cursor;
        paint();

        if (pointer && !drag?.moved && hit(pointer) !== hovered) requestFrame();
        if (moving || transition || emphasisOpacity !== goal) requestFrame();
    }

    function hit(at, touch = false) {
        let best = null;
        let bestDistance = Infinity;
        for (const point of nodes) {
            if (!point.visible) continue;
            const dx = point.sx - at.x;
            const dy = point.sy - at.y;
            const distance = dx * dx + dy * dy;
            const reach = Math.max(point.sr + 4, touch ? 14 : 8);
            if (distance > reach * reach || distance >= bestDistance) continue;
            best = point;
            bestDistance = distance;
        }
        // A visible title is also a target, including after changing its size. Node circles keep priority.
        if (!best && settings.showLabels) {
            for (const point of labels) {
                const rect = point.labelRect;
                if (!point.visible || !rect || at.x < point.sx + rect.left || at.x > point.sx + rect.right ||
                    at.y < point.sy + rect.top || at.y > point.sy + rect.bottom) continue;
                return point;
            }
        }
        return best;
    }

    function paint() {
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, width, height);
        const lineWidth = clamp(camera.scale * 0.75, 0.45, 1.55) * settings.linkThickness;
        drawLinks(links, palette.edge, lineWidth, 0.55 - (settings.dimUnrelated ? emphasisOpacity * 0.44 : 0));
        if (emphasis) drawLinks(emphasis.links, palette.active, lineWidth + 0.75, emphasisOpacity * 0.83);
        for (const point of nodes) {
            if (!point.visible) continue;
            const active = point === hovered || point === selected || point.id === settings.activeId;
            const related = active || !emphasis || point === emphasis || emphasis.neighbors.has(point);
            ctx.globalAlpha = related || !settings.dimUnrelated ? 1 : 1 - emphasisOpacity * 0.79;
            ctx.fillStyle = active ? palette.active : point.color ?? palette.node;
            ctx.beginPath();
            ctx.arc(point.sx, point.sy, point.sr, 0, fullCircle);
            ctx.fill();
            if (active) {
                ctx.globalAlpha = 0.42;
                ctx.strokeStyle = palette.active;
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.arc(point.sx, point.sy, point.sr + 4, 0, fullCircle);
                ctx.stroke();
            }
        }

        drawLabels();
        if (hovered && !drag?.moved) drawTooltip(hovered);
        ctx.globalAlpha = 1;
    }

    function drawLinks(edges, color, lineWidth, opacity) {
        if (!edges.length || opacity < 0.01) return;
        ctx.globalAlpha = opacity;
        ctx.strokeStyle = color;
        ctx.lineWidth = lineWidth;
        const arrows = settings.showArrows && camera.scale >= 0.28;
        ctx.beginPath();
        for (const { source, target } of edges) {
            if (Math.max(source.sx, target.sx) < 0 || Math.min(source.sx, target.sx) > width ||
                Math.max(source.sy, target.sy) < 0 || Math.min(source.sy, target.sy) > height) continue;
            if (!arrows) {
                ctx.moveTo(source.sx, source.sy);
                ctx.lineTo(target.sx, target.sy);
                continue;
            }
            const dx = target.sx - source.sx, dy = target.sy - source.sy;
            const distance = Math.hypot(dx, dy);
            if (distance <= source.sr + target.sr) continue;
            const ux = dx / distance, uy = dy / distance;
            const tipX = target.sx - ux * (target.sr + 1);
            const tipY = target.sy - uy * (target.sr + 1);
            ctx.moveTo(source.sx, source.sy);
            ctx.lineTo(tipX, tipY);
            if (distance > source.sr + target.sr + 11) {
                const baseX = tipX - ux * 6, baseY = tipY - uy * 6;
                ctx.moveTo(baseX - uy * 2.8, baseY + ux * 2.8);
                ctx.lineTo(tipX, tipY);
                ctx.lineTo(baseX + uy * 2.8, baseY - ux * 2.8);
            }
        }
        ctx.stroke();
    }

    function emojiImage(url) {
        if (typeof url !== "string" || !/^\/(?:images\/characters|api\/emojis)\//.test(url)) return null;
        if (emojiImages.has(url)) return emojiImages.get(url);
        const image = new Image();
        const entry = { image, failed: false };
        emojiImages.set(url, entry);
        image.onload = requestFrame;
        image.onerror = () => {
            if (signal.aborted) return;
            entry.failed = true;
            for (const point of nodes) point.label = null;
            if (!reportedEmojiError) {
                reportedEmojiError = true;
                invoke(dotnet, "OnGraphEmojiError");
            }
            requestFrame();
        };
        image.src = url;
        return entry;
    }

    function layoutTitle(point, size, limit) {
        const parts = point.titleParts.length ? point.titleParts : [{ text: point.title }];
        const runs = parts.map(part => {
            const entry = emojiImage(part.imageUrl);
            return entry && !entry.failed ? { image: entry.image, width: size * 1.2 } : measureTitleText(ctx, part.text);
        });
        const width = runs.reduce((total, run) => total + run.width, 0);
        if (width <= limit) return { runs, width };
        const ellipsis = measureTitleText(ctx, "…");
        if (ellipsis.width > limit) return { runs: [], width: 0 };
        const shortened = [];
        let remaining = limit - ellipsis.width;
        for (const run of runs) {
            if (run.width > remaining) {
                if (run.text) {
                    const tail = fitTitleText(ctx, run.text, remaining);
                    shortened.push(tail);
                    remaining -= tail.width;
                }
                break;
            }
            shortened.push(run);
            remaining -= run.width;
        }
        shortened.push(ellipsis);
        return { runs: shortened, width: limit - remaining };
    }

    function paintTitle(title, x, y, outline = false) {
        for (const run of title.runs) {
            if (run.image) {
                const { naturalWidth, naturalHeight } = run.image;
                if (run.image.complete && naturalWidth && naturalHeight) {
                    const scale = run.width / Math.max(naturalWidth, naturalHeight);
                    const w = naturalWidth * scale, h = naturalHeight * scale;
                    ctx.drawImage(run.image, x + (run.width - w) / 2, y - h / 2, w, h);
                }
            } else if (run.text) {
                if (outline) ctx.strokeText(run.text, x, y);
                ctx.fillText(run.text, x, y);
            }
            x += run.width;
        }
    }

    function drawLabels() {
        const fade = clamp((camera.scale - settings.labelThreshold * 0.62) / (settings.labelThreshold * 0.48), 0, 1);
        const root = byId.get(settings.activeId);
        const occupied = [];
        ctx.fillStyle = palette.label;
        ctx.strokeStyle = palette.surface;
        ctx.lineWidth = 3.5 * settings.labelSize;
        ctx.lineJoin = "round";
        ctx.textAlign = "left";
        ctx.textBaseline = "middle";
        for (const point of nodes) if (point !== hovered) point.labelRect = null;
        const priority = new Set([selected, root, emphasis, ...(emphasis?.neighbors ?? []), ...labels]);
        const fontSize = 12 * settings.labelSize;
        const sides = [1, -1];
        for (const point of priority) {
            if (!point?.visible || point === hovered) continue;
            const active = point === selected || point === root;
            const neighbor = emphasis?.neighbors.has(point);
            if (!active && !settings.showLabels) continue;
            const opacity = active ? 1 : neighbor ? Math.max(fade, emphasisOpacity * 0.9)
                : fade * (1 - (settings.dimUnrelated ? emphasisOpacity * 0.86 : 0));
            if (opacity < 0.035) continue;

            const font = (active ? Math.max(650, settings.labelWeight) : settings.labelWeight) + " " + fontSize + "px " + fontFamily;
            ctx.font = font;
            if (point.label?.font !== font) point.label = { ...layoutTitle(point, fontSize, 176 * settings.labelSize), font };
            const x = point.sx;
            const half = point.label.width / 2 + 4;
            const halfHeight = fontSize / 2 + 2;
            for (const side of sides) {
                const y = point.sy + side * (point.sr + 5 + fontSize / 2);
                const rect = { left: x - half, right: x + half, top: y - halfHeight, bottom: y + halfHeight };
                if (rect.right < 0 || rect.left > width || rect.top > height || rect.bottom < 0 ||
                    occupied.some(other => rect.left < other.right && rect.right > other.left &&
                        rect.top < other.bottom && rect.bottom > other.top)) continue;
                occupied.push(rect);
                point.labelRect = { left: -half, right: half, top: rect.top - point.sy, bottom: rect.bottom - point.sy };
                ctx.globalAlpha = opacity;
                paintTitle(point.label, x - point.label.width / 2, y, true);
                break;
            }
        }
    }

    function drawTooltip(point) {
        if (width < 64) return;
        const detail = point.inbound + " indgående · " + (point.links.length - point.inbound) + " udgående";
        const maxText = Math.min(270 * settings.labelSize, width - 42);
        const titleSize = 13 * settings.labelSize, detailSize = 11 * settings.labelSize, gap = 4 * settings.labelSize;
        const titleFont = Math.max(650, settings.labelWeight) + " " + titleSize + "px " + fontFamily;
        const detailFont = settings.labelWeight + " " + detailSize + "px " + fontFamily;
        ctx.font = titleFont;
        const title = layoutTitle(point, titleSize, maxText);
        ctx.font = detailFont;
        const boxWidth = Math.min(maxText, Math.max(title.width, ctx.measureText(detail).width)) + 20;
        let x = point.sx + point.sr + 12;
        if (x + boxWidth > width - 8) x = point.sx - point.sr - 12 - boxWidth;
        x = clamp(x, 8, width - boxWidth - 8);
        const boxHeight = 16 + titleSize + gap + detailSize;
        const y = clamp(point.sy - boxHeight / 2, 8, Math.max(8, height - boxHeight - 8));
        ctx.globalAlpha = 1;
        ctx.beginPath();
        ctx.roundRect(x, y, boxWidth, boxHeight, 8);
        ctx.fillStyle = palette.surface;
        ctx.fill();
        ctx.strokeStyle = palette.edge;
        ctx.lineWidth = 1;
        ctx.stroke();
        ctx.textAlign = "left";
        ctx.fillStyle = palette.label;
        ctx.font = titleFont;
        paintTitle(title, x + 10, y + 8 + titleSize * 0.55);
        ctx.font = detailFont;
        ctx.globalAlpha = 0.76;
        ctx.fillText(detail, x + 10, y + 8 + titleSize + gap + detailSize * 0.55, boxWidth - 20);
    }

    function openMenu(point, x, y) {
        invoke(dotnet, "OnGraphMenu", point?.id ?? "",
            clamp(x, 8, Math.max(8, window.innerWidth - 222)),
            clamp(y, 8, Math.max(8, window.innerHeight - 180)));
    }

    function releaseNode() {
        if (!drag?.node || !drag.moved) return;
        drag.node.fx = null;
        drag.node.fy = null;
        simulation.alphaTarget(0);
    }

    canvas.addEventListener("pointerdown", event => {
        if ((event.button !== 0 && event.button !== 1) || pointers.size >= 2) return;
        const at = local(event);
        const node = event.button === 0 ? hit(at, event.pointerType === "touch") : null;
        pointers.set(event.pointerId, at);
        canvas.setPointerCapture(event.pointerId);
        stage.focus({ preventScroll: true });
        if (event.button === 1) event.preventDefault();
        stopCamera();
        pointer = null;
        clearTimeout(pressTimer);
        if (pointers.size === 2) {
            releaseNode();
            drag = null;
        } else {
            drag = { id: event.pointerId, origin: at, node, button: event.button, moved: false, menu: false };
            if (node && event.pointerType === "touch") {
                pressTimer = setTimeout(() => {
                    if (!drag || drag.id !== event.pointerId || drag.moved) return;
                    drag.menu = true;
                    openMenu(node, event.clientX, event.clientY);
                }, 500);
            }
        }
        requestFrame();
    }, { signal });

    canvas.addEventListener("pointermove", event => {
        const at = local(event);
        const previous = pointers.get(event.pointerId);
        if (!previous) {
            if (!pointers.size && event.pointerType !== "touch") {
                pointer = at;
                requestFrame();
            }
            return;
        }

        if (pointers.size === 2) {
            const before = pinch(pointers);
            pointers.set(event.pointerId, at);
            const after = pinch(pointers);
            zoomAt(before.x, before.y, after.span / before.span);
            pan(after.x - before.x, after.y - before.y);
            requestFrame();
            return;
        }

        if (!drag) {
            // Continue moving with the last finger after a pinch, without treating it as a tap.
            drag = { id: event.pointerId, origin: previous, node: null, button: 0, moved: true, menu: false };
        }
        if (drag.menu) return;
        if (!drag.moved && Math.hypot(at.x - drag.origin.x, at.y - drag.origin.y) <= 4) return;
        clearTimeout(pressTimer);
        if (!drag.moved && drag.node) {
            const origin = worldAt(drag.origin.x, drag.origin.y);
            drag.offsetX = drag.node.x - origin.x;
            drag.offsetY = drag.node.y - origin.y;
            if (!reducedMotion.matches) {
                simulation.alphaTarget(0.18);
                reheat(0.22);
            }
        }
        drag.moved = true;
        pointers.set(event.pointerId, at);
        if (drag.node) {
            settleTicks = 0;
            const world = worldAt(at.x, at.y);
            drag.node.x = drag.node.fx = world.x + drag.offsetX;
            drag.node.y = drag.node.fy = world.y + drag.offsetY;
        } else pan(at.x - previous.x, at.y - previous.y);
        requestFrame();
    }, { signal });

    function finishPointer(event, cancelled) {
        if (!pointers.delete(event.pointerId)) return;
        if (drag?.id === event.pointerId) {
            clearTimeout(pressTimer);
            const clicked = !cancelled && !drag.moved && !drag.menu && drag.button === 0;
            const opened = clicked ? drag.node : null;
            releaseNode();
            drag = null;
            if (opened && byId.has(opened.id)) {
                select(opened);
                openMenu(opened, event.clientX, event.clientY);
            } else if (clicked) {
                select(null);
                openMenu(null, event.clientX, event.clientY);
            }
        }
        const at = local(event);
        pointer = !cancelled && event.pointerType !== "touch" &&
            at.x >= 0 && at.y >= 0 && at.x <= width && at.y <= height ? at : null;
        requestFrame();
    }

    canvas.addEventListener("pointerup", event => finishPointer(event, false), { signal });
    canvas.addEventListener("dblclick", event => {
        if (event.button !== 0 || pointers.size || drag?.moved) return;
        const point = hit(local(event));
        if (!point) return;
        event.preventDefault();
        invoke(dotnet, "OnGraphOpen", point.id);
    }, { signal });
    canvas.addEventListener("pointercancel", event => finishPointer(event, true), { signal });
    canvas.addEventListener("lostpointercapture", event => finishPointer(event, true), { signal });
    canvas.addEventListener("pointerleave", () => {
        pointer = null;
        requestFrame();
    }, { signal });
    canvas.addEventListener("contextmenu", event => {
        event.preventDefault();
        if (event.pointerType === "touch" || drag?.menu) return;
        const point = hit(local(event));
        select(point);
        openMenu(point, event.clientX, event.clientY);
    }, { signal });
    canvas.addEventListener("wheel", event => {
        if (!event.deltaY) return;
        event.preventDefault();
        stopCamera();
        pointer = local(event);
        const unit = event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? height : 1;
        const delta = clamp(event.deltaY * unit, -145, 145);
        zoomAt(pointer.x, pointer.y, Math.exp(-delta * 0.0023));
        requestFrame();
    }, { signal, passive: false });

    stage.addEventListener("keydown", event => {
        if (event.target !== stage && event.target !== host && event.target !== canvas) return;
        const step = event.shiftKey ? 190 : 56;
        switch (event.key) {
            case "ArrowLeft": stopCamera(); pan(-step, 0); break;
            case "ArrowRight": stopCamera(); pan(step, 0); break;
            case "ArrowUp": stopCamera(); pan(0, -step); break;
            case "ArrowDown": stopCamera(); pan(0, step); break;
            case "+":
            case "=": zoomBy(1.25); break;
            case "-":
            case "_": zoomBy(1 / 1.25); break;
            case "0": fit(); break;
            case "n":
            case "N":
            case "p":
            case "P": {
                if (!nodes.length || event.ctrlKey || event.metaKey || event.altKey) return;
                const previous = event.key.toLowerCase() === "p";
                const index = selected ? nodes.indexOf(selected) : previous ? 0 : -1;
                const next = (index + (previous ? -1 : 1) + nodes.length) % nodes.length;
                pointer = null;
                focusNode(nodes[next].id);
                const bounds = canvas.getBoundingClientRect();
                openMenu(nodes[next], bounds.left + nodes[next].sx, bounds.top + nodes[next].sy);
                break;
            }
            case "Enter":
                if (!selected) return;
                invoke(dotnet, "OnGraphOpen", selected.id);
                break;
            case "ContextMenu":
            case "F10": {
                if (!selected || (event.key === "F10" && !event.shiftKey)) return;
                const bounds = canvas.getBoundingClientRect();
                openMenu(selected, bounds.left + selected.sx, bounds.top + selected.sy);
                break;
            }
            case "Escape":
                if (!selected) return;
                select(null);
                openMenu(null, 8, 8);
                break;
            default: return;
        }
        event.preventDefault();
        requestFrame();
    }, { signal });

    const sizeObserver = new ResizeObserver(resize);
    sizeObserver.observe(host);
    const themeObserver = new MutationObserver(refreshTheme);
    for (let element = stage; element; element = element.parentElement) {
        themeObserver.observe(element, { attributes: true, attributeFilter: ["class", "data-theme", "style"] });
    }
    window.addEventListener("resize", resize, { signal });
    colorScheme.addEventListener("change", refreshTheme, { signal });
    reducedMotion.addEventListener("change", () => {
        if (reducedMotion.matches) {
            if (transition) camera = transition.to;
            transition = null;
            if (simulation.alpha() > simulation.alphaMin() && !drag?.moved) settleTicks = 32;
        } else settleTicks = 0;
        requestFrame();
    }, { signal });
    document.fonts?.ready.then(() => {
        if (signal.aborted) return;
        for (const point of cache.values()) point.label = null;
        requestFrame();
    });

    refreshTheme();
    updateGraph(payload);
    resize();

    return {
        update: updateGraph,
        configure,
        focus: focusNode,
        fit,
        zoomBy,
        dispose() {
            controller.abort();
            clearTimeout(pressTimer);
            if (frame) cancelAnimationFrame(frame);
            simulation.stop();
            sizeObserver.disconnect();
            themeObserver.disconnect();
            canvas.remove();
            announcer.remove();
        }
    };
}

function graphOptions(previous, options) {
    const settings = { ...previous };
    if (options && typeof options === "object" && !Array.isArray(options))
        for (const name of Object.keys(defaults)) if (Object.hasOwn(options, name)) settings[name] = options[name];
    settings.activeId = typeof settings.activeId === "string" && settings.activeId.length > 0 && settings.activeId.length <= 512 ? settings.activeId : null;
    for (const name of ["showLabels", "showArrows", "highlightConnections", "dimUnrelated"])
        if (typeof settings[name] !== "boolean") settings[name] = defaults[name];
    settings.labelSize = optionNumber(settings.labelSize, defaults.labelSize, 0.6, 2);
    settings.labelWeight = optionNumber(settings.labelWeight, defaults.labelWeight, 400, 700);
    for (const name of ["nodeSize", "linkThickness", "linkDistance", "labelThreshold"]) {
        settings[name] = optionNumber(settings[name], defaults[name], 0.2, 3);
    }
    for (const name of ["centerForce", "repelForce", "linkForce"]) {
        settings[name] = optionNumber(settings[name], defaults[name], 0, 3);
    }
    return settings;
}

function savedOptions(options) {
    const value = graphOptions(defaults, options);
    return Object.fromEntries(visualOptions.map(name => [name, value[name]]));
}

function forcesChanged(previous, settings) {
    return settings.nodeSize !== previous.nodeSize || settings.centerForce !== previous.centerForce ||
        settings.repelForce !== previous.repelForce || settings.linkForce !== previous.linkForce ||
        settings.linkDistance !== previous.linkDistance;
}

function optionNumber(value, fallback, min, max) {
    return typeof value === "number" && Number.isFinite(value) ? clamp(value, min, max) : fallback;
}

function measureTitleText(ctx, text) {
    return { text, width: ctx.measureText(text).width };
}

function fitTitleText(ctx, text, limit) {
    const characters = Array.from(text);
    let low = 0, high = characters.length;
    while (low < high) {
        const middle = Math.ceil((low + high) / 2);
        if (ctx.measureText(characters.slice(0, middle).join("")).width <= limit) low = middle;
        else high = middle - 1;
    }
    return measureTitleText(ctx, characters.slice(0, low).join(""));
}

function pinch(pointers) {
    const [a, b] = pointers.values();
    return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2, span: Math.max(1, Math.hypot(a.x - b.x, a.y - b.y)) };
}

function blendCamera(from, to, progress) {
    return {
        x: from.x + (to.x - from.x) * progress,
        y: from.y + (to.y - from.y) * progress,
        scale: from.scale * (to.scale / from.scale) ** progress
    };
}

function approach(value, target, step) {
    return value < target ? Math.min(target, value + step) : Math.max(target, value - step);
}

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

function invoke(dotnet, method, ...args) {
    dotnet.invokeMethodAsync(method, ...args).catch(() => {});
}
