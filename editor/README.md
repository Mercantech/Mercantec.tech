# LUP block editor

Self-hosted React editor island for the existing Blazor app. Uses BlockNote (MPL-2.0), Yjs13 and Hocuspocus4 (MIT). No XL packages, external collaboration service, browser document database or editor subscription.

## Build

Requires Node.js22 or newer. From this directory:

~~~sh
npm ci
npm test
npm run build
~~~

Build writes editor.js, editor.css, lazy-loaded chunks and THIRD-PARTY-NOTICES.txt to ../web/wwwroot/notion-editor/. The production web Docker build performs this step before publishing Blazor. The .npmrc selects explicit Yjs13 dependencies, bypassing npm's auto-installation of BlockNote's optional Yjs14 peer family. Keep all three BlockNote packages at the same pinned version.

## Blazor adapter

Load /notion-editor/editor.css and import /notion-editor/editor.js dynamically. Exported functions:

- mount(element, context, accessToken, dotnetCallbacks): initializes a document and waits for authorized server sync before displaying the editor.
- setContext(element, context): refreshes roster, polls, title and userName for the same page and hold.
- hasUnsavedChanges(element): true while local edits await a matching durable storage acknowledgement or a media upload is still running.
- setPresentation(element, true/false): shows or closes a snapshot of the document as slides.
- destroy(element): disposes the React root, WebSocket, Yjs document and browser listeners. Confirm or prevent navigation first when there are unsaved changes.

Context shape: {pageId,holdId,title,canEdit,members:[{id,displayName}],polls:[{id,title,state}],updatedAtUtc,userName?}. Member IDs are MembershipId, including Lectio members without an account. Names are resolved from the current authorized context and not stored in the shared document.

DotNet object methods:

- OnNotionStatus(status, detail): connecting, synced, saving, saved, offline or error. synced means connected; saved means a persisted snapshot matches the current Yjs state.
- GetNotionToken(): returns a current access token for connections and private media access, including authenticated read-only students.

WebSocket: same-origin /collaboration?schemaVersion=4, room notion:<pageId>. A missing or incompatible client version is rejected before document loading; the current editor asks the user to reload. The server authorizes each room against ASP.NET. A server stateless {type:'persisted',fingerprintHash:<sha256 hex>,savedAtUtc:<ISO>,stateVector:<base64>} acknowledges storage. The client compares a canonical SHA256 fingerprint of sorted state-vector entries and normalized DeleteSet ranges plus its local edit generation before clearing dirty state. This includes deletions, which a state vector alone cannot detect, and remains stable when server garbage collection encodes deleted structs differently from client UndoManager retention. A persistenceError event preserves dirty state and displays a storage error until a matching successful acknowledgement arrives.

## Custom blocks

- /elever: live authorized roster, optional selected MembershipId references. Always excluded from slides, including nested rosters.
- /afstemning: reference to an existing poll from the same hold. Opens the existing activity or presenter page; poll answers remain in the existing API.
- /slide: real slide boundary. Presentation is a frozen editorial snapshot with keyboard navigation.
- /læringsmål: formatted inline learning goal.
- /diagram (aliases: mermaid, flowchart): Mermaid diagram rendered without chrome; double-click to edit source.
- /callout: highlighted note (info / tip / warning / important).
- /kodeblok (default BlockNote code block): language picker (C#, JS/TS, Python, …), Shiki syntax highlighting via `@blocknote/code-block`, and a **Kopiér** button. Default language is C#.
- /ai (toolbar **AI**): teacher-only OpenAI helper — generate blocks, rewrite selection, or Mermaid via `POST /api/ai/editor` (API proxy; `OPENAI_API_KEY` never in the browser). Default model `gpt-6-luna` (`OPENAI_MODEL`).
- Drag handle ▤ → **Omdan til** (H1–H3, citat, lister, kode, callout, læringsmål) or **Slet**. Selection toolbar also offers block type change.

Each custom block stores schemaVersion1. Add future schema migrations deliberately; never seed collaborative initialContent independently in each browser. Editing pauses on connection loss and pending edits stay in memory in the open tab. A beforeunload listener warns before losing unpersisted local edits. No persistent offline editing is enabled.

## Browser QA fixture

Install collaboration dependencies first, then run node tests/qa-harness.mjs. Open http://127.0.0.1:9098/notion in two browser tabs. The fixture runs the real collaboration server on loopback9099 and a fake scoped API with fictitious pupils and memory-only storage. It is never included in production images or API routes and supplies no production authentication bypass. Stop with Ctrl+C.

The browser test is intended to verify slash menus, roster selection, poll links, undo/redo, slide privacy, collaboration, save acknowledgements and reload. Unit tests verify snapshot privacy, stable roster relations and poll URL validation.

## Source and license notices

The build generates public/THIRD-PARTY-NOTICES.txt from locked runtime packages, including license texts and source links. It is copied to the served assets. BlockNote source for the shipped version is linked there; project additions live in this directory. Preserve these notices in redistributions and update them with dependency changes.

## Udgivne sider, links, grupper og medier

`mountReadOnly(element, context, accessToken, callbacks)` anvender kun `publishedStateBase64` fra det autoriserede publicerings-API. Det opretter en lokal Y.Doc uden WebSocket-provider, så lærerens nye kladdeændringer ikke kan nå elevvisningen. Brug samme schema til redigering og læsning.

`getPublicationData(element)` returnerer asynkront `{ expectedFingerprint, assetIds }`. Kaldet afvises ved afventende lagring, upload eller samtidige ændringer under hashberegningen. Værten sender også `expectedRevision` fra frisk API-kontekst; API'et kopierer kun den tilsvarende lagrede kladde.

`context.pages` består af `{ id, title, parentPageId }` fra holdets autoriserede sider. `@` indsætter ID-baserede links til sider eller enkelte elever. `/side` indsætter en linkblok med sidevalg. Titler og elevnavne slås op fra konteksten, så ukendte eller ikke-udgivne mål ikke viser skjulte etiketter i elevvisningen.

`context.groups` kan indeholde `{ id, name, members:[{ id, displayName }] }`. Elevblokkens gruppeknap kalder `OnNotionCreateGroup(membershipIdsJson)`; værten viser gruppedialogen og gemmer gennem holdets gruppe-API. Opdatér konteksten med `setContext` efter oprettelse. `/gruppe` henviser til en gemt gruppe.

`/video`, `/billede`, `/lyd` og `/fil` indsætter uploadblokke. Uploadgrænsen er 100 MiB. Dokumentet gemmer kun assetId/pageId/kind. Medieadgang hentes med normal Bearer-auth og udløbende URL'er fra `/api/notion/assets/{id}/access`, der fornys før udløb. MP4/WebM, lyd og billeder bruger browserens egne medieelementer; der er altid et downloadlink, og der foretages ingen automatisk videokonvertering.

Til browser-QA kan `tests/qa-publishing-harness.mjs` køres med Node 22. Den binder kun loopback og anvender fiktive elever. `/notion` er lærerkladde, knappen Udgiv gemmer en testsnapshot, og `/student` anvender den isolerede læsevisning. Tallet for WebSocket-forbindelser ændres ikke, når elevvisningen åbnes. Denne fixture afprøver ikke den rigtige database-/filautorisation; disse dækkes af API-testene.
