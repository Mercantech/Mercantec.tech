/** Fictional local UI fixture; never included in the production editor bundle. */
import React, { useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { BlockNoteSchema, defaultInlineContentSpecs } from '@blocknote/core';
import { createReactInlineContentSpec, SuggestionMenuController, useCreateBlockNote } from '@blocknote/react';
import { BlockNoteView } from '@blocknote/mantine';
import '@blocknote/mantine/style.css';
import '../src/editor.css';
import { EmojiCatalog, EmojiText } from '../src/emoji-context';
import { ReferenceProvider, ReferenceView, useReferenceSearch, type ReferenceEnvironment } from '../src/entity-reference-components';
import { ReferenceSuggestionMenu } from '../src/reference-suggestion-menu';
import raymond from '../../web/wwwroot/images/characters/raymond/neutral.webp';

if (!['localhost', '127.0.0.1'].includes(window.location.hostname)) throw new Error('This fixture is for loopback development only.');
const target = { kind: 'notion-page', id: '773b47a2-c883-45aa-aeb0-78c442582b1d' };
let title = 'SQL :raymond: 🗄️';
const originalFetch = window.fetch.bind(window);
window.fetch = async (input, init) => {
  const url = new URL(typeof input === 'string' ? input : input instanceof URL ? input.href : input.url, window.location.href);
  if (url.pathname === '/__title-qa/references/resolve') {
    return Response.json([{ reference: target, title, href: `/notion/${target.id}`, contextLabel: 'Fiktiv teoriside' }]);
  }
  if (url.pathname === '/__title-qa/references/search') {
    return Response.json({ items: [{ reference: target, title, href: `/notion/${target.id}`, contextLabel: 'Fiktiv teoriside' }], total: 1 });
  }
  return originalFetch(input, init);
};
const mention = createReactInlineContentSpec({ type: 'entityMention', propSchema: { kind: { default: '' }, id: { default: '' } }, content: 'none' }, {
  render: ({ inlineContent }) => <ReferenceView reference={inlineContent.props} />,
});
const schema = BlockNoteSchema.create({ inlineContentSpecs: { ...defaultInlineContentSpecs, entityMention: mention } });
function Document() {
  const search = useReferenceSearch();
  const editor = useCreateBlockNote({ schema, initialContent: [{ type: 'paragraph', content: ['Læs også ', { type: 'entityMention', props: target }, '.'] }, { type: 'paragraph', content: '' }] });
  const [snapshot, setSnapshot] = useState('');
  return <section className="notion-blocknote-host">
    <h2>Mentions i dokumentet</h2>
    <p>Skriv @SQL i den tomme linje, og vælg siden med tastaturet.</p>
    <BlockNoteView editor={editor} theme="light" slashMenu={false} formattingToolbar={false} sideMenu={false} emojiPicker={false} onChange={() => setSnapshot(JSON.stringify(editor.document, null, 2))}>
      <SuggestionMenuController triggerCharacter="@" suggestionMenuComponent={ReferenceSuggestionMenu} getItems={async query => (await search?.search(query))?.items.map(item => ({ title: item.title, subtext: item.contextLabel, group: 'Indhold i LUP', onItemClick: () => editor.insertInlineContent([{ type: 'entityMention', props: item.reference }, ' ']) })) || []} />
    </BlockNoteView>
    <h2>Sidelink</h2><ReferenceView reference={target} block />
    <details><summary>Gemte reference-id'er</summary><pre aria-label="Gemte dokumentdata">{snapshot || 'Skriv en mention for at se dokumentdata.'}</pre></details>
  </section>;
}
function App() {
  const [loaded, setLoaded] = useState(false);
  const [revision, setRevision] = useState(0);
  const [currentTitle, setCurrentTitle] = useState(title);
  const emojis = useMemo(() => loaded ? [{ id: '9392c410-6d6a-4088-a6e6-2ea4de8125a2', name: 'raymond', contentUrl: raymond, altText: 'Raymond' }] : [], [loaded]);
  const environment: ReferenceEnvironment = { sourceKind: 'notion-page', sourceId: '450c040e-100a-4a11-b01c-b6c5fb264433', apiBase: '/__title-qa', canEdit: true, revision, getToken: async () => '' };
  return <EmojiCatalog.Provider value={emojis}><ReferenceProvider environment={environment}>
    <style>{`body{margin:0;background:#f5f6fa;color:#182238;font:16px/1.6 system-ui}main{max-width:980px;margin:30px auto;background:white;border:1px solid #d8dfeb;border-radius:14px;padding:28px}h1{font-size:30px;margin:0 0 14px}h2{font-size:19px;margin:24px 0 10px}button{font:inherit;border:1px solid #cfd7e7;background:#f5f7ff;border-radius:7px;padding:8px 12px;cursor:pointer}nav{padding:14px;background:#f0f3fb;border-radius:8px}a{color:#2452e8}header{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:20px}pre{max-height:240px;overflow:auto;background:#f5f6fa;padding:12px;font-size:12px}.bn-container{border:1px solid #e0e5ef;border-radius:8px}.notion-entity-link{padding:12px;border:1px solid #d8dfeb;border-radius:8px}.notion-emoji-text{white-space:inherit}`}</style>
    <main><h1>Emojis i sidetitler</h1><header>
      <button type="button" onClick={() => setLoaded(value => !value)}>{loaded ? 'Skjul emojibibliotek' : 'Indlæs emojibibliotek'}</button>
      <button type="button" onClick={() => { title = 'Ny titel :raymond: 👩‍💻'; setCurrentTitle(title); setRevision(value => value + 1); }}>Skift sidens titel</button>
    </header>
    <nav aria-label="Sidens navigation"><EmojiText text={currentTitle} /></nav>
    <p>Ukendt emoji: <EmojiText text=":mangler: 🧑‍💻" /></p>
    <p>Tekst, ikke HTML: <EmojiText text={'<img src=x onerror=alert(1)> :raymond:'} /></p>
    <Document />
    </main>
  </ReferenceProvider></EmojiCatalog.Provider>;
}
createRoot(document.getElementById('root')!).render(<App />);
