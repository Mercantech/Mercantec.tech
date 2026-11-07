# LUP til Chrome – version 0.6

Højreklik på et billede → **Tilføj som emoji på LUP.dk**. Udvidelsen
åbner sit eget billedvindue. Vælg hele billedet eller et kvadratisk udsnit,
zoom og træk motivet, ret navn og beskrivelse, og tryk **Gem emoji på LUP**.
Emojien gemmes direkte, og billedvinduet bliver stående. Ingen LUP-fane åbnes
ved upload.

## Installer og forbind én gang

1. Byg pakken med `scripts/Package-LupExtension.ps1` fra repository-roden.
   Scriptet installerer om nødvendigt de låste billedværktøjsafhængigheder
   og pakker runtime, model, licenser og udvidelse i
   `.tools/lup-extension/LUP-0.6.0.zip`.
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
beholder sin personlige nøgle. Version 0.5 bruger også Wikimedia til GIF-filer.

## Stabil billedimport og fejlinfo

Ved højreklik forsøger udvidelsen først at hente netop det valgte billede i
den oprindelige fane og frame. Det bevarer adgang til billedets lokale
`data:`-/`blob:`-indhold og kan bruge sidens normale login og referrer.
Sidens egen referrerpolitik respekteres; cookies sendes kun ved same-origin
page-fetch. Ingen LUP-nøgle er tilgængelig for capture-funktionen.

For responsive billeder bruges også det valgte elements `currentSrc`,
`src`, `srcset` og relevante `<picture>`-kilder. Udvidelsen scanner ikke andre
billeder for at vælge et alternativ. Hvis kopien fra originalsiden ikke
kan bruges, forsøges billedadressen og de fundne alternativer direkte,
med begrænset hentetid og højst 8 MB, også ved streaming og forkerte headere.
Billedtypen genkendes på filens bytes, så en forkert MIME-header ikke alene
ødelægger importen.

Fejl viser en fast `LUP-IMP-...`-kode, en forklaring og relevante handlinger:
**Prøv igen**, domænespecifik adgang, **Brug det viste billede** eller
**Vælg en fil**. Prøv igen forsøger også originalsiden igen uden at ændre
navn, beskrivelse eller eksisterende udsnit. Kopiering af viste pixels
kræver et eksplicit klik og markeres som et PNG-stillbillede; Chrome får
fortsat lov til at blokere beskyttede/tainted billeder.

**Kopiér fejlinfo** medtager kun version, kode, billeddomæne, metode og
eventuel HTTP-status/størrelse. Billedadresser, query-parametre, sidetekst,
cookies, uploadnøgler, serverfejl og stack traces kommer ikke med.

Eksempler på fejlkoder:

| Kode | Betydning |
| --- | --- |
| `LUP-IMP-001` | Billedkladden er ugyldig eller udløbet |
| `LUP-IMP-002` / `006` | Originalside/frame er blokeret, lukket eller ændret |
| `LUP-IMP-101` / `102` | Manglende eller afvist domænetilladelse |
| `LUP-IMP-103` / `104` | Billedserveren kræver login eller afviser hentning |
| `LUP-IMP-105` / `106` | Billedet mangler, eller serveren begrænser hentninger |
| `LUP-IMP-109` / `116` | Timeout eller afbrudt overførsel |
| `LUP-IMP-110` / `114` | Filen eller pixelantallet er for stort |
| `LUP-IMP-111` / `112` | Svaret er ikke et billede, eller filen er beskadiget |
| `LUP-IMP-113` / `119` | SVG kræver PNG, eller viste pixels er beskyttede |

Capture bruger `scripting` sammen med `activeTab` efter brugerens højreklik,
kun i den relevante frame. Store billedkopier ligger som lokale Blobs i
udvidelsens IndexedDB, adskilt fra Chromes sessionkvote. Grænserne er otte
kopier, 8 MB pr. kopi og 32 MB samlet. De slettes ved vellykket gemning,
job-eviction eller oprydning af udløbne/inaktive job. Oprydning kører ved
næste brug/opstart; der oprettes ingen baggrundstimer til at holde Chrome i live.

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

## GIF’er og animerede emojis

Åbn **GIF** fra popupen eller vælg fanen **GIF’er** på søgesiden. Billeder
og GIF’er har hver sin søgning, kladde, resultater og scrollposition.
GIF-fanen søger via Openverse efter Wikimedia-GIF’er markeret CC0/Public
domain. Uden søgeord starter den med en søgning efter animerede ikoner.
Den er ikke en kopi af Discords eller Twitchs samlede GIF-katalog.

GIF-originalen hentes direkte fra en valideret Wikimedia-adresse, fordi
Openverses miniaturetjeneste kan ændre formatet eller mangle GIF-filen.
Kilde og licens vises ved resultatet og i billedværkstedet. Nøglen sendes
aldrig til søgetjenesten eller Wikimedia.

**Vælg GIF** åbner billedværkstedet. Hele billedet og beskårne udsnit gemmes
som animeret GIF med bevaret timing, loop og transparens. Beskæringsfeltet
viser ét billede fra animationen; den lille forhåndsvisning af hele GIF’en
og det gemte resultat kan afspille animationen. **Fjern baggrund · stillbillede**
vælger eksplicit PNG; **Brug original** gendanner GIF’en.

GIF’er behandles lokalt med Chromes ImageDecoder og den medfølgende gifenc.
Størrelsen tilpasses til højst 256 KB uden at fjerne frames eller ændre
animationens varighed. Der er grænser på 300 frames, 30 sekunders animation
og 30 millioner dekodede framepixels. Kan filen ikke passe, vises en fejl
med originalen bevaret; den konverteres aldrig automatisk til et stillbillede.

## Beskæring af billeder

- **Beskær til kvadrat** giver et kvadratisk udsnit. Brug zoomskyderen,
  træk/touch eller x/y-skyderne. Piletaster flytter motivet, +/− zoomer,
  og Home nulstiller. Den fulde kildeopløsning bruges til beskæringen.
- **Brug hele billedet** bevarer billedets format. GIF’er bevarer animationen
  ved tilpasning og beskæring; andre beskårne billeder bliver PNG-stillbilleder.
- **Fjern baggrund** kører lokalt med den medfølgende model. **Stop** afbryder
  beregningen, og **Brug original** vælger det oprindelige billede igen.
- Output er højst 256 KB og tilpasses emojiformatet. Beskårne billeder er
  højst 256 × 256 pixels. Kilden må højst fylde 8 MB og have 16 megapixel.
  SVG understøttes ikke. Billedet gemmes først ved tryk på gemmeknappen.

Hvis billedserveren kræver tilladelse, vises en knap til netop det domæne.
Data/blob-billeder forsøges hentet på originalsiden. En lokal fil kan stadig
være nødvendig, hvis originalsiden er lukket, eller browseren/serveren
blokerer både originalfilen og den viste billedkopi.

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
