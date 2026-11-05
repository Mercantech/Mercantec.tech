(() => {
    /** Pinned ESM — lazy-loades kun når kursussider har Mermaid-fences. */
    const MERMAID_URL = "https://cdn.jsdelivr.net/npm/mermaid@11.6.0/dist/mermaid.esm.min.mjs";
    let mermaidPromise = null;
    let seq = 0;

    function loadMermaid() {
        if (!mermaidPromise) {
            mermaidPromise = import(MERMAID_URL).then((mod) => {
                const mermaid = mod.default;
                mermaid.initialize({
                    startOnLoad: false,
                    securityLevel: "strict",
                    theme: "neutral",
                    flowchart: { useMaxWidth: true, htmlLabels: false },
                    sequence: { useMaxWidth: true },
                    suppressErrorRendering: true
                });
                return mermaid;
            });
        }
        return mermaidPromise;
    }

    function sanitizeSource(code) {
        return String(code ?? "").replace(/\u00A0/g, " ").trim();
    }

    function repairAccidentalProseAfterEdgeTarget(line) {
        const arrow = line.lastIndexOf("-->");
        if (arrow === -1) return line;
        const head = line.slice(0, arrow + 3);
        const tail = line.slice(arrow + 3);
        const targetMatch = tail.match(
            /^(\s*(?:[\w-]+|\[[^\]]*\]|\{[^}]*\}|\([^)]*\)))(?:\s+(.*))?$/
        );
        if (!targetMatch?.[2]) return line;
        const rest = targetMatch[2].trim();
        if (!rest || rest.startsWith("%%")) return line;
        if (/^(style|class|classDef|subgraph|end|click|linkStyle|direction)\b/i.test(rest)) return line;
        return head + targetMatch[1];
    }

    function prepareMermaidSource(raw) {
        const normalized = sanitizeSource(raw);
        if (!normalized) return { source: "", repaired: false };
        let repaired = false;
        const out = normalized.split("\n").map((line) => {
            const fixed = repairAccidentalProseAfterEdgeTarget(line);
            if (fixed !== line) repaired = true;
            return fixed;
        });
        return { source: out.join("\n"), repaired };
    }

    function formatMermaidRenderError(err) {
        const base = "Diagrammet kunne ikke tegnes.";
        const message = typeof err?.message === "string" ? err.message.trim() : "";
        if (!message) return `${base} Ret Mermaid-koden, og prøv igen.`;
        const lineNo = err?.hash?.loc?.first_line ?? (typeof err?.hash?.line === "number" ? err.hash.line + 1 : undefined);
        const snippet = err?.hash?.text ? ` «${err.hash.text}»` : "";
        const lineHint = lineNo ? ` (linje ${lineNo}${snippet})` : snippet ? ` (${snippet.trim()})` : "";
        if (/^Parse error/i.test(message)) {
            return `${base} ${message.split("\n")[0]}${lineHint}.`;
        }
        return `${base} ${message.split("\n")[0]}`;
    }

    function showError(host, code, message) {
        host.replaceChildren();
        host.classList.add("course-mermaid-error");
        host.dataset.mermaidDone = "error";
        if (code) {
            const pre = document.createElement("pre");
            pre.className = "course-mermaid-fallback";
            pre.textContent = code;
            host.appendChild(pre);
        }
        const msg = document.createElement("span");
        msg.className = "course-mermaid-error-msg";
        msg.textContent = message;
        host.appendChild(msg);
    }

    async function drawInto(host, code) {
        const prepared = prepareMermaidSource(code);
        const source = prepared.source;
        if (!source) {
            host.replaceChildren();
            host.dataset.mermaidDone = "empty";
            return false;
        }

        if (host.dataset.mermaidDone === "1" && host.dataset.mermaidSource === source && host.querySelector("svg"))
            return true;

        let mermaid;
        try {
            mermaid = await loadMermaid();
        } catch {
            showError(host, source, "Kunne ikke indlæse Mermaid-biblioteket.");
            return false;
        }

        const id = `course-mmd-${Date.now()}-${++seq}`;
        try {
            const { svg } = await mermaid.render(id, source);
            const wrap = document.createElement("div");
            wrap.className = "course-mermaid";
            wrap.setAttribute("role", "img");
            wrap.setAttribute("aria-label", "Diagram");
            wrap.innerHTML = svg;
            wrap.querySelectorAll("script").forEach((s) => s.remove());
            host.replaceChildren(wrap);
            host.classList.remove("course-mermaid-error");
            host.dataset.mermaidDone = "1";
            host.dataset.mermaidSource = source;
            return true;
        } catch (err) {
            showError(host, source, formatMermaidRenderError(err));
            return false;
        }
    }

    window.courseMermaid = {
        /**
         * Tegn én Mermaid-definition i en Blazor-host (tom div).
         * Host-elementet ejes af Blazor; SVG injiceres her uden at røre øvrige kodeblokke.
         */
        async renderDefinition(host, code) {
            if (!host) return false;
            return drawInto(host, code);
        },

        /**
         * Render legacy Markdig Diagrams-output (`pre.mermaid`) inde i root.
         * Bruges ikke af CourseMarkdownBody-segmenter, men er ufarlig at beholde.
         */
        async render(rootOrSelector) {
            const root = !rootOrSelector
                ? document
                : typeof rootOrSelector === "string"
                    ? document.querySelector(rootOrSelector) || document
                    : rootOrSelector;
            const nodes = Array.from(root.querySelectorAll("pre.mermaid:not([data-mermaid-done])"));
            let n = 0;
            for (const el of nodes) {
                const host = document.createElement("div");
                host.className = "course-mermaid-host";
                el.replaceWith(host);
                if (await drawInto(host, el.textContent)) n++;
            }
            return n;
        }
    };
})();
