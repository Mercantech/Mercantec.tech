# LUP til Chrome

Første funktion: Højreklik på et billede og vælg **Tilføj som emoji på LUP.dk**.
Udvidelsen viser billedet og et kort navn. **Fortsæt på LUP.dk** åbner en ny
fane med LUPs normale emoji-formular. Her gennemgår du beskrivelse og øvrige
felter og trykker **Upload**. Tilføjelse kræver underviseradgang i LUP.

## Installer lokalt

1. Åbn `chrome://extensions` i Chrome.
2. Slå **Udviklertilstand** til øverst til højre.
3. Vælg **Indlæs upakket**, og vælg mappen `extensions/lup` fra dette repo.
4. Fastgør eventuelt LUP i værktøjslinjen. Højreklik på et billede på en normal
   hjemmeside for at begynde.

Udvidelsen er ikke udgivet i Chrome Web Store. Den lokale mappe skal blive
liggende. Efter kildeændringer trykkes **Genindlæs** i `chrome://extensions`;
genindlæs også den åbne LUP-fane. Udvidelsen kræver den tilsvarende
emoji-importfunktion på LUP.dk. En ældre LUP-version kan ikke modtage kladden.

En ZIP-pakke kan dannes med `scripts/Package-LupExtension.ps1`; den lægges i
`.tools/lup-extension/LUP-0.1.0.zip`. Pak ZIP-filen ud, og vælg den udpakkede
mappe med **Indlæs upakket**. Kildekode, tests og dokumentation ligger fortsat i
repositoryet; pakken indeholder kun udvidelsens nødvendige runtime-filer.

## Billeder og adgang

- PNG, JPEG, WebP og AVIF kan klargøres til et emoji-billede på højst 256 KB.
  Store stillbilleder tilpasses til højst 256 pixels på den længste led og
  gemmes som PNG. Om nødvendigt prøves 192 og 128 pixels.
- GIF-filer på højst 256 KB bevares med animation. Større GIF-filer konverteres
  til et stillbillede; dette vises under forhåndsvisningen.
- Kilden må højst fylde 8 MB og have 16 megapixel. SVG understøttes ikke.
- Hentning bruger billedets HTTP(S)-adresse. Chrome kan kræve adgang til netop
  billedets hjemmeside; tilladelsen gives på en særskilt knap. Tilladelsen kan
  senere fjernes i Chromes indstillinger for udvidelsen.
- Adresser til lokale filer, data- og blob-billeder samt billeder, der kræver
  cookies/login eller blokerer download, kan kræve at du gemmer billedet og
  vælger filen manuelt. Chrome Web Store og interne Chrome-sider har deres
  egne begrænsninger.

## Login og kladder

Udvidelsen opbevarer ikke adgangstokens og kalder ikke LUPs API. LUPs normale
Mercantec Auth-login og rettigheder bruges i destinationsfanen. En ny fane kan
kræve login, selv om en anden LUP-fane er åben. Importen skal fortsættes i den
fane, udvidelsen åbnede.

Kildejob og billedkladder udløber efter 30 minutter. De gemmes kun i Chromes
`storage.session`, bliver utilgængelige ved udløb og fjernes ved næste
oprydning eller når browserens session slutter. Der opbevares højst otte
kildejob og otte billedkladder ad gangen; de ældste ryddes først. Kladden
bevares under login og genindlæsning og slettes efter en vellykket upload.
En udløbet kladde startes igen ved at højreklikke på billedet.

## Data og tilladelser

- `contextMenus` til menupunktet på billeder.
- `activeTab` til midlertidig adgang efter dit højreklik.
- `storage` til tidsbegrænsede kildeadresser og billedkladder i hukommelsen.
- Content script kun på **https://lup.dk/** til aflevering af kladden til
  emoji-formularen.
- Valgfri HTTP(S)-værtsadgang bliver kun anmodet for det valgte billedes
  hjemmeside. Der gives ingen obligatorisk adgang til alle hjemmesider.

Billeddownload sender ikke cookies eller en henvisende sideadresse. Når du
fortsætter, afleveres kun selve billedet, filtypen og det valgte navn til den
bestemte LUP-fane. Kildens URL afleveres ikke til LUP. LUPs sædvanlige
billedanalyse og upload gælder derefter; analysen kan bruge den AI-tjeneste,
LUP er konfigureret med. Der er ingen analyse-, login- eller uploadtjeneste
indbygget i udvidelsen.

Import-id er tilfældigt og bundet til destinationsfanens id. Modtagelse af
billeddata kræver præcis HTTPS-origin `https://lup.dk`, stien `/emojis` og den
matchende importparameter. En uploadkvittering accepteres også efter, at LUP
har fjernet importparameteren, men kræver stadig samme fane, origin, sti og
gyldige kladde. Andre faner og andre origins får ingen billeddata.

Udvidelsen bruger Chromes standardikon og tekstnavnet LUP. Der er endnu ikke
tilføjet et særskilt LUP-logo.

## Kontrol

Fra repository-roden med Node 20 eller nyere:

```powershell
node --test extensions/lup/test/*.test.mjs
```

Testene kontrollerer blandt andet størrelse, udløb, base64, origin, tabbinding,
genlevering og uploadkvittering. Browserkontrol skal desuden gennemgå det
virkelige højreklik, billedkonvertering, tilladelsesdialog, login, reload og
upload i LUP. Udvidelsen er en klient og kræver ingen Docker-service; den
eksisterende Docker-hostede LUP håndterer login, analyse og lagring.

Chrome-dokumentation:
[contextMenus](https://developer.chrome.com/docs/extensions/reference/api/contextMenus),
[valgfri tilladelser](https://developer.chrome.com/docs/extensions/reference/api/permissions),
[netværksadgang](https://developer.chrome.com/docs/extensions/develop/concepts/network-requests).
