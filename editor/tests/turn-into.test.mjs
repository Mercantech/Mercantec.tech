import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');

test('drag handle exposes Turn into and callout is a first-class block', () => {
  const turnInto = readFileSync(join(root, 'src/turn-into.tsx'), 'utf8');
  const callout = readFileSync(join(root, 'src/callout-block.tsx'), 'utf8');
  const editor = readFileSync(join(root, 'src/editor.tsx'), 'utf8');
  assert.match(turnInto, /Omdan til/);
  assert.match(turnInto, /type: 'callout'/);
  assert.match(turnInto, /type: 'learningGoal'/);
  assert.match(turnInto, /type: 'mermaidDiagram'/);
  assert.match(turnInto, /Diagram/);
  assert.match(turnInto, /buildTurnIntoUpdate/);
  assert.match(turnInto, /NotionBlockTypeSelect/);
  assert.match(editor, /NotionBlockTypeSelect/);
  assert.match(callout, /type: 'callout'/);
  assert.match(callout, /tone/);
  assert.match(editor, /NotionDragHandleMenu/);
  assert.match(editor, /schemaVersion=5/);
  assert.match(editor, /callout:createCallout/);
});
