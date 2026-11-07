const loginKey = 'lup.emojiImport.login';
const lifetime = 30 * 60 * 1000;
const maxBytes = 256 * 1024;
const mediaTypes = new Set(['image/png', 'image/jpeg', 'image/gif', 'image/webp']);
const sessions = new Map();
let nextHandle = 1;

function importId(value) {
    return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
        ? value.toLowerCase() : null;
}

function matchesPage(page, id) {
    const url = new URL(page.location.href);
    return url.pathname === '/emojis' && importId(url.searchParams.get('lupImport')) === id;
}

function draftValid(data) {
    if (typeof data.name !== 'string' || !data.name.trim() || data.name.length > 240 || /[\x00-\x1f\x7f]/.test(data.name) ||
        !mediaTypes.has(data.contentType) || typeof data.base64 !== 'string' || !data.base64.length ||
        data.base64.length > Math.ceil(maxBytes / 3) * 4 || data.base64.length % 4 !== 0 ||
        !/^[A-Za-z0-9+/]+={0,2}$/.test(data.base64)) return false;
    const padding = data.base64.endsWith('==') ? 2 : data.base64.endsWith('=') ? 1 : 0;
    return data.base64.length / 4 * 3 - padding <= maxBytes;
}

function clearTimers(session) {
    for (const timer of session.timers) session.page.clearTimeout(timer);
    session.timers.length = 0;
}

function notifyError(session, message) {
    if (session.stopped) return;
    session.dotnet.invokeMethodAsync('ExtensionImportError', message).catch(() => {});
}

// Delivers image bytes only. Authentication and upload remain inside LUP.
export function start(dotnet, value) {
    const id = importId(value);
    const page = window;
    if (!id || !matchesPage(page, id)) return 0;
    const handle = nextHandle++;
    const session = { id, page, dotnet, timers: [], stopped: false, delivered: false, inFlight: false };
    const request = () => {
        if (!session.stopped && !session.delivered && matchesPage(page, id))
            page.postMessage({ source: 'lup-emoji-page', type: 'request', id }, page.location.origin);
    };
    session.listener = async event => {
        const data = event.data;
        if (session.stopped || session.delivered || session.inFlight || event.source !== page ||
            event.origin !== page.location.origin || !matchesPage(page, id) || !data ||
            data.source !== 'lup-emoji-extension' || importId(data.id) !== id) return;
        if (data.type === 'error') {
            clearTimers(session);
            notifyError(session, typeof data.message === 'string' && data.message.length <= 300
                ? data.message : 'Billedet kunne ikke hentes fra LUP-udvidelsen.');
            return;
        }
        if (data.type !== 'draft') return;
        if (!draftValid(data)) {
            clearTimers(session);
            notifyError(session, 'Udvidelsen sendte et ugyldigt billede. Vælg billedet igen.');
            return;
        }
        session.inFlight = true;
        clearTimers(session);
        try {
            // A false result means LUP already has a user-edited draft. Do not retry
            // over that draft and do not delete the extension's pending import.
            await dotnet.invokeMethodAsync('ReceiveExtensionDraft', {
                name: data.name, contentType: data.contentType, base64: data.base64,
            });
            session.delivered = true;
        } catch {
            notifyError(session, 'Billedet kunne ikke indsættes. Genindlæs siden for at prøve igen.');
        } finally {
            session.inFlight = false;
        }
    };
    sessions.set(handle, session);
    page.addEventListener('message', session.listener);
    // Register every timeout before sending: a synchronous test bridge or a fast
    // content script can respond immediately and must be able to clear them all.
    for (const delay of [1000, 3000, 6000]) session.timers.push(page.setTimeout(request, delay));
    session.timers.push(page.setTimeout(() => {
        if (!session.delivered && !session.inFlight)
            notifyError(session, 'LUP-udvidelsen svarede ikke. Kontrollér, at den er aktiveret, og genindlæs siden.');
    }, 12000));
    request();
    return handle;
}

export function stop(handle) {
    const session = sessions.get(handle);
    if (!session) return;
    session.stopped = true;
    clearTimers(session);
    session.page.removeEventListener('message', session.listener);
    sessions.delete(handle);
}

export function complete(value) {
    const id = importId(value);
    const page = window;
    if (!id || !matchesPage(page, id)) return;
    for (const session of sessions.values()) {
        if (session.id !== id) continue;
        session.delivered = true;
        clearTimers(session);
    }
    page.postMessage({ source: 'lup-emoji-page', type: 'complete', id }, page.location.origin);
    const url = new URL(page.location.href);
    url.searchParams.delete('lupImport');
    page.history.replaceState(page.history.state, '', url.pathname + url.search + url.hash);
}

export function prepareLogin(value) {
    const id = importId(value);
    if (!id) throw new Error('Ugyldig billedimport.');
    window.sessionStorage.setItem(loginKey, JSON.stringify({ id, expiresAt: Date.now() + lifetime }));
}

export function consumeLoginReturnPath() {
    try {
        const raw = window.sessionStorage.getItem(loginKey);
        window.sessionStorage.removeItem(loginKey);
        if (!raw) return null;
        const value = JSON.parse(raw);
        const id = importId(value?.id);
        const now = Date.now();
        if (!id || typeof value.expiresAt !== 'number' || !Number.isFinite(value.expiresAt) ||
            value.expiresAt <= now || value.expiresAt > now + lifetime) return null;
        // Construct the only supported destination; never trust a stored return URL.
        return '/emojis?lupImport=' + id;
    } catch {
        return null;
    }
}
