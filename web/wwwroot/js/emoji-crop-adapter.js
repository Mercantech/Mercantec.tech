const editors = new Map();
let nextHandle = 1;
let modulePromise;

async function getModule() {
    try { return await (modulePromise ??= import('/image-tools/emoji-crop.mjs')); }
    catch (error) { modulePromise = null; throw error; }
}

function sourceBlob(dataUrl) {
    if (typeof dataUrl !== 'string' || dataUrl.length > Math.ceil(256 * 1024 / 3) * 4 + 50)
        throw new Error('Vælg et billede på højst 256 KB.');
    const match = /^data:(image\/(?:png|jpeg|gif|webp));base64,([A-Za-z0-9+/]+={0,2})$/.exec(dataUrl);
    if (!match) throw new Error('Billedet kunne ikke åbnes til beskæring.');
    const raw = atob(match[2]);
    if (!raw.length || raw.length > 256 * 1024) throw new Error('Vælg et billede på højst 256 KB.');
    return new Blob([Uint8Array.from(raw, character => character.charCodeAt(0))], { type: match[1] });
}

export async function mount(element, dataUrl) {
    const blob = sourceBlob(dataUrl);
    const module = await getModule();
    const editor = await module.createCropEditor(element, blob);
    editor.setMode?.('square');
    const handle = nextHandle++;
    editors.set(handle, editor);
    return handle;
}

export async function result(handle) {
    const editor = editors.get(handle);
    if (!editor) throw new Error('Beskæringen er lukket. Prøv igen.');
    if (!editor.getState().enabled) return { changed: false };
    const blob = await editor.getBlob();
    if (editors.get(handle) !== editor) throw new Error('Beskæringen er lukket.');
    if (blob.type !== 'image/png' || !blob.size || blob.size > 256 * 1024)
        throw new Error('Beskæringen kunne ikke gemmes som PNG på højst 256 KB.');
    let binary = '';
    for (const byte of new Uint8Array(await blob.arrayBuffer())) binary += String.fromCharCode(byte);
    return { changed: true, contentType: 'image/png', base64: btoa(binary) };
}

export function destroy(handle) {
    const editor = editors.get(handle);
    if (!editor) return;
    editors.delete(handle);
    (editor.destroy ?? editor.dispose).call(editor);
}
