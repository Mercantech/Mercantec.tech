export const EMOJI_MAX_BYTES = 256 * 1024;

export function suggestEmojiName(fileName, existingNames = []) {
  const leaf = String(fileName ?? '').replace(/\\/g, '/').split('/').pop() ?? '';
  const stem = leaf.replace(/\.[^.]*$/, '').toLowerCase().normalize('NFD').replace(/\p{M}/gu, '')
    .replace(/æ/g, 'ae').replace(/ø/g, 'o').replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '')
    .slice(0, 32).replace(/_+$/g, '');
  const base = stem.length >= 2 ? stem : 'emoji';
  const names = new Set(Array.from(existingNames, value => String(value).toLowerCase()));
  if (!names.has(base)) return base;
  for (let number = 2; ; number++) {
    const suffix = '_' + number;
    const candidate = base.slice(0, 32 - suffix.length).replace(/_+$/g, '') + suffix;
    if (!names.has(candidate)) return candidate;
  }
}

export function normalizeEmojiName(name) {
  const normalized = String(name ?? '').trim().replace(/^:+|:+$/g, '').toLowerCase();
  return /^[a-z0-9_]{2,32}$/.test(normalized) ? normalized : null;
}

export function validateEmojiFile(file) {
  if (!file || file.size <= 0) return 'Vælg et billede med indhold.';
  if (file.size > EMOJI_MAX_BYTES) return 'Billedet må højst være 256 KB.';
  if (!/\.(png|jpe?g|gif|webp)$/i.test(file.name)) return 'Brug et PNG-, JPEG-, GIF- eller WebP-billede.';
  return null;
}

function errorMessage(status, code) {
  const messages = {
    invalid_name: 'Navnet skal være 2–32 tegn: små bogstaver, tal og _.',
    name_taken: 'Navnet er allerede brugt. Vælg et andet.',
    unsupported_file_type: 'Brug et PNG-, JPEG-, GIF- eller WebP-billede.',
    invalid_file_signature: 'Filen matcher ikke billedtypen.',
    invalid_filename: 'Filnavnet kan ikke bruges.',
    file_too_large: 'Billedet må højst være 256 KB.',
    missing_file: 'Vælg et billede, og prøv igen.',
    incomplete_upload: 'Vælg et billede, og prøv igen.',
    emoji_limit: 'Biblioteket er fyldt. Slet en emoji, før du uploader en ny.',
    object_storage_disabled: 'Fillageret er ikke klar. Prøv igen senere.',
  };
  return messages[code] ?? (status === 401 ? 'Log ind igen for at bruge emojis.'
    : status === 403 ? 'Kun undervisere kan tilføje emojis.'
    : 'Emojien kunne ikke gemmes. Prøv igen.');
}

/** Called only by the explicit Save action. Failed uploads are never retried automatically. */
export async function uploadPlatformEmoji(file, name, { getToken, signal, fetchImpl = globalThis.fetch }) {
  const invalidFile = validateEmojiFile(file);
  if (invalidFile) throw new Error(invalidFile);
  const normalized = normalizeEmojiName(name);
  if (!normalized) throw new Error(errorMessage(400, 'invalid_name'));
  signal?.throwIfAborted();
  const token = await getToken();
  signal?.throwIfAborted();
  const body = new FormData();
  body.append('name', normalized);
  body.append('file', file, file.name);
  let response;
  try {
    response = await fetchImpl('/api/emojis', { method: 'POST', headers: { Authorization: 'Bearer ' + token }, body, signal });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new Error('Forbindelsen blev afbrudt. Kontrollér biblioteket, før du prøver at gemme igen.');
  }
  let result;
  try { result = await response.json(); } catch { /* The HTTP status remains useful for non-JSON errors. */ }
  if (!response.ok) throw new Error(errorMessage(response.status, result?.error));
  if (!result || typeof result.id !== 'string' || typeof result.name !== 'string' || typeof result.contentUrl !== 'string')
    throw new Error('Emojien er gemt, men svaret kunne ikke læses. Åbn biblioteket for at finde den.');
  return { id: result.id, name: result.name, contentUrl: result.contentUrl, isStandard: result.isStandard === true,
    category: typeof result.category === 'string' ? result.category : 'custom',
    keywords: Array.isArray(result.keywords) ? result.keywords.filter(word => typeof word === 'string') : [] };
}
