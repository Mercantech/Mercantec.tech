import assert from 'node:assert/strict';
import { after, test } from 'node:test';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import ts from 'typescript';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { emojiTitleCatalog, splitEmojiTitle } from '../src/emoji-title.mjs';

const editor = fileURLToPath(new URL('../', import.meta.url));
const cache = path.resolve(editor, 'node_modules/.cache');
await mkdir(cache, { recursive: true });
const output = await mkdtemp(path.join(cache, 'emoji-title-test-'));
after(async () => {
  // Only the test's freshly created directory may be removed.
  assert.ok(path.resolve(output).startsWith(cache + path.sep));
  await rm(output, { recursive: true, force: true });
});
for (const name of ['emoji-context', 'entity-reference-components', 'reference-suggestion-menu']) {
  let source = await readFile(path.join(editor, 'src', name + '.tsx'), 'utf8');
  source = source.replaceAll("from './emoji-context'", "from './emoji-context.mjs'")
    .replaceAll("from './emoji-title.mjs'", `from '${pathToFileURL(path.join(editor, 'src/emoji-title.mjs')).href}'`)
    .replaceAll("from './entity-references.mjs'", `from '${pathToFileURL(path.join(editor, 'src/entity-references.mjs')).href}'`);
  const compiled = ts.transpileModule(source, { fileName: name + '.tsx', compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
    esModuleInterop: true,
  } });
  await writeFile(path.join(output, name + '.mjs'), compiled.outputText);
}
const { EmojiText, EmojiCatalog } = await import(pathToFileURL(path.join(output, 'emoji-context.mjs')));
const { ResolvedReferenceLink } = await import(pathToFileURL(path.join(output, 'entity-reference-components.mjs')));
const { ReferenceSuggestionMenu } = await import(pathToFileURL(path.join(output, 'reference-suggestion-menu.mjs')));

const raymond = Object.freeze({ id: '057526d2-c666-5691-9d54-cf1626dddc76', name: 'raymond_applause',
  contentUrl: '/api/emojis/raymond/content', altText: 'Raymond klapper' });
const vatson = Object.freeze({ id: 'vatson', name: 'vatson-vinker', contentUrl: '/api/emojis/vatson/content', altText: '' });
const catalog = Object.freeze([raymond, vatson]);
const indexed = emojiTitleCatalog(catalog);
const render = child => renderToStaticMarkup(React.createElement(EmojiCatalog.Provider, { value: catalog }, child));

test('title rendering resolves repeated case-insensitive shortcodes while preserving Unicode and unknown text', () => {
  const parts = splitEmojiTitle('🗄️ :Raymond_Applause: SQL :vatson-vinker: :unknown: :raymond_applause:', indexed);
  assert.deepEqual(parts.map(p => p.emoji?.name || p.text), [
    '🗄️ ', 'raymond_applause', ' SQL ', 'vatson-vinker', ' :unknown: ', 'raymond_applause',
  ]);
});

test('title shortcode boundaries match platform word rules and do not consume malformed names', () => {
  for (const text of ['sql:raymond_applause:', 'ø:raymond_applause:', '1:raymond_applause:', '_:vatson-vinker:', ':r:', ':raymond_applause', ':navn med mellemrum:']) {
    assert.deepEqual(splitEmojiTitle(text, indexed), [{ text }]);
  }
  assert.equal(splitEmojiTitle('(:raymond_applause:)', indexed)[1].emoji, raymond);
  assert.deepEqual(splitEmojiTitle('', indexed), [{ text: '' }]);
});

test('catalog indexes are reused by identity and replaced when refreshed runtime image URLs arrive', () => {
  assert.equal(emojiTitleCatalog(catalog), indexed);
  const refreshed = [{ ...raymond, contentUrl: '/api/emojis/raymond/new-content' }];
  assert.notEqual(emojiTitleCatalog(refreshed), indexed);
  assert.equal(splitEmojiTitle(':raymond_applause:', emojiTitleCatalog(refreshed))[0].emoji.contentUrl, '/api/emojis/raymond/new-content');
  assert.equal(indexed.get('raymond_applause').contentUrl, raymond.contentUrl);
});

test('actual React title output uses the current image, meaningful ALT and escaped text without HTML injection', () => {
  const html = render(React.createElement(EmojiText, { text: '🗄️ :raymond_applause: SQL <script>alert(1)</script> :unknown:' }));
  assert.match(html, /<img[^>]*class="notion-custom-emoji"[^>]*src="\/api\/emojis\/raymond\/content"/);
  assert.match(html, /alt="Raymond klapper"/);
  assert.match(html, /title=":raymond_applause:"/);
  assert.match(html, /🗄️/);
  assert.match(html, /&lt;script&gt;alert\(1\)&lt;\/script&gt; :unknown:/);
  assert.doesNotMatch(html, /<script>|<svg|onload=/);
});

test('an empty or unavailable emoji catalog retains the literal title and its Unicode emoji', () => {
  const html = renderToStaticMarkup(React.createElement(EmojiText, { text: '🐘 :raymond_applause: SQL' }));
  assert.match(html, /🐘 :raymond_applause: SQL/);
  assert.doesNotMatch(html, /<img/);
});

test('inline and block references render emoji labels without changing identity or navigation', () => {
  const reference = Object.freeze({ kind: 'notion-page', id: 'sql-page' });
  const value = Object.freeze({ reference, title: ':raymond_applause: SQL 🗄️', href: '/notion/sql-page', contextLabel: 'Teori' });
  const unchanged = JSON.stringify({ reference, value });
  for (const block of [false, true]) {
    const html = render(React.createElement(ResolvedReferenceLink, { reference, value, block }));
    assert.match(html, /<a[^>]*href="\/notion\/sql-page"[^>]*contentEditable="false"/);
    assert.match(html, /<img[^>]*alt="Raymond klapper"/);
    assert.match(html, /SQL 🗄️/);
    assert.equal(JSON.stringify({ reference, value }), unchanged);
  }
});

test('reference suggestion rows show emoji titles and preserve controller IDs and selection semantics', () => {
  let picked = 0;
  const items = [{ title: ':raymond_applause: SQL', subtext: 'Side · Teori', group: 'Indhold i LUP', onItemClick: () => picked++ }];
  const html = render(React.createElement(ReferenceSuggestionMenu, { items, loadingState: 'loaded', selectedIndex: 0 }));
  assert.match(html, /id="bn-suggestion-menu"/);
  assert.match(html, /id="bn-suggestion-menu-item-0"/);
  assert.match(html, /role="option"[^>]*aria-selected="true"/);
  assert.match(html, /<img[^>]*alt="Raymond klapper"/);
  assert.match(html, /Side · Teori/);
  assert.equal(picked, 0);
});
