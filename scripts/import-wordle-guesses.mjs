import { mkdir, writeFile } from 'node:fs/promises';

const source = 'https://ordregister.dk/files/cor1.5.1.0.tsv';
const destination = new URL('../api/Wordle/Data/guesses.txt', import.meta.url);
const wordPattern = /^[a-zæøå]{5}$/u;
const excludedForms = /^(?:prop|fork|præfiks|suffiks|symbol)(?:\.|$)/u;

const response = await fetch(source);
if (!response.ok) {
  throw new Error(`Kunne ikke hente COR₁ (${response.status}).`);
}

const guesses = new Set();
for (const line of (await response.text()).split(/\r?\n/u)) {
  const [id, , , form = '', fullWord = '', norm = ''] = line.split('\t');
  if (!id.startsWith('COR.') || norm !== 'N' || excludedForms.test(form)) continue;

  const word = fullWord.normalize('NFC').toLowerCase();
  if (wordPattern.test(word)) guesses.add(word);
}

if (guesses.size !== 6419) {
  throw new Error(`Forventede 6419 ord i COR₁ 1.5.1.0, fik ${guesses.size}.`);
}

await mkdir(new URL('.', destination), { recursive: true });
await writeFile(destination, `${[...guesses].sort().join('\n')}\n`, 'utf8');
console.log(`Importerede ${guesses.size} godkendte gæt fra COR₁ 1.5.1.0.`);
