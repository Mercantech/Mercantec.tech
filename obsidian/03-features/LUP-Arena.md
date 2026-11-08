---
type: feature
status: implemented
tags: [games, avatars, multiplayer]
---

# LUP Arena

LUPs eget platformskampspil ligger på `/arena` (og `/games/arena`). Der er links fra profil, kontomenu og spiloversigt. Spillet bruger den valgte profilavatar som udgangspunkt: startfiguren, BIP-01 eller Raymond. Kun kontoens oplåste avatarer kan vælges til onlinekampe.

## Grafik og hovedmenu

Arenaen har et separat skærmfyldende spillayout med hovedvalg til online, træning, kæmpere og indstillinger. Styring ligger i indstillingerne. Lokal træning sættes på pause, når indstillingerne åbnes. Genererede rasterassets i `web/wwwroot/assets/arena/v2/` erstatter de proceduretegnede baggrunde, platforme, effekter og startfigurer.

14 WebP-filer fylder cirka 7,7 MB: menubillede, tre banebaggrunde, platform- og effektatlas, fem fighteratlasser og tre portrætter. Figurarkene har 80 validerede frames med fast fodplacering. Startfiguren understøtter kort hår, langt hår og kasket; profilens farver anvendes som materialefarver. PNG-kilder og præcise image_gen-prompter ligger i `assets/arena/v2/` og hentes ikke af browseren.

Fem nye menutests dækker navigation, låste figurer, login, lydvalg og manglende grafik. Alle 548 webtests består. Visuel browserkontrol af denne grafikrevision blev blokeret af computerbrugs-værktøjet; assetkontrol og direkte software-rendering bruges som supplerende QA.

## Spil

To til fire kæmpere konkurrerer om at blive på banen. Skadeprocenten øger tilbageskubbet; en tur uden for banens grænser koster et liv. Sidste overlevende vinder. Tidsgrænsen afgør ved liv/skade; en lige stilling går i sudden death. Kampen afsluttes med resultat og mulighed for revanche.

Kampmotoren omfatter acceleration, luftstyring, dobbelthop, hopbuffer, coyote time, envejsplatforme, kantgreb, normale og retningsbestemte angreb, luftangreb, opladede kraftslag, projektiler, specialangreb, opadgående redningsangreb, skjold, parering, skjoldbrud, undvigelser, greb/kast, hitstop, hitstun, påvirkning af flyveretning, genopståen og kort beskyttelse efter genopståen. Der er tre forskellige baner: Over tagene, Værkstedet og Skyøerne. CPU-spillere kan bevæge sig, angribe og forsøge at vende tilbage til banen.

Træning er tilgængelig uden login og kører samme C#-motor i Blazor WASM. Ingen separat browserfysik skal holdes synkron med serveren. Onlinekampe kræver det eksisterende Mercantec-login.

## Styring

| Handling | Tastatur | Standardcontroller |
| --- | --- | --- |
| Bevægelse/retning | A/D og W/S eller piletaster | Venstre pind |
| Hop/dobbelthop | Space | A |
| Angreb | J | X |
| Special | K; W+K redder opad | B + retning |
| Skjold | L | LT/RT |
| Undvig | Shift | LB |
| Grib/kast | U | Y |
| Oplad kraftslag | Hold I, slip I | Hold RB, slip RB |

Touchknapper inkluderer retning, hop, angreb, special, skjold, undvigelse, greb og kraftslag. Lyd kan slås til; fuld skærm åbnes fra kampbanens værktøjslinje.

## Onlineflow

1. Vælg en oplåst figur og bane. Vælg liv, minutter og eventuelt et offentligt rum.
2. Opret rummet og kopiér invitationen, eller giv vennerne rumkoden på seks tegn.
3. Vennerne logger ind på deres egne computere og åbner invitationen eller indtaster koden.
4. Alle markerer sig klar. Værten starter kampen. Værten kan tilføje CPU-spillere op til i alt fire kæmpere.
5. Efter kampen kan værten vælge revanche, som åbner lobbyen igen.

Der er automatisk genforbindelse og 30 sekunders frist til at genoptage sin plads. Værtsrollen flytter til en anden menneskelig spiller, hvis værten forlader rummet. Udtrykkelig udmeldelse sendes både på forbindelsen og gennem det godkendte HTTP-endpoint. En sen genforbindelse kan ikke genåbne et rum, som brugeren har forladt.

## Avatarer og visning

BIP-01 og Raymond bruger de eksisterende 4×2 spritesheets med otte frames pr. handling. Startfiguren tegnes med profilens hudfarve, hår, frisure og tøj. Disse kosmetiske valg valideres og videresendes i lobby og kampsnapshots, så modstanderne ser samme udseende. Ejerskab kontrolleres mod AvatarProfiles/AvatarOwnerships på serveren; browseren kan ikke åbne låste figurer. Udseendet påvirker ikke hitboxes eller kampfysik.

## Arkitektur

- `shared/Arena.cs`: input, stadier, kosmetik og snapshots.
- `api/Arena/ArenaSimulation.cs`: transportuafhængig simulation ved 60 faste trin i sekundet. Filen kompileres også direkte ind i webprojektet til lokal træning.
- `api/Arena/ArenaRoomService.cs`: midlertidige rum, forbindelser, adgangsbilletter, 60 Hz kamp og 20 Hz snapshots.
- `api/Controllers/ArenaController.cs`: eksisterende JWT-login, ejerskab og WebSocket-adgang.
- `web/Pages/Arena.razor*`: dansk UI, profilintegration og lokal træningsløkke.
- `web/wwwroot/js/arena-client.js`: tastatur/controller/touch, interpolation, forbindelser og genforbindelse.
- `web/wwwroot/js/arena-renderer.js`: Canvas 2D-baner, sprites, kamera, effekter og HUD.

Serveren modtager kun input. Positioner, skade, liv, træffere og vindere beregnes på serveren. Der er ingen klientindsendte kampresultater. Transporten bruger en kortlivet engangsbillet fra et autentificeret HTTP-kald; det egentlige login-token placeres ikke i WebSocket-URLen. Der er origin-kontrol, beskedgrænser, inputsekvenser og oprydning. Kosmetik tillader kun sekscifrede hex-farver og de kendte frisurer.

Klienten interpolerer netværkssnapshots og fremskriver lokalt den egne figur kortvarigt. Det er ikke en rollback-netcode. Høj netværksforsinkelse vil derfor kunne mærkes; ping vises i kampbanen.

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

## Verifikation den 8. november 2026

- Samlet API-regression: 473 bestået, 0 fejl, 0 sprunget over.
- Web-regression: 500 bestået, 0 fejl, 0 sprunget over.
- Heraf 32 målrettede arena-tests: determinisme, inputvalidering, dobbelthop, solide/envejsplatforme, kantgreb, angreb, skjold, redning, KO, genopståen, vinder, sudden death, bots, revanche, genforbindelse, ejerskab og kosmetik.
- Rigtig Kestrel med to uafhængige WebSocket-klienter kontrollerer kampstart, input, snapshots, profiludseende, genforbindelse og værtsskifte.
- `dotnet publish web -c Release` gennemført.
- Produktionspakken afprøvet i browseren: træning, bevægelse, hop/special, resultat, nulstilling, alle tre baner, lyd, fuld skærm og smalt mobilformat.
- Live onlinekamp på selve lup.dk er ikke verificeret, fordi ændringen ikke er deployet.
