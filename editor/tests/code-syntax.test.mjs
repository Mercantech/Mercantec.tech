import test from 'node:test';
import assert from 'node:assert/strict';
import { curlLanguage, highlightCodeLanguage } from '../src/code-syntax.ts';
import { buildTurnIntoUpdate } from '../src/turn-into-content.ts';

test('a fenced cURL command keeps its language and text while using shell highlighting', () => {
  const command = 'curl --header "Content-Type: application/json" https://example.test';
  const fencedCommand = ['```cURL', command, '```'].join('\n');
  const block = buildTurnIntoUpdate(
    { type: 'paragraph', content: fencedCommand },
    'codeBlock',
    { language: 'csharp' },
    { supportedLanguages: { curl: curlLanguage }, defaultCodeLanguage: 'csharp' },
  );

  assert.equal(block.props.language, 'curl');
  assert.equal(block.content, command);
  assert.equal(curlLanguage.name, 'cURL');
  assert.equal(highlightCodeLanguage(block.props.language), 'shellscript');
  assert.equal(highlightCodeLanguage('csharp'), 'csharp');
});
