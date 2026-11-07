const DATABASE = 'lup-import-captures-v1';
const STORE = 'captures';
const MAX_ITEM = 8 * 1024 * 1024;
const MAX_TOTAL = 32 * 1024 * 1024;
const MAX_ITEMS = 8;
const TTL = 30 * 60 * 1000;
let opening;
let writes = Promise.resolve();

function database() {
  if (!opening) opening = new Promise((resolve, reject) => {
    if (typeof indexedDB === 'undefined') { reject(new Error('capture_storage_unavailable')); return; }
    const request = indexedDB.open(DATABASE, 1);
    request.onupgradeneeded = () => { if (!request.result.objectStoreNames.contains(STORE)) request.result.createObjectStore(STORE, { keyPath: 'id' }); };
    request.onerror = () => reject(new Error('capture_storage_unavailable'));
    request.onblocked = () => reject(new Error('capture_storage_unavailable'));
    request.onsuccess = () => {
      const db = request.result;
      db.onversionchange = () => { db.close(); opening = null; };
      resolve(db);
    };
  }).catch(error => { opening = null; throw error; });
  return opening;
}

function requestValue(request) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(new Error('capture_storage_unavailable'));
  });
}
function completed(transaction) {
  const result = new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onabort = transaction.onerror = () => reject(new Error('capture_storage_unavailable'));
  });
  result.catch(() => {});
  return result;
}
function serial(action) {
  const result = writes.then(action);
  writes = result.catch(() => {});
  return result;
}

/** Blobs stay in the extension origin, separate from the 10 MB session JSON quota. */
export function putCapture(id, blob, metadata, expiresAt) {
  return serial(async () => {
    const now = Date.now();
    if (typeof id !== 'string' || !/^[0-9a-f-]{36}$/.test(id) || !(blob instanceof Blob) || !blob.size || blob.size > MAX_ITEM
        || !Number.isFinite(expiresAt) || expiresAt <= now || expiresAt > now + TTL)
      throw new Error('capture_storage_invalid');
    const db = await database(), transaction = db.transaction(STORE, 'readwrite'), done = completed(transaction), store = transaction.objectStore(STORE);
    const rows = await requestValue(store.getAll());
    const retained = [];
    for (const row of rows) {
      if (row.id === id || !(row.blob instanceof Blob) || row.expiresAt <= now) store.delete(row.id);
      else retained.push(row);
    }
    retained.sort((a, b) => a.createdAt - b.createdAt);
    let total = retained.reduce((sum, row) => sum + row.blob.size, 0) + blob.size;
    while (retained.length >= MAX_ITEMS || total > MAX_TOTAL) {
      const old = retained.shift();
      if (!old) break;
      total -= old.blob.size; store.delete(old.id);
    }
    store.put({ id, blob, method: metadata.method, animationLost: metadata.animationLost === true,
      createdAt: now, expiresAt });
    await done;
  });
}

export async function getCapture(id) {
  const db = await database(), transaction = db.transaction(STORE, 'readonly');
  const row = await requestValue(transaction.objectStore(STORE).get(id));
  if (!row || !(row.blob instanceof Blob) || !row.blob.size || row.blob.size > MAX_ITEM || row.expiresAt <= Date.now()) {
    if (row) await deleteCapture(id);
    return null;
  }
  return row;
}

export function deleteCapture(id) {
  return serial(async () => {
    const db = await database(), transaction = db.transaction(STORE, 'readwrite'), done = completed(transaction);
    transaction.objectStore(STORE).delete(id); await done;
  });
}

export function pruneCaptures(activeIds) {
  return serial(async () => {
    const db = await database(), transaction = db.transaction(STORE, 'readwrite'), done = completed(transaction), store = transaction.objectStore(STORE);
    const rows = await requestValue(store.getAll()), now = Date.now();
    for (const row of rows) if (row.expiresAt <= now || (activeIds && !activeIds.has(row.id))) store.delete(row.id);
    await done;
  });
}
