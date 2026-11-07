import { JOB_TTL_MS, TTL_MS, validId, sourceUrl, validDraft, validateDraftAccess, validateDraftCompletion } from './core.mjs';

const MENU_ID = 'lup-add-emoji';
const MAX_PENDING = 8;
const fail = (message, retry = false) => ({ ok: false, message, retry });

async function prune() {
  const entries = await chrome.storage.session.get(null);
  const obsolete = Object.entries(entries).filter(([key, item]) => /^(pending|job):/.test(key)
    && (!item || !Number.isFinite(item.expiresAt) || item.expiresAt <= Date.now())).map(([key]) => key);
  if (obsolete.length) await chrome.storage.session.remove(obsolete);
}

async function makeRoom(prefix) {
  await prune();
  const entries = await chrome.storage.session.get(null);
  const items = Object.entries(entries).filter(([key]) => key.startsWith(prefix)).sort((a, b) => a[1].expiresAt - b[1].expiresAt);
  if (items.length >= MAX_PENDING) await chrome.storage.session.remove(items.slice(0, items.length - MAX_PENDING + 1).map(([key]) => key));
}

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({ id: MENU_ID, title: 'Tilføj som emoji på LUP.dk', contexts: ['image'] });
  await prune();
});
chrome.runtime.onStartup.addListener(() => { void prune(); });

export async function openImageJob(srcUrl) {
  await makeRoom('job:');
  const id = crypto.randomUUID();
  const selected = sourceUrl(srcUrl);
  const job = { id, srcUrl: selected?.href ?? null, expiresAt: Date.now() + JOB_TTL_MS, prepareTabId: null };
  await chrome.storage.session.set({ [`job:${id}`]: job });
  const tab = await chrome.tabs.create({ url: chrome.runtime.getURL(`prepare.html?job=${id}`) });
  await chrome.storage.session.set({ [`job:${id}`]: { ...job, prepareTabId: tab.id } });
}

chrome.contextMenus.onClicked.addListener((info) => {
  if (info.menuItemId !== MENU_ID) return;
  void openImageJob(info.srcUrl).catch(error => console.error('LUP kunne ikke åbne billedkladden.', error));
});

function prepareSender(sender, id, job) {
  if (!validId(id) || !job || job.id !== id || job.expiresAt <= Date.now() || sender?.tab?.id !== job.prepareTabId) return false;
  try {
    const url = new URL(sender.url);
    const expected = new URL(chrome.runtime.getURL('prepare.html'));
    return url.protocol === expected.protocol && url.host === expected.host && url.pathname === expected.pathname
      && url.searchParams.getAll('job').length === 1 && url.searchParams.get('job') === id;
  } catch { return false; }
}

export async function handleMessage(message, sender) {
  if (!message || typeof message !== 'object' || !validId(message.id)) return fail('Importen er ugyldig. Vælg billedet igen.');
  if (['prepare:get', 'prepare:continue'].includes(message.type)) {
    const key = `job:${message.id}`;
    const job = (await chrome.storage.session.get(key))[key];
    if (job && job.prepareTabId === null && job.expiresAt > Date.now()) return fail('Forbereder…', true);
    if (!prepareSender(sender, message.id, job)) return fail('Billedkladden er udløbet. Højreklik på billedet igen.');
    if (message.type === 'prepare:get') return { ok: true, srcUrl: job.srcUrl };
    const id = crypto.randomUUID();
    const draft = { id, name: message.name, contentType: message.contentType, base64: message.base64,
      expiresAt: Date.now() + TTL_MS, destinationTabId: 1 };
    if (!validDraft(draft)) return fail('Billedet skal være PNG, JPEG, GIF eller WebP på højst 256 KB med et kort navn.');
    await makeRoom('pending:');
    const pendingKey = `pending:${id}`;
    await chrome.storage.session.set({ [pendingKey]: { ...draft, destinationTabId: null } });
    try {
      const tab = await chrome.tabs.create({ url: `https://lup.dk/emojis?lupImport=${id}` });
      await chrome.storage.session.set({ [pendingKey]: { ...draft, destinationTabId: tab.id } });
      await chrome.storage.session.remove(key);
      return { ok: true };
    } catch {
      await chrome.storage.session.remove(pendingKey);
      return fail('LUP.dk kunne ikke åbnes. Prøv igen.');
    }
  }
  if (!['emoji:request', 'emoji:complete'].includes(message.type)) return fail('Ukendt besked.');
  const key = `pending:${message.id}`;
  const draft = (await chrome.storage.session.get(key))[key];
  if (draft?.destinationTabId === null && draft.expiresAt > Date.now()) return fail('Forbereder…', true);
  const allowed = message.type === 'emoji:complete'
    ? validateDraftCompletion(draft, sender, message.id) : validateDraftAccess(draft, sender, message.id);
  if (!allowed) return fail('Billedkladden er udløbet eller tilhører en anden fane. Højreklik på billedet igen.');
  if (message.type === 'emoji:complete') {
    await chrome.storage.session.remove(key);
    return { ok: true };
  }
  return { ok: true, id: draft.id, name: draft.name, contentType: draft.contentType, base64: draft.base64 };
}

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  void handleMessage(message, sender).then(respond).catch(() => respond(fail('Importen kunne ikke læses. Prøv igen.')));
  return true;
});
