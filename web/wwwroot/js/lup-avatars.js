/* Read and clear legacy browser profiles during the one-time database migration. */
(() => {
    const prefix = "lup-avatar-profile-v1:";
    const key = subject => prefix + (typeof subject === "string" && subject.trim() ? "user:" + encodeURIComponent(subject.trim()) : "guest");
    let currentAudio = null;
    window.lupAvatars = {
        get(subject) { try { return localStorage.getItem(key(subject)); } catch { return null; } },
        remove(subject) { try { localStorage.removeItem(key(subject)); return true; } catch { return false; } },
        async play(file) {
            if (typeof file !== "string" || !/^\/assets\/avatars\/bip-01\/audio\/phrase_[1-5]_[a-z]+\.wav$/.test(file)) return false;
            if (currentAudio) { currentAudio.pause(); currentAudio.currentTime = 0; }
            currentAudio = new Audio(file); currentAudio.volume = .65;
            try { await currentAudio.play(); return true; } catch { return false; }
        }
    };
    document.addEventListener("visibilitychange", () => { if (document.hidden && currentAudio) currentAudio.pause(); });
})();
