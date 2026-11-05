import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const source = readFileSync(join(root, 'src/mermaid-block.tsx'), 'utf8');

test('mermaid block ships a flowchart sample teachers can paste from Notion', () => {
  assert.match(source, /type:\s*'mermaidDiagram'/);
  assert.match(source, /dangerouslySetInnerHTML/, 'SVG skal holdes i React-state så BlockNote re-renders ikke visker diagrammet');
  assert.match(source, /flowchart TB/);
  assert.match(source, /E2E\["E2E \(End-to-End\)"\]/);
  assert.match(source, /style UNIT fill:#e8f5e9/);
  assert.match(source, /securityLevel:\s*'strict'/);
});
