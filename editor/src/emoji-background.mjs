import { EMOJI_MAX_BYTES } from './emoji-upload.mjs';

/** Validate local model output before allowing it to replace an editable draft. */
export function backgroundPngBytes(result) {
  const value = result?.base64;
  if (result?.contentType !== 'image/png' || typeof value !== 'string' || !value.length
    || value.length > Math.ceil(EMOJI_MAX_BYTES / 3) * 4 || value.length % 4 !== 0
    || !/^[A-Za-z0-9+/]+={0,2}$/.test(value)) throw new Error('Baggrunden kunne ikke fjernes. Originalbilledet er bevaret.');
  let binary;
  try { binary = atob(value); } catch { throw new Error('Billedresultatet kunne ikke læses. Originalbilledet er bevaret.'); }
  const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
  const png = [137, 80, 78, 71, 13, 10, 26, 10];
  if (bytes.length > EMOJI_MAX_BYTES || bytes.length < png.length || !png.every((value, index) => bytes[index] === value))
    throw new Error('Billedresultatet er ikke et gyldigt PNG-billede på højst 256 KB. Originalbilledet er bevaret.');
  return bytes;
}

export function backgroundPngName(originalName) {
  const leaf = String(originalName ?? '').replaceAll('\\', '/').split('/').at(-1) || 'emoji';
  return (leaf.replace(/\.[^.]*$/, '').slice(0, 220) || 'emoji') + '.png';
}

export async function emojiFileDataUrl(file) {
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  const type = /\.gif$/i.test(file.name) ? 'image/gif' : /\.webp$/i.test(file.name) ? 'image/webp'
    : /\.jpe?g$/i.test(file.name) ? 'image/jpeg' : 'image/png';
  return `data:${type};base64,${btoa(binary)}`;
}
