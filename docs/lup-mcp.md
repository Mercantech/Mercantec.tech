# Codex og LUP-notesbøger

LUP-MCP lader Codex finde, læse og redigere hold- og teorisider direkte i kladden samt oprette nye undersider. Forbindelsen virker også, når ingen browsereditor er åben. Codex udfører skrivearbejdet; LUP kalder ikke sin egen AI-model for at omskrive det igen.

Personlige notesbøger, elevnoter, afleveringer, udgivelse og sletning af sider indgår ikke i denne forbindelse. Adgang kontrolleres mod den indloggede undervisers aktuelle rettigheder ved hvert kald.

## Forbindelse

- MCP-adresse: `https://lup.dk/api/mcp`.
- Transport: Streamable HTTP.
- OAuth-udsteder: `https://auth.mercantec.tech`.
- Offentlig OAuth-klient: `lup-codex`, navn **Codex · LUP**. Klienten kræver PKCE S256 og har ingen client secret.
- Anmodede scopes: `openid profile offline_access`.
- Lokal callback-port: `58217`, kun på loopback.
- Registreret callback til standardadressen med Codex CLI `0.162.1`: `http://127.0.0.1:58217/callback/uiZRnXVpgm6w`.

Klienten er registreret separat fra LUP-webklienten. Callback-URI'en skal svare præcist til den adresse, som den installerede Codex-version viser. Codex kan føje et serverafhængigt id til `/callback`; ved ændret serveradresse eller Codex-version kontrolleres den viste adresse igen. En fast port undgår afhængighed af wildcard-registrering af lokale porte.

Serveren annoncerer OAuth protected-resource metadata gennem sin `WWW-Authenticate`-header. Mercantec Auths eksisterende OIDC-discovery beskriver authorization-code- og refresh-flowet. Manglende dynamisk klientregistrering er forventet, da Codex bruger den forhåndsregistrerede klient.

## Tilslut i Codex

Forhåndsvis den nødvendige offentlige konfiguration:

```powershell
./scripts/setup-lup-mcp.ps1
```

Når endpointet er udrullet:

```powershell
./scripts/setup-lup-mcp.ps1 -Configure
codex mcp login lup
```

Opsætningsscriptet kræver en beskyttet, tilgængelig MCP-server og tilføjer kun en ny `lup`-sektion i Codex' `config.toml`. Det overskriver ingen eksisterende forbindelser, ændrer ingen approval-indstillinger og skriver ingen tokens eller client secrets. Hvis `lup` allerede findes, stopper scriptet uden at ændre filen.

Ved første tilslutning registreres Codex' præcise callback på Auth-klienten. Fuldfør derefter normal Mercantec-login. Tokens gemmes af Codex' egen OAuth-lagring. Der er ikke behov for en OpenAI API-nøgle til denne forbindelse.

Efter tilslutning kan `/mcp` vise serveren. CLI, app og IDE-udvidelse bruger den delte lokale MCP-konfiguration; en genstart eller genindlæsning af klienten kan være nødvendig for at få de nye værktøjer ind i en allerede åben samtale.

## Brug

Eksempel:

> Åbn denne LUP-side: https://lup.dk/notion/SIDENS-ID. Gør forklaringen mere forståelig for H1, bevar kode og medier, og tilføj tre øvelser til sidst. Redigér kun kladden.

De seks værktøjer er:

| Værktøj | Formål |
| --- | --- |
| `find_pages` | Find sider i et bestemt hold eller en samling. |
| `read_page` | Læs live-indhold, blok-id'er, formatering og grundlag for ændringer. Store sider har synlig fortsættelse. |
| `create_page` | Opret en tom underside som kladde under en eksisterende side. Forælderens hold og samling arves; samme operations-id genbruges ved genforsøg. |
| `apply_changes` | Anvend en samlet, valideret ændring på én side. Brug et nyt operations-id til en ny ændring og samme id ved genforsøg. |
| `get_change` | Se resultat og før/efter-historik for en konkret ændring. |
| `undo_change` | Fortryd ændringen, hvis det berørte indhold stadig matcher. |

Codex skal læse siden før redigering og kun sende ændringer for de berørte blokke. En konflikt betyder, at målområdet skal læses igen, før Codex prøver en ny ændring. Redigering andre steder på siden ugyldiggør ikke et ellers uændret målområde.

### Opret en underside

`create_page` kræver `parentPageId`, `title` og et nyt `operationId` (UUID). Oprettelsen kræver lærerrolle og aktuel redigeringsadgang til forælderen. Siden arver hold og samling, mens den kaldende bruger registreres som opretter. Den nye side er en tom kladde. Læs derefter siden med `read_page`, og indsæt skabelon eller tekst gennem `apply_changes`, så indholdet følger den normale samarbejdslagring.

Side og privat oprettelseskvittering gemmes atomisk. Ved et tabt svar gentages `create_page` med samme operations-id og samme titel og forælder; svaret peger på den allerede oprettede side. Et operations-id må ikke genbruges til en anden side. Adgang kontrolleres igen ved genforsøg, og en efterfølgende slettet side bliver ikke genskabt automatisk. `get_change` og `undo_change` gælder indholdsændringer; oprettelsens status genfindes ved at gentage `create_page`.

### Ændringsformat

`read_page` returnerer sideoplysninger og `document.blocks`. Hver blok har bl.a. `id`, `type`, `text`, `parentId`, `fingerprint`, `contentFingerprint` og en tabsfri XML-repræsentation i `block`. Brug `document.nextCursor` ved næste `read_page`; `partial` og `totalBlocks` angiver dækningen. `find_pages` bruger tilsvarende `nextOffset`.

Læsning har et serverbudget på 256 KiB pr. svar og fortsætter ved hele blokgrænser. Hvis én blok alene overskrider budgettet, returneres `block_too_large` med blok-id og størrelse; indholdet bliver ikke afkortet. Opsætningsscriptet giver kun `lup.tools.read_page` et `output_token_limit` på `100000`, så Codex' generelle værktøjsbudget ikke klipper det strukturerede læsegrundlag. Det giver ikke MCP-serveren adgang til flere sider eller andre data.

`apply_changes` modtager `pageId`, `operationId`, en valgfri kort `summary` og `changes`. Hver ændring angiver `type`:

| `type` | Nødvendige felter |
| --- | --- |
| `replace_text` | `blockId`, `contentFingerprint`, `from`, `to`, `text`; valgfri `expectedText` som ekstra kontrol. Indeks er UTF-16 ligesom JavaScript. |
| `insert_before` / `insert_after` | Ankerets `blockId` og `fingerprint` samt den nye `block`. |
| `update_block` | Målblokkens `blockId` og `fingerprint` samt `block`. Eksisterende id og underblokke bevares. |
| `delete_block` | Målblokkens `blockId` og `fingerprint`. Beskyttet indhold må ikke slettes. |
| `append_block` | `block`, kun til et helt tomt dokument uden et eksisterende anker. |

Nyt enkelt indhold kan beskrives som `{ "type": "paragraph", "content": "Den nye tekst" }`, med valgfrie `props` og `children`. Brug den tabsfrie XML-repræsentation til struktureret indhold, hvor alle egenskaber skal bevares, f.eks. en eksisterende tabel. En batch gælder én side og valideres samlet før mutation; den er ikke en tværgående transaktion over flere sider.

`apply_changes` og `undo_change` returnerer en kompakt kvittering med bl.a. operations-id og status. `get_change` modtager `operationId` og returnerer som standard også kun metadata; brug `includeAudit: true`, når den fulde, private før/efter-historik er nødvendig. `undo_change` modtager det oprindelige `operationId` og et nyt `reversalId`, som genbruges ved genforsøg på netop denne fortrydelse. En anden instruktion eller ændret payload skal have et nyt operations-id.

Fortrydelse findes også i LUP under **Mere → Codex-ændringer**. Det er en særskilt, kontrolleret tilbageførsel; browserens almindelige Ctrl+Z er ikke historik for ændringer lavet af en ekstern samarbejdsklient.

## Drift og verifikation

OAuth skal verificeres med den faktiske Codex-klient: præcis callback, S256, tokenudveksling, fornyelse med roteret refresh token og de eksisterende `iss`/`aud`-kontroller. En vellykket HTTP-discovery alene dokumenterer ikke hele login-flowet.

Forbindelsen er afprøvet med Codex CLI `0.162.1`, en forhåndsregistreret offentlig klient, PKCE S256 og fast callback-port. Mercantec Auths eksisterende OIDC-discovery kan bruges; dynamisk registrering eller ændringer af Auths globale sikkerhedsregler er ikke en forudsætning for denne klient.

Live-prøven den 8. november 2026 gennem `lup.dk` omfattede Codex-login, alle fem værktøjer med det officielle TypeScript-SDK, pagineret læsning, ændring, gentaget request og privat historik. En separat OAuth-session verificerede refreshrotation, brug af det nye token og afvisning af det gamle refresh token. Efter genstart af API og samarbejdsserver fandtes ændringen fortsat præcis én gang. Den åbne editor modtog ændringen straks, og både LUPs historikmenu og gentaget MCP-fortrydelse blev kontrolleret.

En særskilt demoside blev også redigeret uden en åben editor, genåbnet og fortrudt. Begge midlertidige ændringer blev fortrudt. Den eksisterende demosides otte blokke blev kontrolleret med uændret XML; den oprindeligt helt tomme demoside endte med BlockNotes normale tomme tekstblok efter genåbning.

MCP-svar må først angive en gemt ændring, når samarbejdsserveren har fået en holdbar databasekvittering. Ved afbrudt forbindelse genbruges operations-id'et. Historik skal kunne skelne en gemt ændring fra en afventende eller fejlet ændring.

Smoke-test efter udrulning: læs en demoside med browseren lukket, indsæt en lille tekst, genåbn siden, og fortryd netop denne operation. Gentag et write med samme operations-id og bekræft, at indholdet kun findes én gang. Test samtidig redigering i og uden for målområdet samt tilbagekaldt adgang.

Kilder: [Codex MCP og OAuth](https://learn.chatgpt.com/docs/extend/mcp), [Mercantec Auths integrationsmanifest](https://auth.mercantec.tech/.well-known/mercantec-auth.json).
