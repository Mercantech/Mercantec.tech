---
type: feature
status: implemented
tags: [games, avatars, multiplayer]
---

# LUP Arena

LUPs eget platformskampspil ligger på `/arena` (og `/games/arena`). Der er links fra profil, kontomenu og spiloversigt. Spillet bruger den valgte profilavatar som udgangspunkt: startfiguren, BIP-01, Raymond eller Julemanden. Kun kontoens oplåste avatarer kan vælges til onlinekampe.

## Grafik og hovedmenu

Rettelse af kampeffekter: v2-effektatlasset havde halvtransparente matte felter og nabografik i flere celler. Arenaen bruger i stedet de originale, individuelt isolerede effekter og projektilark for BIP-01 og Raymond, eksporteret tabsfrit til `web/wwwroot/assets/arena/v3/effects/`. Alle 20 filer og 32 effektframes har alpha 0 langs en kant på otte pixels. De 17 relevante originale kampsheets pr. figur er koblet på igen, med otte frames pr. handling. Starterfigurens v2-art bevares. `arena-avatar-art.js` håndterer figurark; `arena-effect-art.js` håndterer karaktereffekter og ejerskab.

BIP-01 og Raymond har hver deres serverberegnede kampsæt. De eksisterende spritesheets og effekter viser handlingerne; skade, kræfter, ressourcer og cooldowns beregnes i den fælles C#-motor.

## Unikke evner

BIP-01 er en hurtig tekniker med en energimåler. Pulsbolten flyver hurtigt; Magnetgreb trækker fjender ind og opsamler fjendtlige projektiler som energi. Bobleskjold reflekterer projektiler i de første seks frames. Raketryk er et kort, hurtigt angreb med beskyttet afsæt, og Rakethop giver et stejlt redningsløft. Reparationsfelt bruger energi på en kort, afbrydelig heling. Kerneoverload kræver fuld evnemåler, rammer omkring BIP og tømmer energien.

Raymond er en tung kontrollør med større rækkevidde og langsommere, stærkere projektiler. Relationsgreb fastholder én modstander; retning og U/J kaster. Gentagne hop kan bryde grebet, og en tredje spillers træffer kan frigøre den fangede. Tryghedsskjold absorberer projektiler og lader evnemåleren. Elefantstorm varer længere og kan modstå én træffer med reduceret skade. Snabelløft er et tungere, bredt redningsangreb. Gendannelse bruger skjold på en langsommere, afbrydelig heling. Den store JOIN sender en komet, som trækker fjender sammen og eksploderer.

Julemanden bruger sin egen julemagi, som gendannes med 6 pr. sekund. Stjerneskud koster 14 magi og flyver i en let bue; Gavegreb koster 20 og løfter gennem skjold uden fastholdelse. Julefred absorberer projektiler til 10 magi. Kanesus er et svævende ryk til 22 magi, mens Skorstensspring bruger 16 på redningsløftet. Julevarme koster 45 magi og heler 12 skade samt 12 skjold efter en afbrydelig pause på 0,8 sekunder. Den store gavefest bruger fuld evnemåler og sender tre selvstændige magiske snebolde i en vifte; opladningen kan afbrydes, og cooldown er 11 sekunder. Computerstyringen bruger samme serverkontrakt og ressourcekrav. Santa-grafik indlæses fra de originale avatarark; ti separate Arena-effekter/projektilstrimler ligger sammen med de andre karakterers v3-art.

Træffere lader de store evner; heling og passiv ventetid gør ikke. Evnerne har separate cooldowns. KO, udmeldelse og kampnulstilling frigør greb og rydder aktive evnetilstande. Genforbindelse nulstiller inputsekvensen uden at genopfylde ressourcer. Lokal træning starter med fuld evnemåler; onlinekampe starter tomme, og klienten kan ikke tilvælge træningsressourcer.

Den valgte figurs taster og evner findes under Kæmpere og Indstillinger. Kampens HUD viser ressourcer, evnemåler, cooldowns, reparation og fastholdelse.

Arenaen har et separat skærmfyldende spillayout med hovedvalg til online, træning, kæmpere og indstillinger. Styring ligger i indstillingerne. Lokal træning sættes på pause, når indstillingerne åbnes. Genererede rasterassets i `web/wwwroot/assets/arena/v2/` erstatter de proceduretegnede baggrunde, platforme, effekter og startfigurer.

14 WebP-filer fylder cirka 7,7 MB: menubillede, tre banebaggrunde, platform- og effektatlas, fem fighteratlasser og tre portrætter. Figurarkene har 80 validerede frames med fast fodplacering. Startfiguren understøtter kort hår, langt hår og kasket; profilens farver anvendes som materialefarver. PNG-kilder og præcise image_gen-prompter ligger i `assets/arena/v2/` og hentes ikke af browseren.

Menutests dækker navigation, låste figurer, login, lydvalg, manglende grafik, evneoversigt og lokal træning. Direkte software-rendering med de rigtige teksturer dækker kampens effekter og HUD; browserens fulde UI verificeres separat fra disse grafikkontroller.

## Spil

To til fire kæmpere konkurrerer om at blive på banen. Skadeprocenten øger tilbageskubbet; en tur uden for banens grænser koster et liv. Sidste overlevende vinder. Tidsgrænsen afgør ved liv/skade; en lige stilling går i sudden death. Kampen afsluttes med resultat og mulighed for revanche.

Kampmotoren omfatter acceleration, luftstyring, dobbelthop, hopbuffer, coyote time, envejsplatforme, kantgreb, normale og retningsbestemte angreb, luftangreb, opladede kraftslag, projektiler, specialangreb, opadgående redningsangreb, skjold, parering, skjoldbrud, undvigelser, greb/kast, hitstop, hitstun, påvirkning af flyveretning, genopståen og kort beskyttelse efter genopståen. Der er tre forskellige baner: Over tagene, Værkstedet og Skyøerne. CPU-spillere kan bevæge sig, angribe og forsøge at vende tilbage til banen.

Træning er tilgængelig uden login og kører samme C#-motor i Blazor WASM. Ingen separat browserfysik skal holdes synkron med serveren. Onlinekampe kræver det eksisterende Mercantec-login.

## Øvebane og kampstart

Træning kan åbnes som en øvebane uden tidsgrænse eller som en kamp mod en computer. Øvebanen kan begynde midt på banen, ved kanten eller uden for den. Modstanderen kan stå stille, holde skjold, gentage et valgt træk eller kæmpe med en af tre sværhedsgrader. R og Nulstil gendanner startpositionerne. Fyld op genopfylder ressourcer i øvebanen.

Korte tryk gemmes mellem netværksbeskeder og simulationstrin. Angreb, special og greb kan ligge klar i op til seks simulationstrin, mens figuren lander eller bliver klar igen. Retningen på specialtrykket bevares.

Menuen henter forhåndsvisning. Kampen varmer grafik til den valgte bane og deltagerne med en kort ventetid og bruger reservegrafik ved manglende filer. Kampens HUD viser læsbare målere på små skærme samt grebets resterende tid og hop.

## Styring

| Handling | Tastatur | Standardcontroller |
| --- | --- | --- |
| Bevægelse/retning | A/D og W/S eller piletaster | Venstre pind |
| Hop/dobbelthop | Space | A |
| Angreb | J | X |
| Special | K; W+K redder opad | B + retning |
| Skjold | L | LT/RT |
| Undvig | Shift | LB |
| Nulstil øvebane | R | Nulstil-knappen |
| Grib/kast | U | Y |
| Stor evne (BIP/Raymond) eller kraftslag (startfigur) | Hold I, slip I | Hold RB, slip RB |

Touchknapper inkluderer retning, hop, angreb, special, skjold, undvigelse, greb og kraftslag. Lyd kan slås til; fuld skærm åbnes fra kampbanens værktøjslinje.

## Lyd

Lyd er som standard slået til. Lydstyrken gemmes lokalt, og de fire figurpakker bruger deres egne replikker ved kampstart og resultat. Talelyd dæmper kamplydene kort, mens underteksten vises. Browserens AudioContext oprettes og genoptages direkte i et betroet klik eller tastetryk før Blazor-interoperabilitet. Lydknapperne i toppen og i Indstillinger gemmer valget lokalt og stopper igangværende klip ved mute. Skjult fane pauser lyden; en ny interaktion kan genaktivere den.

BIP-01, Raymond og Julemanden bruger deres egne originale WAV-klip for bevægelse, træffere og evner. Kun kampens relevante klip indlæses efter aktivering. Startfiguren og manglende lydfiler har en lokal syntetiseret reserve. Lyden bruger en fælles volumenbegrænser, maksimalt 12 samtidige stemmer og ingen gentagen lyd for kontinuerlige trækfelter. Afbrudt heling og opladning stopper deres klip.

16 Node-tests dækker browseraktivering, optagede klip, reserver, mute, samtidighed, ejerskab og oprydning. Reel browserkontrol har dekodet 39 originale kampklip og målt ikke-nul lydoutput for alle tre figurpakker og startfiguren; mute giver nul output.

## Onlineflow

1. Vælg en oplåst figur og bane. Vælg liv, minutter og eventuelt et offentligt rum.
2. Opret rummet og kopiér invitationen, eller giv vennerne rumkoden på seks tegn.
3. Vennerne logger ind på deres egne computere og åbner invitationen eller indtaster koden.
4. Alle markerer sig klar. Værten starter kampen. Værten kan tilføje CPU-spillere op til i alt fire kæmpere.
5. Efter kampen kan værten vælge revanche, som åbner lobbyen igen.

Der er automatisk genforbindelse og 30 sekunders frist til at genoptage sin plads. Værtsrollen flytter til en anden menneskelig spiller, hvis værten forlader rummet. Værten kan fjerne en inaktiv deltager efter 90 sekunder i lobbyen eller 45 sekunder i kampen. En afbrudt spiller kan fjernes efter 10 sekunder. Serveren genkontrollerer aktiviteten ved fjernelse. Udtrykkelig udmeldelse sendes både på forbindelsen og gennem det godkendte HTTP-endpoint. En sen genforbindelse kan ikke genåbne et rum, som brugeren har forladt.

## Avatarer og visning

BIP-01 og Raymond bruger de eksisterende 4×2 spritesheets med otte frames pr. handling. Startfiguren tegnes med profilens hudfarve, hår, frisure og tøj. Disse kosmetiske valg valideres og videresendes i lobby og kampsnapshots, så modstanderne ser samme udseende. Ejerskab kontrolleres mod AvatarProfiles/AvatarOwnerships på serveren; browseren kan ikke åbne låste figurer. Udseendet påvirker ikke hitboxes eller kampfysik.

## Arkitektur

- `shared/Arena.cs`: input, stadier, kosmetik og snapshots.
- `shared/ArenaMoveSets.cs`: korte danske kontrol- og evnebeskrivelser til menuerne.
- `api/Arena/ArenaSimulation.cs`: transportuafhængig simulation ved 60 faste trin i sekundet. Filen kompileres også direkte ind i webprojektet til lokal træning.
- `api/Arena/ArenaRoomService.cs`: midlertidige rum, forbindelser, adgangsbilletter, 60 Hz kamp og 20 Hz snapshots.
- `api/Controllers/ArenaController.cs`: eksisterende JWT-login, ejerskab og WebSocket-adgang.
- `web/Pages/Arena.razor*`: dansk UI, profilintegration og lokal træningsløkke.
- `web/wwwroot/js/arena-client.js`: tastatur/controller/touch, interpolation, forbindelser og genforbindelse.
- `web/wwwroot/js/arena-renderer.js`: Canvas 2D-baner, sprites, kamera, effekter og HUD.

Serveren modtager kun input. Positioner, skade, liv, træffere og vindere beregnes på serveren. Der er ingen klientindsendte kampresultater. Transporten bruger en kortlivet engangsbillet fra et autentificeret HTTP-kald; det egentlige login-token placeres ikke i WebSocket-URLen. Der er origin-kontrol, beskedgrænser, inputsekvenser og oprydning. Kosmetik tillader kun sekscifrede hex-farver og de kendte frisurer.

Serveren sender seneste kampsnapshot til hver forbindelse. Klienten interpolerer modstandere med buffer til ujævne leveringstider og fremskriver den lokale figur kortvarigt. Det er ikke en rollback-netcode. Høj netværksforsinkelse vil derfor kunne mærkes; ping vises i kampbanen.

Kamprum findes i den aktuelle API-proces og er begrænset til 128 samtidige rum. En API-genstart afslutter igangværende kampe. Den eksisterende Docker-stack kører én API-instans. Flere replikaer kræver session affinity eller en separat fælles room-host; der er ikke tilføjet en ny database eller ekstern spiltjeneste.

## Kørsel og deployment

Fra repositoryets rod:

```sh
dotnet run --project api --launch-profile http
dotnet run --project web
```

Den almindelige udviklingsadresse er http://localhost:3000/arena; API på port 8080 bruger den eksisterende database og loginopsætning.

En databasefri forhåndsvisning af den faktiske produktionspakke:

```sh
dotnet publish web -c Release
node tools/arena-preview.cjs
```

Åbn http://127.0.0.1:3080/arena. Denne forhåndsvisning kan køre lokal træning; onlinekampe kræver den fulde API-stack.

Til produktion bruges den eksisterende Docker Compose/Dokploy-pipeline. API-start registrerer automatisk arenaens hosted service og WebSocket-middleware. Nginx har et særskilt `/api/arena/connect`-endpoint med WebSocket upgrade og tilstrækkelig timeout. Der kræves ingen nye hemmeligheder, domæner, database-migreringer eller Docker-services. Når ændringen er indarbejdet i den branch, Dokploy bygger, udgives hele API- og webpakken samlet efter [Dokploy-drift](../02-ai-workflows/Dokploy-drift.md).

Der er ikke udført deployment fra denne chat: Dokploy-forbindelsen er ikke tilgængelig. `lup.dk`, `www.lup.dk` og `mercantec.tech` er allerede aliases til samme stack.

## Verifikation den 9. november 2026

- Samlet API-regression: 586 bestået, 0 fejl, 0 sprunget over.
- Web-regression: 562 bestået, 0 fejl, 0 sprunget over.
- Heraf 59 arena-tests, inklusive 22 nye evnetests og fem nye netværkstests med en rigtig Kestrel-server og to uafhængige WebSocket-klienter.
- Evnetests kontrollerer faktisk position, skade og projektiler: magnettræk, fastholdelse/kast, tre hop for at slippe fri, tredje spillers afbrydelse, refleksion, absorption, panserets første/anden træffer, energi, separate cooldowns, afbrudt heling, forskellige redningsbaner, områdeeksplosion, JOIN-træk/eksplosion, KO, kampnulstilling og determinisme.
- Netværkstests sammenligner hele samme-tick-snapshots på begge klienter, afviser forfalsket kamp-/træningstilstand og forældede input og kontrollerer ressourcer ved genforbindelse.
- 14 Node-tests af sprites, effekter, ejerskab, meter og HUD-tilstande består.
- Native Canvas 2D-kontrol med 64 rigtige assets: 21 nye evnescener på tre baner, HUD med to/fire kæmpere og 72 tidligere sprites-/effektscener. Alle 32 effektframes har helt transparente kanter; alle tegningskoordinater er endelige tal.
- API og web er bygget i Release. Produktionswebpakken er kontrolleret over HTTP for alle 75 arenaressourcer, de præcise testede JavaScript-moduler og evneoversigtens CSS.
- Den nye revision er ikke browserafprøvet på selve lup.dk; netværks- og grafikkontrollerne er kørt lokalt.
