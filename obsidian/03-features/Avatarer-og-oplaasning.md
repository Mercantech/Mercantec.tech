# Avatarer og oplåsning

`/profile/character` er en avatarsamling. Den tidligere garderobe og valg af
hud, hår og tøj er erstattet af hele figurer, som låses op og vælges.

## Figurer

- `starter`: gratis startfigur.
- `bip01`: BIP-01, level 2 og 150 guld.
- `raymond`: Raymond / Professor Codd, level 3 og 150 guld. Oplåsning vælger figuren med det samme.
- `santa`: Julemanden, level 4 og 150 guld samt julegaver sendt til mindst tre forskellige brugere.
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
Kun de oprindelige browserfigurer (`starter`, `bip01`) med opfyldte levelkrav accepteres, og valg skal være ejet. Raymond og fremtidige avatarer kan ikke erhverves gennem legacyimport.
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

## Raymond

Raymond har nu samme komplette28-handlingspakke som robotten: otte fuldkropsframes pr. handling i4 × 2,28SFX og fem indtalte danske replikker. Masterark er2048 × 1024 med512px-frames; appen bruger1024 × 512 med256px-frames. Der er fem evner (Snabelpuls, Relationsgreb, Tryghedsskjold, Elefantstorm og Gendannelse) samt Den store JOIN med separat opladning og udløsning.

`AvatarCatalog.Actions` beskriver label, gruppe, FPS, loop, sluttilstand, lydfil og beskrivelse. `AvatarSprite` spiller de otte rigtige frames og holder one-shots på den sidste frame. Pauser og genafspil er eksplicitte, og reduceret bevægelse viser én stillestående frame. Alle28 bevægelser og lyde kan prøves i samlingen; lyd starter kun på brugerens klik.

`AvatarLayerPreview` indlæser de kuraterede lokale lagkontrakter og synkroniserer med aktørens faktiske CSS-tid. Pickup kan vise bog eller mønt med samme figurark og en kalibreret håndkurve; Snabelpuls sender et separat projektil videre på eget verdensur efter figurens sidste frame. Lag kan slås fra, pauses og genafspilles; figurskift og navigation rydder billeder, timere og RAF. Dette er et asset-/lag-preview, ikke en ny skades- eller kampmotor.

Ansigtsikon og fire reaktioner eksporteres som transparente512px-PNG fra den eksisterende karaktergrafik. Hele grafik- og lydleverancen, prompter og kontrolmetode er beskrevet i [grafikdokumentationen](../../assets/avatars/raymond/README.md). Den komplette lokale ZIP indeholder også højopløste masterark, kilder,28animeredeWebP'er, standalone-preview og audio-demo.

Raymonds ejerskab, valg og pris er uændret (level3,150guld). Eksisterende ejere får den udvidede pakke uden en ny betaling. Ingen schema-migrering er nødvendig. Legacyimport kan fortsat kun tildele starter/BIP-01. Store offentlige avatarfiler hentes ved brug og kan derefter bruges offline fra assetcache; personlige API-profiler caches aldrig.


## Julemanden

Julemanden har id `santa`, kræver level 4, koster 150 guld og kræver gaver sendt i juletemaet til mindst tre forskellige brugere. Oplåsning og valg bruger den eksisterende kontoprofil og følger med mellem computere. Santa kan ikke tildeles gennem gamle browserdata. Allerede oplåste figurer bevares.

Aktivitetskrav beskrives i `AvatarUnlockDefinition.Requirements` i den delte kontrakt, så nye figurer kan få andre krav. Serveren beregner `AvatarProfileDto.UnlockProgress` fra gemte aktiviteter og kontrollerer alle krav før betaling. Klienten kan vise fremskridt, men kan ikke tildele dem. Manglende fremskridtsdata giver ikke adgang til en figur med aktivitetskrav.

Julegavekravet tæller unikke, andre modtager-id'er for den aktuelle afsender på tværs af hele gavehistorikken. Kun gaver med `Theme == "christmas"` tæller; modtageren behøver ikke åbne dem. Dubletter, gaver til én selv, andre afsendere og andre temaer tæller ikke. Temaet fastholdes ved første afsendelsesforsøg og gemmes i databasen, så et genforsøg ikke kan ommærke en eksisterende gave. Ældre gaver uden gemt tema migreres til `unknown` og giver ikke udokumenteret julegavefremskridt. Gavehistorikkens nye temafelt kræver en EF-migrering.

Samlingen viser eksempelvis `2 / 3 forskellige brugere`, opdaterer efter en vellykket gaveafsendelse og har en manuel knap til at hente ny status fra en anden enhed. Et afvist oplåsningsforsøg ændrer hverken saldo, ejerskab, valg eller revisionsnummer.

Pakken har samme 28 handlinger og 224 tegnede frames som BIP-01/Raymond, 28 originale lydeffekter, fem indtalte danske replikker, ansigtsikon og fire emojis. Stjerneskud, Gavegreb, Julefred, Kanesus og Julevarme er de fem evner; Den store gavefest har separate opladnings- og udløsningsark. Genstande, gyldne stjerner, magiske snebolde og effekter er selvstændige sprites. De kalibrerede hænder og projektilstartpunkter kommer fra Santas egne ark.

Runtime ligger under `web/wwwroot/assets/avatars/santa/`. Den komplette lokale pakke ligger i `design/santa-avatar-v1/` ved siden af repositoryet. Lydkilder og den faktiske lokale talestemme er dokumenteret i `assets/avatars/santa/AUDIO.md`. Lyd kræver et eksplicit klik i avatarsamlingen.

Den fælles browserkontrol kan køres mod Santa med `LUP_QA_AVATAR=santa`; uden parameter kontrollerer den fortsat Raymond. Begge forløb indeholder de eksisterende robot- og kontokontroller.
