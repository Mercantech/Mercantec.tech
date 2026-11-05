import test from 'node:test';
import assert from 'node:assert/strict';
import { prepareMermaidSource } from '../src/mermaid-utils.ts';

test('trailing free text on last line is repaired before render', () => {
  const trailing = `flowchart TD
    A[Start] --> B{Er betingelsen sand?}
    B -- Ja --> C[Udfør kodeblok 1]
    B -- Nej --> D[Udfør kodeblok 2]
    C --> E[Slut]
    D --> E Den vil ikke vise følgende`;
  const { source, repaired } = prepareMermaidSource(trailing);
  assert.equal(repaired, true);
  assert.doesNotMatch(source, /Den vil ikke/);
});
