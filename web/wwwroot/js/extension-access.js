const key = 'lup.extension.login';
const lifetime = 30 * 60 * 1000;

export function prepareLogin() {
    window.sessionStorage.setItem(key, JSON.stringify({ intent: 'extension-access', expiresAt: Date.now() + lifetime }));
}

export function consumeLoginReturnPath() {
    try {
        const raw = window.sessionStorage.getItem(key);
        window.sessionStorage.removeItem(key);
        if (!raw) return null;
        const value = JSON.parse(raw);
        const now = Date.now();
        if (value?.intent !== 'extension-access' || typeof value.expiresAt !== 'number'
            || !Number.isFinite(value.expiresAt) || value.expiresAt <= now || value.expiresAt > now + lifetime) return null;
        return '/extension';
    } catch { return null; }
}

export function copyKey(value) {
    if (typeof value !== 'string' || !/^lup_[A-Za-z0-9_-]{43}$/.test(value))
        return Promise.reject(new Error('Nøglen kunne ikke kopieres.'));
    return navigator.clipboard.writeText(value);
}
