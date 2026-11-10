/* Read and clear legacy browser profiles during the one-time database migration. */
(() => {
    const prefix = "lup-avatar-profile-v1:";
    const key = subject => prefix + (typeof subject === "string" && subject.trim() ? "user:" + encodeURIComponent(subject.trim()) : "guest");
    const actions = new Set(["idle", "blink", "wave", "think", "talk", "point", "celebrate", "encourage", "surprised", "unlock", "walk", "run", "jump", "land", "sit", "sleep", "pickup", "interact", "hit", "knockout", "respawn", "ability_1_pulse", "ability_2_magnet", "ability_3_shield", "ability_4_dash", "ability_5_repair", "ultimate_charge", "ultimate_release"]);
    const phrases = new Set(["phrase_1_welcome", "phrase_2_start", "phrase_3_success", "phrase_4_retry", "phrase_5_milestone"]);
    let currentAudio = null;
    const stop = () => { if (currentAudio) { currentAudio.pause(); currentAudio.currentTime = 0; currentAudio = null; } };
    window.lupAvatars = {
        get(subject) { try { return localStorage.getItem(key(subject)); } catch { return null; } },
        remove(subject) { try { localStorage.removeItem(key(subject)); return true; } catch { return false; } },
        stop,
        async play(file) {
            if (typeof file !== "string") return false;
            const match = /^\/assets\/avatars\/(bip-01|raymond|santa|lupp)\/audio\/([a-z0-9_]+)\.wav$/.exec(file);
            if (!match || match[0] !== file || (!actions.has(match[2]) && !phrases.has(match[2]))) return false;
            stop();
            currentAudio = new Audio(file); currentAudio.volume = .65;
            try { await currentAudio.play(); return true; } catch { return false; }
        }
    };
    document.addEventListener("visibilitychange", () => { if (document.hidden) stop(); });
})();
