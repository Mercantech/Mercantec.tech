# Avatarer og oplåsning

`/profile/character` er en avatarsamling. Den tidligere garderobe og valg af
hud, hår og tøj er erstattet af hele figurer, som låses op og vælges.

## Figurer

- `starter`: gratis startfigur.
- `bip01`:BIP-01, level 2 og 150 guld. Oplåsning vælger robotten med det samme.
- Tolv felter med spørgsmålstegn viser kommende figurer. De er information,
  ikke købsknapper eller katalog-id'er, der kan låses op.

Krav og priser findes i den delte serverkontrakt `shared/Avatars.cs`.
`web/Demo/AvatarCatalog.cs` indeholder figurernes beskrivelser og replikker.
Serveren validerer level, ejerskab og saldo. Et gentaget oplåsningskald
trækker ikke guld igen. Kun kendte, ejede figurer kan vælges.

## Gemning og konto

Avatarvalg, oplåste figurer og guldsaldo gemmes pr. autentificeret bruger
i PostgreSQL. Identiteten kommer fra login-tokenets subject. Ingen request
kan vælge en anden bruger via en body eller en URL-parameter.

Tabellerne `avatar_profiles`, `avatar_ownerships` og
`avatar_item_ownerships` oprettes af EF-migreringen
`20261108065614_AvatarProfiles`. API'et anvender migrationer ved opstart.
Profilen oprettes ved første indlæsning. En ny computer henter den fra API'et.

- `GET /api/me/avatars`: hent/opret profil.
- `POST /api/me/avatars/{id}/unlock`: betal, lås op og vælg.
- `PUT /api/me/avatars/selection`: vælg en ejet figur.
- `POST /api/me/avatars/items/{id}/buy`: køb et møbel med samme saldo.
- `POST /api/me/avatars/import`: begrænset engangsoverførsel fra gammel browser.

Betaling, ejerskab og valg gemmes atomisk. En revisionskolonne og unikke
nøgler beskytter mod samtidige køb og dobbeltbetaling. Klienten sender også
`X-LUP-Avatar-Subject` med den konto, handlingen startede for; serveren afviser
et mismatch med tokenet, så et kontoskift under tokenfornyelse ikke udfører
handlingen på den nye konto.

`AvatarClient` bruger platformens autentificerede HttpClient.
`DemoWorld.BindAvatarProfileAsync` indlæser serverprofilen ved login/kontoskift.
Sene svar fra en tidligere konto ignoreres, og udestående kald afbrydes.
Valg og betaling vises først som gennemført efter serverens bekræftelse.
Netværksfejl vises med genforsøg; der er ingen lokal skrivefallback.

### Eksisterende browserdata

`lupAvatars.get` læser kun gamle kontospecifikke
`lup-avatar-profile-v1:user:{subject}`-poster. Nye avatarvalg gemmes aldrig
via localStorage. Når en ny databaseprofil stadig tillader overførsel og den
aktuelle konto har en gammel browserpost, sender klienten den til import-API'et.
Kun kendte figurer med opfyldte levelkrav accepteres, og valg skal være ejet.
Guld kan højst svare til startsaldoen minus importerede avatarers pris og kan
ikke hæve en eksisterende saldo. Efter import eller første vellykkede køb/valg
lukkes importmuligheden. Browserposten fjernes efter bekræftet servertilstand.
Gamle data i en anden browser kan derefter ikke overskrive kontoen.

En tom ny browser importerer ikke en tom profil. Derfor kan brugeren stadig
åbne sin gamle browser og få tidligere avatarer overført, indtil vedkommende
foretager første ændring på kontoen. Gæstedata og den gamle globale world-
snapshots avatarfelter overføres aldrig til en konto.

Level og XP bruger fortsat platformens eksisterende demoprogression.
Serverprofilens udgangspunkt er level 7 og 320 guld; serveren stoler ikke på
browserens level eller nye saldi. Nye møbelkøb bruger samme databasesaldo som
avatarer. Gamle rumindretninger og tøjdata er fortsat browserdata. Dette flytter
ikke hele lejligheds- eller XP-systemet til databasen.

## Visning og grafik

`AvatarSprite` bruges af samlingen, elevsiden, lejligheden og prøvespillet.
Den viser V3's rene robotark med otte frames i 4 × 2, hver 256 × 256 px.
`AvatarId` kan bruges til en forhåndsvisning, og `Animation` vælger et kendt
animationsnavn. Ugyldige animationsnavne bruger `idle`.
Reduceret bevægelse viser første frame.

`AvatarIcon` viser ansigtet og bruges i headeren og profilen. Parametre:
`AvatarId`, `Size`, `Expression` og `Decorative`. Udtryk:
`happy`, `wink`, `think`, `celebrate`. Emoji- og profil-PNG kan hentes på
avatarsiden. Ikonet indeholder kun hovedet; det indlæser ikke et kropsark.

Runtimefiler ligger i `web/wwwroot/assets/avatars/bip-01/`:

- `sprites/`:28 karakterark uden indtegnede genstande/projektiler/eksterne FX.
- `face.png` og`emoji-*.png`:transparente 512 px-ansigter.
- `audio/`:28 lydeffekter og fem eksisterende danske replikker.
- `manifest.json`:runtimeoversigt over filer, bevægelser og events.

Genstande og projektiler til kommende spil skal fortsat oprettes som
separate entities. Den valgte avatar giver en figurvisning, ikke en kampmotor.
Kildedesign, præcise ImageGen-prompter og ikontests findes i workspace-mappen
`design/robot-avatar-v3/icons/` ved siden af repositoryet.

Butikken viser møbler og linker til avatarsamlingen. Legacy-tøjdata er
bevaret for ældre rum/world-snapshots, men garderoben er ikke længere et
brugerflow. Oplåsningsoversigten viser robotten og kommende møbler.

## Verifikation

```text
dotnet build web
dotnet test tests/Mercantec.Tech.Api.Tests
dotnet test tests/Mercantec.Tech.Web.Tests
node --test tests/lup-ui.test.cjs tests/lup-avatars.test.cjs
node tests/qa-avatar-collection.cjs
```

API-testene bruger rigtige, isolerede SQLite-filer og nye databaseforbindelser
for at kontrollere vedvarende data, kontoadskillelse, revisionskonflikter,
samtidig oplåsning og engangsoverførsel. Web-testene dækker klientruter,
fejl, kontoskift og afvisning af sene svar.

Browser-QA kræver en lokal platform på 3000, Playwright og Chrome. Den bruger
to isolerede browserkontekster med en fælles API-testdouble for at teste den
samlede brugerflade og HTTP-kobling. Ingen rigtige konti, tokens eller elevdata
bruges. Testen dækker valg på tværs af enheder, fejl og genforsøg, migration,
fælles saldo, ikoner, emojis, lyd, profil/lejlighed/spil, tema og mobilvisning.
Den erstatter ikke databaseintegrationstestene. Rapport og screenshots gemmes
uden for repoet i `design/robot-avatar-platform/`.
