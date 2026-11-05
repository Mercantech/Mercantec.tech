import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

test('code blocks wire language picker, C#, highlighter and copy', () => {
  const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));
  const code = readFileSync(join(root, 'src/code-block.ts'), 'utf8');
  const editor = readFileSync(join(root, 'src/editor.tsx'), 'utf8');
  const css = readFileSync(join(root, 'src/editor.css'), 'utf8');
  const turnInto = readFileSync(join(root, 'src/turn-into.tsx'), 'utf8');

  assert.equal(pkg.dependencies['@blocknote/code-block'], '0.56.0');
  assert.match(code, /defaultLanguage:\s*'csharp'/);
  assert.match(code, /'csharp'/);
  assert.match(code, /Kopiér/);
  assert.match(code, /createNotionCodeBlock/);
  assert.match(code, /syntaxHighlighter/);
  assert.match(editor, /codeBlock:createNotionCodeBlock/);
  assert.match(editor, /extensions:\[syntaxHighlighter\]/);
  assert.match(css, /\.notion-code-copy/);
  assert.match(turnInto, /language:\s*'csharp'/);
});
