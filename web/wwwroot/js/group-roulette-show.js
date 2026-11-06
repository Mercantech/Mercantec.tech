// Classroom presentation for LUP. Inspired by Mercantech/GruppeRoulette.
// Allocation and persistence stay in the Blazor component and authenticated APIs.
const sessions = new WeakMap();
const palette = ["#ffd561", "#ff7bb7", "#89dccb", "#bba4ff", "#ffab78", "#9dcaff"];

export function initialize(root, callback, settings = {}) {
  if (sessions.has(root)) return;
  const state = { callback, controller: new AbortController(), spinning: false, timers: new Set(), cancelSpin: null,
    audio: null, resuming: null, sound: settings?.sound === true, volume: volume(settings?.volume, .75), effects: new Set(),
    speech: null, browserSpeech: null, speechVersion: 0, preparedSpeech: null, strips: new Set(), spinFrame: 0, frame: 0,
    celebration: null, celebrationTimer: 0, overviewFocus: null, oldOverflow: "", suppressClick: 0 };
  sessions.set(root, state);
  const options = { signal: state.controller.signal };
  // Unlock the audio context while the browser still has the actual user gesture.
  // Waiting for a Blazor event and then JS interop loses that activation in some browsers.
  const unlockFromGesture = event => {
    const control = event.target.closest?.(".roulette-spin,.roulette-lever,[data-roulette-audio],[data-roulette-voice],[data-roulette-auto]");
    if (control && !control.disabled) { unlockAudio(state); try { window.speechSynthesis?.resume(); } catch { } }
  };
  root.addEventListener("pointerdown", unlockFromGesture, { ...options, capture: true });
  root.addEventListener("click", unlockFromGesture, { ...options, capture: true });
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
    if (button && !button.disabled && !state.spinning) { event.preventDefault(); unlockAudio(state); button.click(); }
  }, options);
}

function reducedMotion() { return window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true; }
function schedule(state, action, delay) {
  const timer = setTimeout(() => { state.timers.delete(timer); action(); }, delay);
  state.timers.add(timer); return timer;
}
function fitName(node, viewport) {
  const width = Math.max(40, viewport.clientWidth - 12);
  const length = Math.max(1, node.textContent.length);
  const max = window.innerWidth < 540 ? 25 : 41;
  node.style.setProperty("--fit-font", `${Math.max(13, Math.min(max, width / (length * .62)))}px`);
}

function removeStrips(state) {
  for (const entry of state.strips) {
    entry.strip.remove();
    entry.reel.classList.remove("has-live-reel", "is-rolling", "is-locked");
  }
  state.strips.clear();
}

function createReel(state, reel, pool, target, index, duration) {
  const viewport = reel.querySelector(".roulette-reel-window");
  if (!viewport || !reel.querySelector(".roulette-reel-name")) return null;
  const strip = document.createElement("div");
  strip.className = "roulette-reel-strip";
  strip.setAttribute("aria-hidden", "true");
  const height = Math.max(38, (viewport.clientHeight || 110) * .73);
  const entry = { reel, viewport, strip, rows: [], height, phase: 0, elapsed: 0, locked: false, target,
    stopAt: duration };
  // The caller sets stopAt in visual order below; keeping a five-row strip lets
  // names enter above the payline and leave below it instead of flashing in place.
  for (let row = 0; row < 5; row++) {
    const node = document.createElement("strong");
    node.className = "roulette-reel-item";
    node.style.height = `${height}px`;
    node.style.lineHeight = `${height}px`;
    strip.appendChild(node); entry.rows.push(node);
  }
  const pick = () => pool[Math.floor(Math.random() * pool.length)] ?? "…";
  entry.pick = pick;
  entry.rows.forEach(node => { node.textContent = pick(); fitName(node, viewport); });
  viewport.appendChild(strip);
  reel.classList.remove("is-locked");
  reel.classList.add("has-live-reel", "is-rolling");
  state.strips.add(entry);
  positionReel(entry);
  return entry;
}

function positionReel(entry) {
  const center = (entry.viewport.clientHeight || 110) / 2 - entry.height / 2;
  entry.strip.style.setProperty("--roll-y", `${center - (2 + entry.phase) * entry.height}px`);
}

function advanceReel(entry) {
  for (let row = 0; row < entry.rows.length - 1; row++) entry.rows[row].textContent = entry.rows[row + 1].textContent;
  entry.rows.at(-1).textContent = entry.pick();
  entry.rows.forEach(node => fitName(node, entry.viewport));
}

function lockReel(entry) {
  entry.locked = true; entry.phase = 0;
  entry.rows[2].textContent = entry.target;
  entry.rows.forEach(node => fitName(node, entry.viewport));
  positionReel(entry);
  entry.reel.classList.remove("is-rolling"); entry.reel.classList.add("is-locked");
}

export async function spin(root, names, finalNames, sound, options = {}) {
  const state = sessions.get(root);
  if (!state || state.spinning || !root.isConnected) return false;
  state.spinning = true; state.sound = sound; state.volume = volume(options?.volume, state.volume);
  cancelSpeech(root, false); removeStrips(state); clearCelebration(state);
  const pool = Array.isArray(names) ? names.filter(name => typeof name === "string" && name.length) : [];
  const targets = Array.isArray(finalNames) ? finalNames.filter(name => typeof name === "string") : [];
  if (!targets.length) { state.spinning = false; return false; }
  const duration = Math.min(12000, Math.max(800, Number(options?.durationMs) || 3000));
  const motion = typeof options?.motion === "boolean" ? options.motion : !reducedMotion();
  root.classList.remove(motion ? "roulette-motion-disabled" : "roulette-motion-enabled");
  root.classList.add(motion ? "roulette-motion-enabled" : "roulette-motion-disabled");
  let settled = false;
  return await new Promise(resolve => {
    const finish = completed => {
      if (settled) return;
      settled = true; state.cancelSpin = null; state.spinning = false;
      cancelAnimationFrame(state.spinFrame); state.spinFrame = 0;
      for (const timer of state.timers) clearTimeout(timer);
      state.timers.clear();
      if (!completed) removeStrips(state);
      if (completed && root.isConnected) {
        if (motion) celebrate(root, state, motion);
        showCelebration(root, state, options, motion);
        [523.25, 659.25, 783.99, 1046.5].forEach((frequency, index) => tone(state, frequency, .48, "triangle", .065, index * .11));
      }
      resolve(completed);
    };
    state.cancelSpin = () => finish(false);
    // Cross a frame boundary so a Blazor render which changes three reels to two
    // has reached the DOM. No cached NodeList is used on the next spin.
    state.spinFrame = requestAnimationFrame(() => {
      if (settled || !root.isConnected) return finish(false);
      const reels = [...root.querySelectorAll(".roulette-reel")];
      if (reels.length !== targets.length) return finish(false);
      const entries = reels.map((reel, index) => createReel(state, reel, pool, targets[index], index, duration));
      if (entries.some(entry => !entry)) return finish(false);
      const spacing = Math.min(420, duration / Math.max(4, entries.length + 1));
      entries.forEach((entry, index) => { entry.stopAt = duration - spacing * (entries.length - index - 1); });
      if (!motion) { entries.forEach(lockReel); finish(true); return; }
      const started = performance.now(); let previous = started, lastTick = started;
      ensureAudio(state).then(() => { if (!settled) { tone(state, 120, .18, "sawtooth", .07); tone(state, 70, .22, "square", .045, .12); } });
      const animate = now => {
        if (settled || !root.isConnected) return finish(false);
        const delta = Math.min(80, Math.max(0, now - previous)); previous = now;
        let remaining = 0;
        entries.forEach((entry, index) => {
          if (entry.locked) return;
          entry.elapsed = now - started;
          if (entry.elapsed >= entry.stopAt) {
            lockReel(entry); tone(state, 230 + index * 90, .16, "square", .075); tone(state, 110, .18, "sine", .05); return;
          }
          remaining++;
          const speed = 3 + 18 * Math.pow(1 - entry.elapsed / entry.stopAt, .65);
          entry.phase += delta * speed / 1000;
          while (entry.phase >= 1) { advanceReel(entry); entry.phase--; }
          positionReel(entry);
        });
        if (now - lastTick > 85) { lastTick = now; tone(state, 190 + Math.floor(Math.random() * entries.length) * 25, .035, "square", .022); }
        if (remaining) state.spinFrame = requestAnimationFrame(animate);
        else schedule(state, () => finish(true), 240);
      };
      state.spinFrame = requestAnimationFrame(animate);
    });
  });
}

function volume(value, fallback = .75) { return Number.isFinite(Number(value)) ? Math.min(1, Math.max(0, Number(value))) : fallback; }
function unlockAudio(state) {
  try {
    const Audio = window.AudioContext || window.webkitAudioContext;
    if (!Audio) return false;
    state.audio ||= new Audio();
    if (state.audio.state === "suspended") {
      state.resuming = Promise.resolve(state.audio.resume()).catch(() => {});
    }
    return true;
  } catch { return false; }
}
async function ensureAudio(state) {
  if (!unlockAudio(state)) return false;
  // A browser may leave resume pending if it was not user-initiated. Effects
  // must never block a draw or the next round on an autoplay-policy promise.
  if (state.audio.state !== "running" && state.resuming) {
    let timeout;
    await Promise.race([state.resuming, new Promise(resolve => { timeout = setTimeout(resolve, 250); })]);
    clearTimeout(timeout);
  }
  return state.audio.state === "running";
}
function tone(state, frequency, duration, type, volume, offset = 0) {
  if (!state.sound || !state.audio || state.audio.state !== "running") return;
  try {
    const start = state.audio.currentTime + offset;
    const oscillator = state.audio.createOscillator();
    const envelope = state.audio.createGain();
    oscillator.type = type; oscillator.frequency.value = frequency;
    envelope.gain.setValueAtTime(Math.max(.0001, volume * state.volume), start); envelope.gain.exponentialRampToValueAtTime(.0001, start + duration);
    oscillator.connect(envelope); envelope.connect(state.audio.destination);
    const effect = { oscillator, envelope }; state.effects.add(effect);
    oscillator.onended = () => { state.effects.delete(effect); oscillator.disconnect(); envelope.disconnect(); };
    oscillator.start(start); oscillator.stop(start + duration);
  } catch { }
}
export async function setSound(root, enabled) {
  const state = sessions.get(root); if (!state) return false;
  state.sound = enabled;
  if (enabled) { const ready = await ensureAudio(state); state.sound = ready; if (ready) tone(state, 660, .13, "sine", .06); return ready; }
  else stopEffects(state);
  return true;
}

export function capabilities(root) {
  const state = sessions.get(root);
  return { reducedMotion: reducedMotion(), speechAvailable: !!(window.speechSynthesis && window.SpeechSynthesisUtterance),
    audioAvailable: !!(window.AudioContext || window.webkitAudioContext), audioReady: state?.audio?.state === "running", sound: state?.sound === true };
}

export function setVolume(root, value) {
  const state = sessions.get(root); if (!state) return;
  state.volume = volume(value, state.volume);
  if (state.speech) state.speech.gain.gain.value = state.volume;
  if (state.browserSpeech?.utterance) state.browserSpeech.utterance.volume = state.volume;
}
function stopEffects(state) {
  for (const { oscillator, envelope } of state.effects) {
    try { oscillator.stop(); oscillator.disconnect(); envelope.disconnect(); } catch { }
  }
  state.effects.clear();
}

export async function prepareSpeech(root, base64Wav) {
  const state = sessions.get(root);
  if (!state || !root.isConnected || typeof base64Wav !== "string" || !base64Wav.length || base64Wav.length > 16 * 1024 * 1024) return false;
  if (state.preparedSpeech?.payload === base64Wav) return true;
  const version = state.speechVersion;
  try {
    if (!unlockAudio(state)) return false;
    const raw = atob(base64Wav);
    if (raw.length > 8 * 1024 * 1024) return false;
    const bytes = Uint8Array.from(raw, character => character.charCodeAt(0));
    const buffer = await state.audio.decodeAudioData(bytes.buffer);
    if (!root.isConnected || sessions.get(root) !== state || version !== state.speechVersion) return false;
    state.preparedSpeech = { payload: base64Wav, buffer };
    return true;
  } catch { return false; }
}

export async function playSpeech(root, base64Wav, voiceVolume = .85) {
  const state = sessions.get(root); if (!state || !root.isConnected) return false;
  cancelSpeech(root, false);
  const version = state.speechVersion;
  if (!await prepareSpeech(root, base64Wav) || !await ensureAudio(state)
      || version !== state.speechVersion || sessions.get(root) !== state || !root.isConnected) return false;
  return await new Promise(resolve => {
    const source = state.audio.createBufferSource(), gain = state.audio.createGain();
    const clipVolume = volume(voiceVolume, .85);
    source.buffer = state.preparedSpeech.buffer; gain.gain.value = clipVolume;
    source.connect(gain); gain.connect(state.audio.destination);
    let settled = false;
    const finish = completed => {
      if (settled) return;
      settled = true; clearTimeout(watchdog); source.onended = null;
      try { source.stop(); source.disconnect(); gain.disconnect(); } catch { }
      if (state.speech?.source === source) state.speech = null;
      resolve(completed);
    };
    const watchdog = setTimeout(() => finish(false), Math.min(120000, Math.max(3000, (source.buffer.duration + 2) * 1000)));
    state.speech = { source, gain, volume: clipVolume, finish };
    source.onended = () => finish(true);
    try { source.start(); } catch { finish(false); }
  });
}

export function cancelSpeech(root, discardPrepared = true) {
  const state = sessions.get(root); if (!state) return;
  state.speechVersion++;
  state.speech?.finish(false);
  state.browserSpeech?.finish(false);
  if (discardPrepared) state.preparedSpeech = null;
}

export async function playBrowserSpeech(root, text, voiceVolume = .85) {
  const state = sessions.get(root), engine = window.speechSynthesis, Utterance = window.SpeechSynthesisUtterance;
  if (!state || !root.isConnected || !engine || !Utterance || typeof text !== "string" || !text.trim()) return false;
  cancelSpeech(root, false);
  const version = state.speechVersion;
  // Group names often use CamelCase. Speaking the individual words sounds much
  // closer to the reference than asking a Danish voice to guess a compound.
  const phraseText = text.replace(/([a-zæøå])([A-ZÆØÅ])/g, "$1 $2").replace(/([A-ZÆØÅ]+)([A-ZÆØÅ][a-zæøå])/g, "$1 $2").replace(/\s+/g, " ").trim().slice(0, 500);
  return await new Promise(resolve => {
    let settled = false, utterance = null, watchdog = 0, voicesTimer = 0;
    const finish = completed => {
      if (settled) return;
      settled = true; clearTimeout(watchdog); clearTimeout(voicesTimer);
      engine.removeEventListener?.("voiceschanged", start);
      if (utterance) { utterance.onend = null; utterance.onerror = null; }
      if (!completed && utterance) { try { engine.cancel(); } catch { } }
      if (state.browserSpeech?.finish === finish) state.browserSpeech = null;
      resolve(completed);
    };
    const start = () => {
      if (settled || utterance) return;
      if (version !== state.speechVersion || !root.isConnected || sessions.get(root) !== state) return finish(false);
      let voices;
      try { voices = engine.getVoices(); } catch { return finish(false); }
      if (!voices.length) return; // Browser voices may arrive in a later voiceschanged event.
      // Never choose an online/browser-cloud voice: Røst and device voices are
      // the two supported paths, and neither sends this text to a new service.
      const local = voices.filter(voice => voice.localService === true);
      const voice = local.find(voice => /^da(?:-|$)/i.test(voice.lang)) || local[0];
      if (!voice) return finish(false);
      clearTimeout(voicesTimer); engine.removeEventListener?.("voiceschanged", start);
      utterance = new Utterance(phraseText); utterance.voice = voice; utterance.lang = voice.lang || "da-DK";
      utterance.rate = .93; utterance.pitch = 1; utterance.volume = volume(voiceVolume, .85);
      state.browserSpeech.utterance = utterance;
      utterance.onend = () => finish(true); utterance.onerror = () => finish(false);
      watchdog = setTimeout(() => finish(false), Math.min(25000, Math.max(4500, phraseText.length * 180)));
      try { engine.resume(); engine.speak(utterance); } catch { finish(false); }
    };
    state.browserSpeech = { finish, utterance: null, volume: volume(voiceVolume, .85) };
    engine.addEventListener?.("voiceschanged", start);
    voicesTimer = setTimeout(() => finish(false), 900);
    start();
  });
}

const celebrations = [
  ["burst", "✦", "#ffd561"], ["neon", "⚡", "#ff7bb7"], ["comet", "★", "#9dcaff"],
  ["disco", "◆", "#bba4ff"], ["royal", "♛", "#ffd561"], ["wave", "≈", "#89dccb"],
  ["pixel", "▰", "#ffab78"], ["aurora", "✧", "#89dccb"], ["spark", "✶", "#ff7bb7"],
  ["crown", "♕", "#ffab78"], ["orbit", "✺", "#9dcaff"], ["flash", "✹", "#bba4ff"]
];
function clearCelebration(state) {
  clearTimeout(state.celebrationTimer); state.celebrationTimer = 0;
  state.celebration?.remove(); state.celebration = null;
}
function showCelebration(root, state, options, motion) {
  if (!options?.groupName || !motion) return;
  clearCelebration(state);
  const suppliedIndex = Number(options.celebrationIndex);
  const index = Number.isFinite(suppliedIndex) ? Math.abs(Math.trunc(suppliedIndex)) % celebrations.length : 0;
  const [effect, symbol, color] = celebrations[index];
  const overlay = document.createElement("div"); overlay.className = `roulette-celebration roulette-celebration-${effect}`;
  overlay.setAttribute("aria-hidden", "true"); overlay.style.setProperty("--celebration-color", color);
  const title = document.createElement("strong"); title.className = "roulette-celebration-title"; title.textContent = options.groupName;
  const caption = document.createElement("span"); caption.className = "roulette-celebration-caption"; caption.textContent = options.celebrationText || "Et nyt dream team er landet!";
  const mark = document.createElement("span"); mark.className = "roulette-celebration-symbol"; mark.textContent = symbol;
  overlay.appendChild(mark); overlay.appendChild(caption); overlay.appendChild(title);
  for (let sparkle = 0; sparkle < 18; sparkle++) {
    const node = document.createElement("i"); node.textContent = symbol;
    const angle = sparkle / 18 * Math.PI * 2;
    node.style.setProperty("--spark-x", `${Math.cos(angle) * (180 + Math.random() * 100)}px`);
    node.style.setProperty("--spark-y", `${Math.sin(angle) * (120 + Math.random() * 110)}px`);
    node.style.setProperty("--spark-turn", `${Math.random() * 360 - 180}deg`);
    node.style.setProperty("--spark-delay", `${Math.random() * .2}s`); overlay.appendChild(node);
  }
  root.appendChild(overlay); state.celebration = overlay;
  state.celebrationTimer = setTimeout(() => clearCelebration(state), 3200);
}

function celebrate(root, state, motion) {
  if (!motion) return;
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
  cancelSpeech(root); stopEffects(state); clearCelebration(state);
  state.cancelSpin?.();
  for (const timer of state.timers) clearTimeout(timer); state.timers.clear();
  cancelAnimationFrame(state.spinFrame); state.spinFrame = 0; removeStrips(state);
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
