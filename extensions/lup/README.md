# LUP til Chrome – version 0.4

Højreklik på et billede → **Tilføj som emoji på LUP.dk**. Udvidelsen
åbner sit eget billedvindue. Vælg hele billedet eller et kvadratisk udsnit,
zoom og træk motivet, ret navn og beskrivelse, og tryk **Gem emoji på LUP**.
Emojien gemmes direkte, og billedvinduet bliver stående. Ingen LUP-fane åbnes
ved upload.

## Installer og forbind én gang

1. Byg pakken med `scripts/Package-LupExtension.ps1` fra repository-roden.
   Scriptet installerer om nødvendigt de låste billedværktøjsafhængigheder
   og pakker runtime, model, licenser og udvidelse i
   `.tools/lup-extension/LUP-0.4.0.zip`.
2. Pak ZIP-filen ud. Åbn `chrome://extensions`, slå **Udviklertilstand** til,
   og vælg den udpakkede mappe med **Indlæs upakket**. Alternativt kan den
   byggede `extensions/lup`-mappe bruges direkte. Den skal blive liggende.
3. Åbn [Forbind LUP-udvidelsen](https://lup.dk/extension), og log ind med
   Mercantec Auth. Opret en personlig uploadnøgle og kopiér den.
4. Åbn LUP-ikonet i Chrome, indsæt nøglen, og tryk **Forbind LUP**.

Adgangen er afgrænset til Mathias og Kasper. Oprettelse kræver en godkendt
Mercantec-identitet og underviserrolle. Nøglen vises kun ved oprettelsen og
udløber efter 90 dage. Hver konto kan have fem aktive forbindelser. En nøgle
kan tilbagekaldes på samme side; det slår igennem ved næste API-kald.

**Glem forbindelse** fjerner nøglen på den aktuelle computer. Tilbagekald på
LUP.dk bruges, hvis nøglen også skal ophøre med at virke på serveren.
Efter kildeændringer genindlæses udvidelsen i `chrome://extensions`.
Udvidelsen er endnu ikke udgivet i Chrome Web Store.

Ved opdatering pakkes filerne ud oven i den eksisterende udvidelsesmappe.
Genindlæs derefter i `chrome://extensions`, så den samme Chrome-profil
beholder sin personlige nøgle. Version 0.4 tilføjer adgang til Openverse.

## Find nye billeder

Skriv et søgeord i LUP-popupen, eller åbn søgeikonet i billedværkstedet.
Søgningen henter nye billeder fra [Openverse](https://openverse.org) og
viser kun resultater markeret **CC0** eller **Public domain**. Der kræves
ingen ekstra API-nøgle. Engelske ord giver ofte flere resultater; forslagene
Katte, Hunde, Robotter, Rummet og Blomster bruger engelske søgeord.

Hvert resultat har titel, ophav, kildeside og licenslink. **Vælg billede**
åbner den eksisterende beskæring og baggrundsfjernelse. **Gem emoji på LUP**
er stadig den eneste handling, der uploader til biblioteket. Søgefane,
forespørgsel, resultater og scroll bevares, og **Tilbage til søgningen**
gør det nemt at vælge næste motiv. Oprettelsen bruger fortsat jeres
eksisterende personlige uploadnøgler.

Kildedata følger redigeringskladden og vises også efter beskæring. Vælges
en anden lokal billedfil, fjernes de tidligere kildedata. Kilde- og
licensoplysninger gemmes ikke permanent i LUPs emojidatabase.

Søgninger sendes kun ved en søgehandling, med 20 resultater ad gangen og
højst ti sider. Søgevisningen og højst 200 valgbare resultater gemmes
midlertidigt i 30 minutter. Openverse har egne hastighedsgrænser, som vises
som en fejl, hvis de nås. Søgesiden gendanner sin visning uden en ny søgning.

Søgning og billeddownload sender aldrig LUP-nøglen, cookies eller referrer
til Openverse. Thumbnail- og billedadresser konstrueres fra et kontrolleret
UUID på den faste Openverse-host. Den fulde billedversion bruges først;
en mindre forhåndsvisning prøves, hvis originalen ikke kan indlæses inden
for udvidelsens billedgrænser. Det almindelige højreklik virker fortsat
på andre hjemmesider med eventuel tilladelse til det valgte domæne.

LUP er ikke godkendt eller certificeret af Openverse. Se
[API-dokumentation](https://api.openverse.org/v1/) og
[vilkår](https://docs.openverse.org/terms_of_service).

## Beskæring og baggrund

- **Beskær til kvadrat** giver et kvadratisk udsnit. Brug zoomskyderen,
  træk/touch eller x/y-skyderne. Piletaster flytter motivet, +/− zoomer,
  og Home nulstiller. Den fulde kildeopløsning bruges til beskæringen.
- **Brug hele billedet** bevarer billedets format. Små GIF-filer kan dermed
  beholde animationen. Beskæring og baggrundsfjernelse giver et PNG-stillbillede.
- **Fjern baggrund** kører lokalt med den medfølgende model. **Stop** afbryder
  beregningen, og **Brug original** vælger det oprindelige billede igen.
- Output er højst 256 KB og tilpasses emojiformatet. Beskårne billeder er
  højst 256 × 256 pixels. Kilden må højst fylde 8 MB og have 16 megapixel.
  SVG understøttes ikke. Billedet gemmes først ved tryk på gemmeknappen.

Hvis billedserveren kræver tilladelse, vises en knap til netop det domæne.
Blob-/dataadresser, private billeder og servere, som blokerer download, kan
kræve, at billedet gemmes og vælges som lokal fil.

## Nøgler, data og rettigheder

Rigtige nøgler indgår ikke i kode, manifest, `.env`, ZIP eller Git. Nøglen
gemmes i den lokale Chrome-profils `storage.local`, uden synkronisering.
Storageadgang er begrænset til udvidelsens egne betroede sider; content scripts
og websider kan ikke hente nøglen gennem beskedprotokollen. Serveren gemmer kun
SHA256-hashen samt ejer, udløb og tilbagekaldelsesstatus.

Nøglen kan kun bruges mod `GET /api/extension/status` og
`POST /api/extension/emojis` med scope `emoji:create`. Den giver ikke adgang til
listning, omdøbning, sletning eller andre API'er og kan ikke oprette nye nøgler.
Disse nøglefunktioner kræver normal Mercantec JWT-login på LUP-siden.
Upload genbruger API'ets validering af navn, filsignatur, filstørrelse og
biblioteksgrænse. Ejerens stabile Auth-`sub` registreres som opretter.
Der er en grænse på 30 uploadforsøg pr. minut pr. nøgle.

Serverens ikke-hemmelige kontoliste konfigureres med
`ExtensionUpload__AllowedEmails` og `ExtensionUpload__AllowedSubjects`
(`EXTENSION_UPLOAD_ALLOWED_EMAILS` / `EXTENSION_UPLOAD_ALLOWED_SUBJECTS` i
Compose). Standardkonfigurationen bruger de eksisterende konti og Mathias'
stabile Auth-id, så et tilknyttet e-mailalias ikke giver adgang til en anden
bruger. En tom liste giver ingen adgang. Tilbagekaldelse og kontolisten
kontrolleres ved hvert kald.

Billeddownload og upload sender ikke cookies. Upload går kun til den faste
HTTPS-adresse på LUP.dk; nøglen sendes som Authorization-header, aldrig i URL.
Kildens adresse videresendes ikke til LUP. Beskæring og baggrundsfjernelse
kører lokalt. Gemning sender alene det valgte billede, navn og beskrivelse;
den direkte udvidelsesupload udfører ingen ekstern AI-metadataanalyse.

Kildejob er bundet til billedvinduets fane og udløber efter 30 minutter.
Fejl ved gemning bevarer kladden; vellykket gemning rydder jobbet. Der gemmes
højst otte kildejob ad gangen. Den tidligere LUP-sideimport er bevaret for
kompatibilitet med ældre installationer.

## Udvikling og kontrol

```powershell
node --test extensions/lup/test/*.test.mjs tests/LupExtension/*.test.mjs
dotnet test tests/Mercantec.Tech.Api.Tests --filter FullyQualifiedName~ExtensionUpload
dotnet test tests/Mercantec.Tech.Web.Tests
npm --prefix image-tools test
npm --prefix editor run build
```

Den nye database-migration opretter kun tabellen til hashede forbindelser.
Normal LUP-login forbliver authorization code + PKCE via Mercantec Auth.
Billedruntime/model og tilhørende licenser bygges fra `image-tools/` og pakkes
lokalt i udvidelsen; Chrome henter ikke eksekverbar kode fra en CDN.
Se [billedværktøjer](../../image-tools/README.md) for model og kilder.
