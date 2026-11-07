# Baggrundsfjernelse til LUP-emojis

Baggrunden fjernes i en separat Web Worker i brugerens browser med ONNX Runtime
Web/WASM og den lille U2netp-model. Funktionen bruger hverken en ekstern
billedtjeneste, nøgler eller en ekstra Docker-service. LUPs normale analyse og
upload af emoji-billeder er et separat eksisterende flow.

## Byg og test

Fra repository-roden med Node 22+:

```powershell
npm --prefix image-tools ci
npm --prefix image-tools run build
npm --prefix image-tools test
```

Build kopierer runtime, WASM, model, hjælpemodul og licenser til
`web/wwwroot/image-tools/`. Output og `node_modules/` er genererede filer og
skal ikke lægges i git. Docker skal bygge disse assets før `dotnet publish web`.
En komplet, allerede bygget og hash-valideret modelfil genbruges ved næste build.
Første build downloader modellen fra den officielle rembg-release.

Modellen fylder 4.574.861 bytes. Runtime-WASM fylder cirka 14,85 MB. Filerne
hentes først, når baggrundsfjernelsen bruges; modellen genbruges mellem jobs i
samme worker. Browserens almindelige HTTP-cache kan genbruge asset-downloads.

## Browser-API

```javascript
const tools = await import('/js/emoji-background.js');
const result = await tools.removeBackground(dataUrl, jobId);
// result: { base64: '...', contentType: 'image/png' }
tools.cancel(jobId);
```

Et job-id er en unik, ikke-tom streng på højst 128 tegn. Der køres kun ét
job ad gangen og gemmes højst fire aktive/ventende jobs. Et job har højst 60
sekunder inklusive ventetid, runtime-/modelindlæsning og behandling. Stop af et
aktivt job terminerer workeren, så CPU-beregningen reelt afbrydes. Et ventende
job kan stoppes uden at afbryde det aktive job. Efter stop/timeout kan næste job
oprette en ny worker. En fejl eller annullering returnerer aldrig et erstatningsbillede.

Kun base64-data-URI for PNG, JPEG, GIF og WebP accepteres. Netadresser hentes
ikke af funktionen. Kilden må højst fylde 8 MB og have 16 megapixel. Resultatet
er et stillbillede som PNG med alpha og højst 256 KB. Længste led begrænses til
256 pixels; 192 og 128 pixels forsøges, hvis PNG-filen ellers bliver for stor.
GIF og animeret WebP behandles ud fra første frame. Brugerfladen skal gøre det
muligt at bevare originalen og forklare, at resultatet er et stillbillede.

## Model og afgrænsning

U2netp er en model til salient object detection for generelle motiver, ikke
kun portrætter. Baggrundsfjernelsen er automatisk og kan beholde skygger,
små baggrundselementer eller skære fine detaljer væk. En forhåndsvisning og
muligheden for at bruge originalen er derfor en del af uploadflowet.

Input normaliseres som i rembg: RGB ved 320 × 320, division med billedets
største RGB-værdi, kanalnormalisering med mean `[.485,.456,.406]` og std
`[.229,.224,.225]`, derefter NCHW float32. Browseren udfører billedskaleringen
med canvas i høj kvalitet, så pixels kan afvige fra PILLanczos. Første ONNX-
output min/max-normaliseres til en blød alphamaske. Den multipliceres med
billedets oprindelige alpha; eksisterende gennemsigtighed bliver ikke fyldt ud.
Konstante eller ikke-endelige masker afvises og må ikke erstatte originalen.

Modellen bruger ONNX opset 11, input `input.1` med shape `[1,3,320,320]` og
første output `1959` med shape `[1,1,320,320]`. Runtime kører med én WASM-tråd,
så der ikke kræves COOP/COEP eller `crossOriginIsolated`. Selve beregningen er
stadig væk fra sidens hovedtråd.

## Kilder og licenser

- ONNX Runtime Web **1.31.1**, MIT: npm-versionen er låst i `package-lock.json`.
  [Runtime](https://github.com/microsoft/onnxruntime),
  [versioneret licens](https://raw.githubusercontent.com/microsoft/onnxruntime/v1.31.1/LICENSE),
  [versionerede tredjepartsnotitser](https://raw.githubusercontent.com/microsoft/onnxruntime/v1.31.1/ThirdPartyNotices.txt).
- U-2-Net/U2netp, Apache 2.0: [officielt projekt](https://github.com/xuebinqin/U-2-Net)
  og [licens](https://raw.githubusercontent.com/xuebinqin/U-2-Net/master/LICENSE).
- Rembg, MIT: [ONNX-model og download-checksum](https://github.com/danielgatis/rembg/blob/main/rembg/sessions/u2netp.py),
  [normalisering](https://github.com/danielgatis/rembg/blob/main/rembg/sessions/base.py),
  [licens](https://raw.githubusercontent.com/danielgatis/rembg/main/LICENSE.txt).

Build verificerer både rembg-projektets MD5 og den lokalt verificerede SHA256:

```text
https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2netp.onnx
MD5     8e83ca70e441ab06c318d82300c84806
SHA256  309c8469258dda742793dce0ebea8e6dd393174f89934733ecc8b14c76f4ddd8
```

Notitser og fulde licenser distribueres sammen med de genererede assets.
Runtime-JavaScript, hjælpe-MJS og WASM skal altid komme fra samme npm-build.
[ONNX Runtime-deployment](https://onnxruntime.ai/docs/tutorials/web/deploy.html).
