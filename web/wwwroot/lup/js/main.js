import { forlob } from "./forlob.js";
import { praktik, praktikHtml } from "./praktik.js";
import { tidslinje } from "./tidslinje.js";

const byShort = Object.fromEntries(forlob.map((f) => [f.short.toLowerCase(), f]));

const homeView = document.getElementById("home-view");
const detailView = document.getElementById("detail-view");
const praktikView = document.getElementById("praktik-view");
const detailCode = document.getElementById("detail-code");
const detailTitle = document.getElementById("detail-title");
const detailLead = document.getElementById("detail-lead");
const detailBody = document.getElementById("detail-body");
const praktikTitle = document.getElementById("praktik-title");
const praktikLead = document.getElementById("praktik-lead");
const praktikBody = document.getElementById("praktik-body");
const tidslinjeTrack = document.getElementById("tidslinje-track");
const tidslinjeRail = document.getElementById("tidslinje-rail");
const tlRailUp = document.getElementById("tl-rail-up");
const tlRailDown = document.getElementById("tl-rail-down");
const navLinks = document.querySelectorAll("[data-nav]");

let railActiveId = "";
let railRaf = 0;
let tidslinjeBuilt = false;
let railScrolling = false;

const prefersReducedMotion = () =>
  window.matchMedia("(prefers-reduced-motion: reduce)").matches;

const isTimelineOpen = () => !homeView.hidden;

const railIndex = () => {
  const idx = tidslinje.findIndex((s) => s.id === railActiveId);
  return idx < 0 ? 0 : idx;
};

const escapeHtml = (value) =>
  String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");

const lupBrandPlain = "Lokal Undervisningsplan";
const lupBrandHtml = `<span class="brand-letter">L</span>okal <span class="brand-letter">U</span>ndervisnings<span class="brand-letter">P</span>lan`;

const lupTitlePlain = (titel) => `${titel} — ${lupBrandPlain}`;
const lupTitleHtml = (titel) => `${escapeHtml(titel)} — ${lupBrandHtml}`;

const inlineMarkdown = (text) =>
  escapeHtml(text).replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");

const renderMarkdown = (md) => {
  const lines = md.replace(/\r\n/g, "\n").split("\n");
  const html = [];
  let inList = false;

  const closeList = () => {
    if (inList) {
      html.push("</ul>");
      inList = false;
    }
  };

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      closeList();
      continue;
    }
    if (trimmed.startsWith("## ")) {
      closeList();
      html.push(`<h2>${inlineMarkdown(trimmed.slice(3))}</h2>`);
      continue;
    }
    if (trimmed.startsWith("- ")) {
      if (!inList) {
        html.push("<ul>");
        inList = true;
      }
      html.push(`<li>${inlineMarkdown(trimmed.slice(2))}</li>`);
      continue;
    }
    closeList();
    html.push(`<p>${inlineMarkdown(trimmed)}</p>`);
  }
  closeList();
  return html.join("\n");
};

/** § 54: tre hoveddele — mål, indhold, evaluering. */
const LUP_PILLARS = [
  {
    id: "maal",
    num: 1,
    title: "Mål for undervisningen",
    blocks: new Set(["Mål for undervisningen", "Forventningsniveau"]),
  },
  {
    id: "indhold",
    num: 2,
    title: "Indhold i undervisningen",
    blocks: new Set([
      "Indhold i undervisningen",
      "Helhedsorientering",
      "Praksisrelation",
      "Tværfaglighed",
      "Differentiering",
    ]),
  },
  {
    id: "eval",
    num: 3,
    title: "Evaluering og bedømmelse",
    blocks: new Set([
      "Evaluering og bedømmelse",
      "Løbende evaluering",
      "Bedømmelsesgrundlag",
      "Bedømmelseskriterier",
      "Afsluttende overhøring",
    ]),
  },
];

const LUP_INDHOLD_SUB = new Set([
  "Helhedsorientering",
  "Praksisrelation",
  "Tværfaglighed",
  "Differentiering",
]);

const LUP_EVAL_SUB = new Set([
  "Løbende evaluering",
  "Bedømmelsesgrundlag",
  "Bedømmelseskriterier",
  "Afsluttende overhøring",
]);

const parseLupBlocks = (md) => {
  const lines = md.replace(/\r\n/g, "\n").split("\n");
  const blocks = [];
  let current = null;

  for (const line of lines) {
    if (line.startsWith("## ")) {
      if (current) blocks.push(current);
      current = { title: line.slice(3).trim(), lines: [] };
    } else if (current) {
      current.lines.push(line);
    }
  }
  if (current) blocks.push(current);
  return blocks;
};

const renderBlockBody = (lines) => {
  const html = [];
  let inList = false;

  const closeList = () => {
    if (inList) {
      html.push("</ul>");
      inList = false;
    }
  };

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      closeList();
      continue;
    }
    if (trimmed.startsWith("- ")) {
      if (!inList) {
        html.push("<ul>");
        inList = true;
      }
      html.push(`<li>${inlineMarkdown(trimmed.slice(2))}</li>`);
      continue;
    }
    closeList();
    html.push(`<p>${inlineMarkdown(trimmed)}</p>`);
  }
  closeList();
  return html.join("\n");
};

const renderLupBlock = (block, { sub = false } = {}) => {
  const isPrimary =
    block.title === "Mål for undervisningen" ||
    block.title === "Indhold i undervisningen" ||
    block.title === "Evaluering og bedømmelse";
  const parts = [];
  if (!isPrimary) {
    const tag = sub ? "h3" : "h2";
    parts.push(
      `<${tag} class="${sub ? "lup-subhead" : ""}">${inlineMarkdown(block.title)}</${tag}>`
    );
  }
  parts.push(renderBlockBody(block.lines));
  return parts.join("\n");
};

const renderLupMarkdown = (md) => {
  const blocks = parseLupBlocks(md);
  const meta = blocks.filter((b) => b.title === "Varighed");
  const pillarBlocks = LUP_PILLARS.map((pillar) => ({
    pillar,
    blocks: blocks.filter((b) => pillar.blocks.has(b.title)),
  }));

  const metaHtml = meta.length
    ? `<div class="lup-meta">${meta.map((b) => renderLupBlock(b)).join("")}</div>`
    : "";

  const pillarsHtml = pillarBlocks
    .map(({ pillar, blocks: group }) => {
      if (!group.length) return "";
      const body = group
        .map((b) =>
          renderLupBlock(b, {
            sub:
              LUP_INDHOLD_SUB.has(b.title) ||
              LUP_EVAL_SUB.has(b.title) ||
              b.title === "Forventningsniveau",
          })
        )
        .join("");
      return `
        <section class="lup-pillar lup-pillar--${pillar.id}" id="lup-${pillar.id}" aria-labelledby="lup-${pillar.id}-title">
          <header class="lup-pillar-head">
            <h2 class="lup-pillar-title" id="lup-${pillar.id}-title">${escapeHtml(pillar.title)}</h2>
          </header>
          <div class="lup-pillar-body">${body}</div>
        </section>`;
    })
    .join("");

  return `${metaHtml}${pillarsHtml}`;
};

const routeParts = () => {
  const raw = (location.hash || "#/").replace(/^#\/?/, "").toLowerCase();
  const [key = "home", stop = ""] = raw.split(/[/?#]/);
  return { key: key || "home", stop };
};

const hideAll = () => {
  homeView.hidden = true;
  detailView.hidden = true;
  praktikView.hidden = true;
  document.body.classList.remove("has-tl-rail");
};

const setActiveNav = (key) => {
  navLinks.forEach((link) => {
    link.classList.toggle("is-active", link.dataset.nav === key);
  });
};

const updateRailArrows = () => {
  const idx = railIndex();
  if (tlRailUp) tlRailUp.disabled = idx <= 0;
  if (tlRailDown) tlRailDown.disabled = idx >= tidslinje.length - 1;
};

const scrollToStop = (stopId = "") => {
  const behavior = prefersReducedMotion() ? "auto" : "smooth";
  railScrolling = true;

  if (!stopId) {
    window.scrollTo({ top: 0, behavior });
  } else {
    const target = document.getElementById(`stop-${stopId}`);
    if (target) target.scrollIntoView({ behavior, block: "start" });
    else window.scrollTo({ top: 0, behavior });
  }

  window.setTimeout(
    () => {
      railScrolling = false;
      syncRail();
    },
    prefersReducedMotion() ? 50 : 450
  );
};

const goToStop = (stopId) => {
  if (!isTimelineOpen()) {
    location.hash = stopId ? `#/tidslinje/${stopId}` : "#/";
    return;
  }
  const nextHash = stopId ? `#/tidslinje/${stopId}` : "#/";
  if (location.hash !== nextHash) {
    history.replaceState(null, "", nextHash);
  }
  scrollToStop(stopId);

  railActiveId = stopId || tidslinje[0]?.id || "";
  tidslinjeRail?.querySelectorAll("[data-stop]").forEach((link) => {
    const on = link.dataset.stop === railActiveId;
    link.classList.toggle("is-active", on);
    if (on) link.setAttribute("aria-current", "true");
    else link.removeAttribute("aria-current");
  });
  updateRailArrows();
};

const goRailStep = (delta) => {
  if (!isTimelineOpen()) return;
  const next = tidslinje[railIndex() + delta];
  if (!next) return;
  goToStop(next.id);
};

const renderStop = (stop, index) => {
  const typeLabel = stop.type === "praktik" ? "Praktik" : "Skole";
  const lup = stop.type === "skole" ? byShort[stop.id] || byShort[stop.label?.toLowerCase()] : null;

  const titlePlain = lup ? lupTitlePlain(lup.titel) : stop.title;
  const titleHtml = lup ? lupTitleHtml(lup.titel) : escapeHtml(stop.title);
  const summary = lup?.kort || stop.summary;
  const duration =
    lup?.uger != null
      ? `<span class="tl-duration">${lup.uger} uger</span>`
      : "";

  let panel;
  if (stop.type === "praktik") {
    panel = `<div class="tl-panel">${stop.body}</div>`;
  } else {
    const image = stop.image
      ? `<figure class="praktik-fig"><img src="assets/praktik/${escapeHtml(
          stop.image
        )}" alt="${escapeHtml(titlePlain)}" loading="lazy" /></figure>`
      : "";
    const body = lup
      ? `<div class="prose tl-lup lup-doc">${renderLupMarkdown(lup.markdown)}</div>`
      : `<p class="tl-summary">${escapeHtml(stop.summary)}</p>`;
    panel = `<div class="tl-panel">${image}${body}</div>`;
  }

  return `
    <li class="tl-item tl-${stop.type}" id="stop-${escapeHtml(stop.id)}" style="--i:${index}">
      <header class="tl-node">
        <span class="tl-dot" aria-hidden="true"></span>
        <span class="tl-meta">
          <span class="tl-kind">${typeLabel}</span>
          <span class="tl-label">${escapeHtml(stop.label)}</span>
          ${duration}
        </span>
        <span class="tl-copy">
          <h2 class="tl-title">${titleHtml}</h2>
          <p class="tl-summary">${escapeHtml(summary)}</p>
        </span>
      </header>
      ${panel}
    </li>`;
};

const renderRail = () =>
  tidslinje
    .map((stop) => {
      const kind = stop.type === "praktik" ? "Praktik" : "Skole";
      return `
        <a
          class="tl-rail-link tl-rail-${stop.type}"
          href="#/tidslinje/${escapeHtml(stop.id)}"
          data-stop="${escapeHtml(stop.id)}"
          title="${escapeHtml(kind)} · ${escapeHtml(stop.label)}"
        >
          <span class="tl-rail-dot" aria-hidden="true"></span>
          <span class="tl-rail-text">
            <span class="tl-rail-kind">${kind}</span>
            <span class="tl-rail-label">${escapeHtml(stop.label)}</span>
          </span>
        </a>`;
    })
    .join("");

const syncRail = () => {
  if (!isTimelineOpen() || !tidslinjeRail || railScrolling) return;

  const marker = window.scrollY + Math.min(180, window.innerHeight * 0.22);
  let active = tidslinje[0]?.id || "";

  for (const stop of tidslinje) {
    const el = document.getElementById(`stop-${stop.id}`);
    if (!el) continue;
    const top = el.getBoundingClientRect().top + window.scrollY;
    if (top <= marker) active = stop.id;
  }

  if (active !== railActiveId) {
    railActiveId = active;

    tidslinjeRail.querySelectorAll("[data-stop]").forEach((link) => {
      const on = link.dataset.stop === active;
      link.classList.toggle("is-active", on);
      if (on) link.setAttribute("aria-current", "true");
      else link.removeAttribute("aria-current");
    });

    const activeLink = tidslinjeRail.querySelector(`[data-stop="${active}"]`);
    activeLink?.scrollIntoView({ block: "nearest", inline: "nearest", behavior: "smooth" });
  }

  updateRailArrows();
};

const onRailScroll = () => {
  if (railRaf) return;
  railRaf = requestAnimationFrame(() => {
    railRaf = 0;
    syncRail();
  });
};

const showTimeline = (stopId = "") => {
  const alreadyOpen = isTimelineOpen();

  detailView.hidden = true;
  praktikView.hidden = true;
  homeView.hidden = false;
  document.body.classList.add("has-tl-rail");
  document.title = "LUP · Lokale undervisningsplaner";
  setActiveNav("home");

  if (!tidslinjeBuilt) {
    tidslinjeTrack.innerHTML = tidslinje.map((s, i) => renderStop(s, i)).join("");
    tidslinjeRail.innerHTML = renderRail();
    tidslinjeBuilt = true;
    railActiveId = "";
    requestAnimationFrame(() => tidslinjeTrack.classList.add("is-settled"));
  }

  if (alreadyOpen) {
    scrollToStop(stopId);
    return;
  }

  requestAnimationFrame(() => {
    scrollToStop(stopId);
    syncRail();
  });
};

const showForlob = (key) => {
  const item = byShort[key];
  if (!item) {
    showTimeline();
    return;
  }

  hideAll();
  detailView.hidden = false;
  detailCode.textContent = item.uger != null ? `${item.short} · ${item.uger} uger` : item.short;
  detailTitle.innerHTML = lupTitleHtml(item.titel);
  detailLead.textContent = item.kort;
  detailBody.classList.add("lup-doc");
  detailBody.innerHTML = renderLupMarkdown(item.markdown);
  document.title = `${item.short} · LUP`;
  setActiveNav(key);
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const showPraktik = () => {
  hideAll();
  praktikView.hidden = false;
  praktikTitle.textContent = praktik.title;
  praktikLead.textContent = praktik.lead;
  praktikBody.innerHTML = praktikHtml;
  document.title = "Praktikmålsoversigt · LUP";
  setActiveNav("praktik");
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const render = () => {
  const { key, stop } = routeParts();
  if (key === "home" || key === "" || key === "tidslinje") {
    showTimeline(key === "tidslinje" ? stop : "");
    return;
  }
  if (key === "praktik") {
    showPraktik();
    return;
  }
  showForlob(key);
};

window.addEventListener("hashchange", render);
window.addEventListener("scroll", onRailScroll, { passive: true });
window.addEventListener("resize", onRailScroll, { passive: true });

tlRailUp?.addEventListener("click", () => goRailStep(-1));
tlRailDown?.addEventListener("click", () => goRailStep(1));

tidslinjeRail?.addEventListener("click", (event) => {
  const link = event.target.closest("[data-stop]");
  if (!link || !isTimelineOpen()) return;
  event.preventDefault();
  goToStop(link.dataset.stop);
});

window.addEventListener("keydown", (event) => {
  if (!isTimelineOpen()) return;
  if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey) return;
  const tag = event.target?.tagName;
  if (tag === "INPUT" || tag === "TEXTAREA" || event.target?.isContentEditable) return;
  if (event.key === "ArrowUp") {
    event.preventDefault();
    goRailStep(-1);
  } else if (event.key === "ArrowDown") {
    event.preventDefault();
    goRailStep(1);
  }
});

const THEMES = new Set(["system", "light", "dark", "christmas", "hacker"]);
const themeSelect = document.getElementById("theme-select");

const signedIn = () => {
  try {
    return !!sessionStorage.getItem("mt_access_token");
  } catch (_) {
    return false;
  }
};

const storedTheme = () => {
  try {
    const stored = localStorage.getItem("lup-theme");
    if (stored) return stored;
    if (signedIn() && localStorage.getItem("lup-dk-theme") === "dark") return "dark";
  } catch (_) {}
  return null;
};

/** Samme nøgle som forsiden. Uden login er standarden lys, også når valget er System. */
const currentTheme = () => {
  const stored = storedTheme();
  if (!signedIn() && (!stored || stored === "system")) return "light";
  return THEMES.has(stored) ? stored : "light";
};

const applyTheme = (theme, persist = true) => {
  const next = THEMES.has(theme) ? theme : "light";
  if (persist) {
    try {
      localStorage.setItem("lup-theme", next);
    } catch (_) {}
  }
  const chosen = !signedIn() && next === "system" ? "light" : next;
  const osDark = matchMedia("(prefers-color-scheme: dark)").matches;
  const resolved = chosen === "system" ? (osDark ? "dark" : "light") : chosen;
  const dark = resolved === "dark" || resolved === "christmas" || resolved === "hacker";
  document.documentElement.dataset.theme = resolved;
  document.documentElement.style.colorScheme = dark ? "dark" : "light";
  if (themeSelect) themeSelect.value = chosen;
};

applyTheme(currentTheme(), false);

themeSelect?.addEventListener("change", () => {
  applyTheme(themeSelect.value);
});

matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => {
  if (signedIn() && storedTheme() === "system") applyTheme("system", false);
});

if (!location.hash || location.hash === "#") {
  location.replace("#/");
}

render();
