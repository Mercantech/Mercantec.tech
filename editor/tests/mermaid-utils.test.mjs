import test from 'node:test';
import assert from 'node:assert/strict';
import {
  formatMermaidRenderError,
  prepareMermaidSource,
  repairAccidentalProseAfterEdgeTarget,
} from '../src/mermaid-utils.ts';

test('repairAccidentalProseAfterEdgeTarget strips prose after edge target', () => {
  const line = '    D --> E Den vil ikke vise følgende';
  assert.equal(repairAccidentalProseAfterEdgeTarget(line), '    D --> E');
});

test('repairAccidentalProseAfterEdgeTarget keeps labeled edges and node labels', () => {
  const edge = '    B -- Ja --> C[Udfør kodeblok 1]';
  assert.equal(repairAccidentalProseAfterEdgeTarget(edge), edge);
  const node = '    C --> E[Slut]';
  assert.equal(repairAccidentalProseAfterEdgeTarget(node), node);
});

test('prepareMermaidSource repairs trailing prose but keeps valid danish flowchart', () => {
  const clean = `flowchart TD
    A[Start] --> B{Er betingelsen sand?}
    B -- Ja --> C[Udfør kodeblok 1]
    B -- Nej --> D[Udfør kodeblok 2]
    C --> E[Slut]
    D --> E`;
  const { source, repaired } = prepareMermaidSource(clean);
  assert.equal(repaired, false);
  assert.match(source, /Udfør kodeblok/);

  const broken = clean.replace('D --> E', 'D --> E Den vil ikke vise følgende');
  const fixed = prepareMermaidSource(broken);
  assert.equal(fixed.repaired, true);
  assert.match(fixed.source, /D --> E\n?$/m);
});

test('formatMermaidRenderError surfaces parse line from Mermaid', () => {
  const text = formatMermaidRenderError({
    message: "Parse error on line 6:\n...got 'Den'",
    hash: { text: 'Den', loc: { first_line: 6 } },
  });
  assert.match(text, /Parse error on line 6/);
  assert.match(text, /linje 6/);
  assert.match(text, /Den/);
});
