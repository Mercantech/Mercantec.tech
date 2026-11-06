export const curlLanguage = { name: 'cURL', aliases: ['curl'] };

export function highlightCodeLanguage(language: string): string {
  // Keep the saved cURL label and highlight its command using the bundled shell grammar.
  return language.trim().toLowerCase() === 'curl' ? 'shellscript' : language;
}
