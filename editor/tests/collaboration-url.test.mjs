import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

test('the drawing and document providers use the same collaboration URL', () => {
  for (const source of ['editor.tsx', 'drawing-runtime.ts']) {
    const code = readFileSync(new URL('../src/' + source, import.meta.url), 'utf8');
    assert.match(code, /url:\s*collaborationUrl\(location\)/, source);
  }
});
