---
type: feature
status: plan
tags:
  - feature
  - aflevering
---

# Afleveringer

## Problem

Skoleopgaver skal kunne afleveres **i platformen** — ikke kun via Lectio/eksterne kanaler.

## Forslag

Aflevering knyttet til quests/opgaver med krav (fx link, video, fil). **Underviser-feedback er privat** (kun elev + underviser).

## Acceptkriterier

- [ ] Elev kan aflevere iht. quest-krav
- [ ] Underviser kan se, vurdere og give privat feedback
- [ ] Status synlig for elev (afleveret / feedback / godkendt)
- [ ] Kobling til XP ved godkendelse hvor quest kræver det

## Afhængigheder

- [[Quests-og-planner]]
- [[Notifikationer]]
- Object storage: Silo/MinIO i Compose (`pgsty/silo`, alias `mercantec-tech-minio`) + API `POST/GET /api/files`

## Risks

- Filstørrelser / hosting-omkostninger
- GDPR for uploadede elevmaterialer

## Out of scope

- Offentlig underviser-feedback på afleveringer
- Erstatning af hele Lectio som skolesystem

## Relateret

- [[Feature-oversigt]]
- [[Social-portfolio]] — adskilt fra aflevering

## Implementering 5. november 2026

En ny selvstændig side på `/afleveringer` bruger de eksisterende visuelle komponenter. Siden har forløbsmaterialer, quests, grupper, fælles kladder, afleveringsversioner og privat underviserfeedback med vedhæftninger og godkendelse. Eksisterende navigation og demosider er bevaret.

Filer gemmes gennem den eksisterende MinIO/Silo-integration. PostgreSQL gemmer filregister, tilknytninger, deltagerhistorik og vurderinger. Migreringen `LearningFilesAndSubmissions` opretter de nye tabeller. Tidligere objekter uden filregister afvises af fil-API'et, indtil de får verificeret ejerskab og tilknytning.

### Drift og visning

- Uploadgrænsen er 100 MiB pr. fil. Kladden kan indeholde op til 100 filer.
- PDF, almindelige browserbilleder, tekst/kode og browserunderstøttet lyd/video kan vises direkte.
- ZIP-filer op til 20 MiB kan få filoversigt og kodevisning af enkelte filer.
- DOC/DOCX/ODT/RTF, præsentationer og regneark får PDF-forhåndsvisning gennem LibreOffice.
- MP4/MOV/MKV/AVI/WMV kan få en H.264/AAC MP4-forhåndsvisning gennem FFmpeg.
- Compose-tjenesten `previews` flytter konverteringsjob mellem MinIO og et midlertidigt spool-område. `converter` har intet netværk og ingen database- eller lagercredentials. Originaler bevares ved konverteringsfejl.
- Forhåndsvisninger og originaler bruger samme aktuelle adgangskontrol. Korte visningsbilletter gør PDF-/videoelementer mulige uden at lægge login-token i URL'en.
- HTML samt komplette Unity/Godot-webeksporter kan forberedes fra ZIP. Udpakket størrelse er højst 512 MiB og 5.000 poster.
- Webafvikling kræver en separat HTTPS-host i `WEB_PREVIEW_ORIGIN`, appens HTTPS-origin i `WEB_PREVIEW_APP_ORIGIN` og en proxy, der kun videresender `/api/web-files/` til API'et. Proxyen skal bevare original Host og HTTPS-oplysning. Angiv proxyens IP i `REVERSE_PROXY_KNOWN_PROXIES`. Origin-værdien er tom som standard.
- Den isolerede spilhost bør have egne host-only cookies og må ikke dele appens autentifikation. Single-thread webbuilds er første mål. Threaded builds kræver yderligere afprøvning af cross-origin isolation.
- Native Unreal-builds og øvrige programmer kan afleveres i ZIP med supplerende video. De afvikles ikke af filsystemet.

### Fortsat arbejde

Genoptagelig upload, højere konfigurerbare kvoter, virusscanning, automatisk oprydning af ubrugte uploads og backup/restore-procedure skal færdiggøres før bred drift med elevdata. Mislykkede konverteringer kræver aktuelt en ny upload. XP-integrationen afventer en vedvarende XP-model. Videofremdrift og en Notion-lignende sideeditor er særskilte efterfølgende funktioner.

Migrering og tjenester skal først afprøves på staging med skolens egne dokumenter og repræsentative webbuilds. Produktionsdeploy udføres særskilt.

### Verifikation efter samlet oprydning og test

Den samlede .NET-kørsel består med 73 API-tests og 66 webtests. Docker Compose validerer, migreringen er afprøvet på PostgreSQL 16, og EF melder ingen udestående modelændringer. Både runtime- og konverteringscontaineren kan bygges.

Et midlertidigt testmiljø med egne PostgreSQL- og MinIO-instanser gennemførte 28 HTTP-kontroller af upload, byte-identiske originaler, privat filadgang, kursusmaterialers udgivelse, delt gruppekladde, indsendelse, feedback og godkendelse.

Chromium gennemførte underviser/elev/gruppedeltager-flowet mod de rigtige API-controllere. Native PDF-visning, billedvisning og video med afspilning og spoling blev verificeret. Et arkiv over 20 MiB beholder download og webafvikling, selv om den begrænsede arkivoversigt afviser størrelsen.

DOCX, PPTX og XLSX blev konverteret til gyldige PDF-filer. En MOV med video og lyd blev konverteret til H.264/AAC MP4. Private visningsbilletter samt otte byte-range-kontroller bestod. Konverteringen blev udført uden netværksadgang.

HTML-spil og enkeltstående HTML blev afprøvet i browseren på en isoleret HTTPS-origin. Strukturelle Unity- og Godot-webfixtures hentede relative JavaScript- og datafiler og kompilerede gyldig WebAssembly. Repræsentative fulde engine-eksporter fra undervisningen bør fortsat afprøves før produktionsbrug.

Testene fandt og rettede intern HTTP-signering i S3-klienten, fallback for store ZIP-filer, behandling af ikke-seekbare HTML-streams og sikker oprydning af gamle spoolmapper. Eksisterende sider, navigation og CSS blev ikke ændret under denne testopgave.

Der er eksisterende NuGet-sikkerhedsadvarsler om den transitive pakke `System.Security.Cryptography.Pkcs` 9.0.0. De kræver en særskilt opdatering af afhængighederne.

### Afsluttet lokal oprydning

De tre midlertidige QA-containere, deres anonyme testvolumener, to QA-images og alle egne midlertidige testmapper er fjernet. Docker-byggecache er tømt fra cirka 10 GB til 0 byte, og genererede .NET-byggefiler er ryddet.

Efter TRIM blev Dockers afmonterede virtuelle disk komprimeret fra 13.380.878.336 til 4.625.268.736 byte. C-drevet har ved slutkontrollen cirka 13,6 GB ledig plads mod cirka 9,2 GB ved opgavens start. Docker er startet igen. De oprindelige web-, API- og databasecontainere kører, og begge permanente projektvolumener er bevaret.
