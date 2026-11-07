const issue = (title, message, hint, retry = true) => ({ title, message, hint, retry });
export const IMPORT_ISSUES = Object.freeze({
  'LUP-IMP-001': issue('Billedkladden er udløbet', 'Denne billedimport er ikke længere aktiv.', 'Højreklik på billedet på originalsiden igen for at starte en ny kladde.', false),
  'LUP-IMP-002': issue('Chrome kan ikke læse originalsiden', 'Siden tillader ikke, at udvidelsen henter billedet herfra.', 'Prøv billedets egen hjemmeside, eller gem billedet og vælg filen her.'),
  'LUP-IMP-003': issue('Det midlertidige billede er væk', 'Billedet var knyttet til den oprindelige side og kan ikke længere læses.', 'Hold originalsiden åben. Højreklik på billedet igen, eller vælg en lokal fil.'),
  'LUP-IMP-004': issue('Billedet kunne ikke gemmes midlertidigt', 'Chrome kunne ikke gemme den lokale billedkladde.', 'Luk nogle gamle LUP-billedvinduer, og prøv igen. Du kan også vælge en fil.'),
  'LUP-IMP-005': issue('Den hentede billedkopi kunne ikke læses', 'Billedkopien havde ikke et understøttet billedformat.', 'Prøv at hente billedet igen, eller gem det som PNG, JPEG, WebP, AVIF eller GIF.'),
  'LUP-IMP-006': issue('Originalsiden er ikke tilgængelig', 'Fanen eller billedets indlejrede ramme er lukket eller ændret.', 'Åbn originalsiden, og højreklik på billedet igen.'),
  'LUP-IMP-007': issue('Billedet er stadig ved at blive hentet', 'Originalsiden er endnu ikke færdig med at overføre billedet.', 'Vent et øjeblik, og prøv igen.'),
  'LUP-IMP-008': issue('Billedadressen kan ikke åbnes direkte', 'Adressen er ikke en almindelig billedadresse på nettet.', 'Prøv kopien fra originalsiden, eller vælg en fil fra computeren.'),
  'LUP-IMP-101': issue('Chrome mangler adgang til billedserveren', 'Billedet ligger på et domæne, som udvidelsen endnu ikke har adgang til.', 'Giv adgang med knappen nedenfor. Tilladelsen gælder kun det viste domæne.'),
  'LUP-IMP-102': issue('Adgang blev ikke givet', 'Chrome har ikke fået tilladelse til at hente fra billedserveren.', 'Du kan give adgang igen eller vælge billedet som en lokal fil.'),
  'LUP-IMP-103': issue('Billedet kræver login', 'Billedserveren beder om en aktiv login-session (HTTP 401).', 'Log ind på originalsiden, og højreklik igen. Du kan også prøve det viste billede som stillbillede.'),
  'LUP-IMP-104': issue('Billedserveren afviste hentningen', 'Serveren vil ikke udlevere billedet direkte (HTTP 403).', 'Prøv billedkopien fra originalsiden, eller gem billedet og vælg filen her.'),
  'LUP-IMP-105': issue('Billedet findes ikke på adressen', 'Billedet er fjernet, eller adressen er udløbet (HTTP 404/410).', 'Genindlæs originalsiden, og højreklik på billedet igen. En synlig kopi kan muligvis bruges som stillbillede.'),
  'LUP-IMP-106': issue('Billedserveren beder om en pause', 'Serveren har midlertidigt begrænset antallet af hentninger (HTTP 429).', 'Vent lidt, og tryk Prøv igen.'),
  'LUP-IMP-107': issue('Billedserveren meldte fejl', 'Billedserveren kunne ikke levere billedet lige nu.', 'Prøv igen om lidt, eller vælg en lokal fil.'),
  'LUP-IMP-108': issue('Billedet kunne ikke hentes direkte', 'Forbindelsen blev blokeret eller kunne ikke oprettes.', 'Prøv kopien fra originalsiden. Hvis det ikke hjælper, så gem billedet og vælg filen her.'),
  'LUP-IMP-109': issue('Billedserveren svarede for langsomt', 'Billedet blev ikke færdigt med at blive hentet inden tidsgrænsen.', 'Prøv igen, eller vælg en lokal kopi.'),
  'LUP-IMP-110': issue('Billedet er for stort', 'Kildefilen må højst fylde 8 MB.', 'Vælg en mindre udgave af billedet eller en kortere GIF.'),
  'LUP-IMP-111': issue('Adressen gav ikke et billede', 'Serveren returnerede ikke et genkendeligt billedformat.', 'Det kan være en login-side, en serverfejl eller et andet filformat. Prøv kopien fra originalsiden eller en lokal fil.'),
  'LUP-IMP-112': issue('Billedfilen kunne ikke læses', 'Billedet er tomt, beskadiget eller bruger et format, som Chrome ikke kan åbne.', 'Gem en ny kopi som PNG, JPEG, WebP, AVIF eller GIF, og prøv igen.'),
  'LUP-IMP-113': issue('SVG skal først gemmes som et almindeligt billede', 'SVG kan ikke importeres direkte som emoji her.', 'Gem eller eksportér billedet som PNG. Et synligt billede kan også forsøges hentet som stillbillede.'),
  'LUP-IMP-114': issue('Billedet har for mange pixels', 'Billedet må højst være 16 megapixel.', 'Vælg en mindre udgave af billedet. Det endelige emoji-billede bliver meget mindre.'),
  'LUP-IMP-115': issue('Chrome kunne ikke spørge om adgang', 'Tilladelsesvinduet kunne ikke åbnes.', 'Tryk på adgangsknappen igen, eller vælg en lokal fil.'),
  'LUP-IMP-116': issue('Hentningen blev afbrudt', 'Billedet blev ikke overført fuldstændigt.', 'Kontrollér forbindelsen, og prøv igen. Den eksisterende kladde er bevaret.'),
  'LUP-IMP-117': issue('GIF’en kunne ikke tilpasses', 'Animationen kunne ikke behandles inden for emojiformatets grænser.', 'Prøv en kortere eller mindre GIF. Animationen bliver ikke automatisk lavet om til et stillbillede.'),
  'LUP-IMP-119': issue('Det viste billede kan ikke kopieres', 'Browserens billedbeskyttelse forhindrer kopiering af de viste pixels.', 'Gem billedet fra originalsiden, og vælg filen her.'),
});

export class ImportFailure extends Error {
  constructor(code, context = {}) {
    const safe = Object.hasOwn(IMPORT_ISSUES, code) ? code : 'LUP-IMP-108';
    super(IMPORT_ISSUES[safe].message); this.name = 'ImportFailure'; this.code = safe; this.context = safeContext(context);
  }
}
const METHODS = new Set(['direct', 'page-fetch', 'embedded', 'blob', 'canvas', 'capture-cache', 'alternate-source', 'local-file']);
function safeContext(value = {}) {
  const result = {};
  const status = value.status ?? value.httpStatus;
  if (Number.isInteger(status) && status >= 100 && status <= 599) result.status = status;
  if (Number.isInteger(value.bytes) && value.bytes >= 0 && value.bytes <= 1024 * 1024 * 1024) result.bytes = value.bytes;
  if (METHODS.has(value.method)) result.method = value.method;
  if (typeof value.host === 'string' && /^[a-z0-9.:[\]-]{1,253}$/i.test(value.host)) result.host = value.host;
  return result;
}
export function importIssue(error, fallback = 'LUP-IMP-112') {
  const code = Object.hasOwn(IMPORT_ISSUES, error?.code) ? error.code : fallback;
  return { code, ...IMPORT_ISSUES[code], context: safeContext(error?.context) };
}
export function importDiagnostic(error, version) {
  const value = importIssue(error);
  const lines = ['LUP billedimport', `Version: ${/^[0-9.]{1,20}$/.test(version) ? version : 'ukendt'}`, `Fejlkode: ${value.code}`];
  if (value.context.host) lines.push(`Billeddomæne: ${value.context.host}`);
  if (value.context.method) lines.push(`Metode: ${value.context.method}`);
  if (value.context.status) lines.push(`HTTP-status: ${value.context.status}`);
  if (value.context.bytes !== undefined) lines.push(`Bytes: ${value.context.bytes}`);
  return lines.join('\n');
}
