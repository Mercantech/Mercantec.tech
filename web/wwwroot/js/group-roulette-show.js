// Classroom presentation for LUP. Inspired by Mercantech/GruppeRoulette.
// Allocation and persistence stay in the Blazor component and authenticated APIs.
const sessions = new WeakMap();
const palette = ["#ffd561", "#ff7bb7", "#89dccb", "#bba4ff", "#ffab78", "#9dcaff"];

export function initialize(root, callback) {
  if (sessions.has(root)) return;
  const state = { callback, controller: new AbortController(), spinning: false, timers: new Set(), cancelSpin: null, audio: null, sound: false, frame: 0, overviewFocus: null, oldOverflow: "", suppressClick: 0 };
  sessions.set(root, state);
  const options = { signal: state.controller.signal };
  const lever = root.querySelector(".roulette-lever");
  // The page may still be waiting for authentication. Bind the eventual lever
  // by delegating on the persistent root rather than a transient button.
  let gesture = null;
  root.addEventListener("pointerdown", event => {
    const target = event.target.closest(".roulette-lever");
    if (!target || target.disabled || state.spinning) return;
    gesture = { target, pointerId: event.pointerId, y: event.clientY, pull: 0 };
    try { target.setPointerCapture(event.pointerId); } catch { }
  }, options);
  root.addEventListener("pointermove", event => {
    if (!gesture || gesture.pointerId !== event.pointerId) return;
    gesture.pull = Math.max(0, Math.min(65, event.clientY - gesture.y));
    gesture.target.style.setProperty("--pull", `${gesture.pull}px`);
    gesture.target.style.setProperty("--lever-angle", `${-11 + gesture.pull * .35}deg`);
    if (gesture.pull > 6) event.preventDefault();
  }, options);
  const release = (event, cancelled = false) => {
    if (!gesture || gesture.pointerId !== event.pointerId) return;
    const released = gesture; gesture = null;
    released.target.style.setProperty("--pull", "0px");
    released.target.style.setProperty("--lever-angle", "-11deg");
    if (!cancelled && released.pull > 23) {
      state.suppressClick = performance.now() + 600;
      state.callback.invokeMethodAsync("SpinFromLever").catch(() => {});
    }
  };
  root.addEventListener("pointerup", event => release(event), options);
  root.addEventListener("pointercancel", event => release(event, true), options);
  root.addEventListener("click", event => {
    if (event.target.closest(".roulette-lever") && performance.now() < state.suppressClick) {
      event.preventDefault(); event.stopImmediatePropagation();
    }
  }, { ...options, capture: true });
  document.addEventListener("keydown", event => {
    if (!root.isConnected || event.defaultPrevented) return;
    if (event.key === "Escape" && root.querySelector(".roulette-overview")) {
      event.preventDefault(); state.callback.invokeMethodAsync("CloseOverview").catch(() => {}); return;
    }
    if (event.code !== "Space" || root.querySelector(".roulette-overview") || root.querySelector(".roulette-copy-fallback")) return;
    if (event.target.closest?.("input,textarea,select,button,a,[contenteditable=true],summary")) return;
    const button = root.querySelector(".roulette-spin");
    if (button && !button.disabled) { event.preventDefault(); button.click(); }
  }, options);
}

function reducedMotion() { return window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true; }
function schedule(state, action, delay) {
  const timer = setTimeout(() => { state.timers.delete(timer); action(); }, delay);
  state.timers.add(timer); return timer;
}
function fitName(node) {
  const width = Math.max(40, node.parentElement.clientWidth - 12);
  const length = Math.max(1, node.textContent.length);
  const max = window.innerWidth < 540 ? 25 : 41;
  node.style.setProperty("--fit-font", `${Math.max(13, Math.min(max, width * 1.85 / (length * .62)))}px`);
}

export async function spin(root, names, finalNames, sound) {
  const state = sessions.get(root);
  if (!state || state.spinning || !root.isConnected) return false;
  state.spinning = true; state.sound = sound;
  const reels = [...root.querySelectorAll(".roulette-reel")];
  const targets = finalNames.slice(0, reels.length);
  if (!targets.length) { state.spinning = false; return false; }
  if (sound) await ensureAudio(state);
  tone(state, 125, .12, "sawtooth", .018);
  const motion = !reducedMotion();
  let settled = false;
  return await new Promise(resolve => {
    const finish = completed => {
      if (settled) return;
      settled = true; state.cancelSpin = null; state.spinning = false;
      for (const timer of state.timers) clearTimeout(timer);
      state.timers.clear();
      if (completed && root.isConnected) {
        if (motion) celebrate(root, state);
        [523.25, 659.25, 783.99, 1046.5].forEach((frequency, index) => tone(state, frequency, .35, "triangle", .024, index * .105));
      }
      resolve(completed);
    };
    state.cancelSpin = () => finish(false);
    let remaining = targets.length;
    reels.forEach((reel, index) => {
      const node = reel.querySelector(".roulette-reel-name");
      reel.classList.remove("is-locked");
      if (!node || index >= targets.length) return;
      let active = true, tickNumber = 0;
      const cycle = () => {
        if (!active || settled || !root.isConnected) return;
        node.textContent = names.length ? names[Math.floor(Math.random() * names.length)] : "…";
        fitName(node);
        if (++tickNumber % 5 === 0) tone(state, 190 + index * 35, .025, "square", .006);
        schedule(state, cycle, 45 + Math.min(65, tickNumber));
      };
      if (motion) cycle();
      schedule(state, () => {
        active = false; node.textContent = targets[index]; fitName(node); reel.classList.add("is-locked");
        tone(state, 310 + index * 115, .14, "triangle", .03);
        if (--remaining === 0) schedule(state, () => finish(true), motion ? 220 : 0);
      }, motion ? 1250 + index * 360 : 0);
    });
  });
}

async function ensureAudio(state) {
  if (!state.sound) return;
  try {
    const Audio = window.AudioContext || window.webkitAudioContext;
    if (!Audio) return;
    state.audio ||= new Audio();
    if (state.audio.state === "suspended") await state.audio.resume();
  } catch { state.sound = false; }
}
function tone(state, frequency, duration, type, volume, offset = 0) {
  if (!state.sound || !state.audio || state.audio.state !== "running") return;
  try {
    const start = state.audio.currentTime + offset;
    const oscillator = state.audio.createOscillator();
    const envelope = state.audio.createGain();
    oscillator.type = type; oscillator.frequency.value = frequency;
    envelope.gain.setValueAtTime(volume, start); envelope.gain.exponentialRampToValueAtTime(.0001, start + duration);
    oscillator.connect(envelope); envelope.connect(state.audio.destination);
    oscillator.onended = () => { oscillator.disconnect(); envelope.disconnect(); };
    oscillator.start(start); oscillator.stop(start + duration);
  } catch { }
}
export async function setSound(root, enabled) {
  const state = sessions.get(root); if (!state) return;
  state.sound = enabled;
  if (enabled) { await ensureAudio(state); tone(state, 660, .13, "sine", .02); }
  else if (state.audio?.state === "running") await state.audio.suspend();
}

function celebrate(root, state) {
  if (reducedMotion()) return;
  const canvas = root.querySelector(".roulette-confetti"); if (!canvas) return;
  const context = canvas.getContext("2d"); if (!context) return;
  cancelAnimationFrame(state.frame);
  const width = window.innerWidth, height = window.innerHeight;
  const ratio = Math.min(2, window.devicePixelRatio || 1);
  canvas.width = width * ratio; canvas.height = height * ratio;
  context.setTransform(ratio, 0, 0, ratio, 0, 0);
  const particles = Array.from({ length: 125 }, () => ({
    x: width * .44, y: height * .45, vx: (Math.random() - .5) * 16, vy: -5 - Math.random() * 12,
    rotation: Math.random() * Math.PI, spin: (Math.random() - .5) * .3, size: 4 + Math.random() * 7,
    color: palette[Math.floor(Math.random() * palette.length)]
  }));
  const start = performance.now(); let last = start;
  const draw = now => {
    if (!root.isConnected) return;
    const age = (now - start) / 2350, delta = Math.min(2, (now - last) / 16.667); last = now;
    context.clearRect(0, 0, width, height);
    for (const particle of particles) {
      particle.x += particle.vx * delta; particle.y += particle.vy * delta; particle.vy += .24 * delta; particle.rotation += particle.spin * delta;
      context.save(); context.globalAlpha = Math.max(0, 1 - age); context.translate(particle.x, particle.y); context.rotate(particle.rotation);
      context.fillStyle = particle.color; context.fillRect(-particle.size / 2, -particle.size / 3, particle.size, particle.size * .62); context.restore();
    }
    if (age < 1) state.frame = requestAnimationFrame(draw);
    else context.clearRect(0, 0, width, height);
  };
  state.frame = requestAnimationFrame(draw);
}

export async function setOverview(root, open) {
  const state = sessions.get(root); if (!state) return;
  const shell = root.querySelector(".roulette-shell");
  if (open) {
    state.overviewFocus = document.activeElement; state.oldOverflow = document.body.style.overflow;
    if (shell) shell.inert = true;
    document.body.style.overflow = "hidden";
    await new Promise(requestAnimationFrame);
    root.querySelector(".roulette-overview-close")?.focus();
  } else {
    if (shell) shell.inert = false;
    document.body.style.overflow = state.oldOverflow;
    if (state.overviewFocus?.isConnected) state.overviewFocus.focus();
    state.overviewFocus = null;
  }
}
export async function toggleFullscreen(root) {
  if (document.fullscreenElement === root) await document.exitFullscreen();
  else if (root.requestFullscreen) await root.requestFullscreen();
  else throw new Error("Fullscreen is not supported");
}
export async function copy(text) { try { await navigator.clipboard.writeText(text); return true; } catch { return false; } }
export function loadSession(key) { try { return localStorage.getItem(key); } catch { return null; } }
export function saveSession(key, value) { try { localStorage.setItem(key, value); } catch { } }
export function closeOrReturn(url) {
  // The primary classroom action stays in the authenticated tab. Only a window
  // actually opened by another window is a candidate for closing.
  if (window.opener && !window.opener.closed) {
    try { window.close(); } catch { }
    setTimeout(() => { if (!window.closed) window.location.assign(url); }, 120);
  } else window.location.assign(url);
}
export function cancel(root) {
  const state = sessions.get(root); if (!state) return;
  state.cancelSpin?.();
  for (const timer of state.timers) clearTimeout(timer); state.timers.clear();
  cancelAnimationFrame(state.frame);
  const canvas = root.querySelector(".roulette-confetti"); canvas?.getContext("2d")?.clearRect(0, 0, canvas.width, canvas.height);
  const shell = root.querySelector(".roulette-shell"); if (shell) shell.inert = false;
  if (state.overviewFocus) document.body.style.overflow = state.oldOverflow;
  state.overviewFocus = null;
}
export async function dispose(root) {
  const state = sessions.get(root); if (!state) return;
  cancel(root); state.controller.abort();
  try { await state.audio?.close(); } catch { }
  sessions.delete(root);
}
