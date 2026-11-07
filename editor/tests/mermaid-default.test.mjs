import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const block = readFileSync(join(root, 'src/mermaid-block.tsx'), 'utf8');
const utils = readFileSync(join(root, 'src/mermaid-utils.ts'), 'utf8');

test('mermaid block ships a flowchart sample teachers can paste from Notion', () => {
  assert.match(block, /type:\s*'mermaidDiagram'/);
  assert.match(block, /dangerouslySetInnerHTML/, 'SVG skal holdes i React-state så BlockNote re-renders ikke visker diagrammet');
  assert.match(block, /flowchart TB/);
  assert.match(block, /E2E\["E2E \(End-to-End\)"\]/);
  assert.match(block, /UNIT\["Unit"\]/);
  assert.doesNotMatch(block, /style UNIT fill:/);
  assert.match(utils, /securityLevel:\s*'strict'/);
  assert.match(block, /formatMermaidRenderError/);
});
