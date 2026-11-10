# Spilupload

`/games/mine` (og alias `/games/upload`) lader en logget ind bruger gemme sit eget browser-spil. Brugeren angiver navn og en HTML-/HTM-fil eller ZIP med en HTML-startside. Filgrænsen er 100 MiB. Dette er private uploads og kræver hverken et hold eller en offentlig udgivelse. Elevvisning kan ikke bruges til at uploade på en andens konto.

- `GET /api/me/games` returnerer op til 50 af kontoens egne uploads.
- `POST /api/me/games` modtager multipartfelterne `file`, `title` og `requestId`.
- `X-LUP-Game-Subject` binder klienthandlingen til den konto, den begyndte på. Et skift af konto giver 409 før lagring.

Ejer kommer udelukkende fra det autentificerede subject. Serveren begrænser både den angivne og faktisk læste størrelse, kræver samme længde og beregner SHA-256. `WebBuildService` validerer ZIP uden stiudbrud, symbolske links, dubletter, ufuldstændige filer, mere end 5.000 entries eller mere end 512 MiB udpakket indhold. En HTML-startside er påkrævet. En HTML-fil behandles som et enkelt web-build; systemet kan ikke automatisk afgøre, om indholdet er et færdigt spil.

Først efter original og alle validerede webfiler er gemt, oprettes `FileAsset` og `UploadedGame` atomisk i databasen. Der findes ingen pending-/draft-rækker, som kan give oplåsningsfremskridt. Fejl rydder original, derivater og manifest fra forsøget. Rydningsfejl giver ingen adgang til objekter uden registreret fil. Migrationen `20261110085326_UploadedGamesForAvatarRequirements` tilføjer kun den nye tabel og indekser; den gætter ikke på tidligere bilag.

Unik `(OwnerSub, RequestId)` gør genforsøg idempotente. Samme id skal have samme navn, filnavn og indholdshash; ændret indhold giver 409. To konti kan bruge samme request-id uden at dele data. `FileAssetId` er unik og har cascade delete, så slettede kildefiler ikke fortsat giver udokumenteret fremskridt. En allerede optjent avatar bevares. Privatlivseksporten indeholder egne uploadoplysninger uden objektnøgler eller filindhold.

Et vellykket upload genhenter avatarprofilen og opfylder LUPPs krav. Samlingen viser `1 / 1 uploadet spil` og `Lås op gratis`. Browserdata og klientindsendte fremskridt giver ingen adgang; serveren beregner kravet igen ved oplåsning.

## Afvikling og drift

Upload og LUPP-oplåsning kræver database og konfigureret object storage. De fungerer uden et eksekverbart preview-domæne. Mine spil linker til den eksisterende private filvisning, hvor originalen fortsat kan hentes.

Afvikling bruger den eksisterende `WEB_PREVIEW_ORIGIN`, et særskilt HTTPS-domæne med autoriseret filbillet. `WEB_PREVIEW_APP_ORIGIN` angiver appens origin. Domænet skal rutes **direkte til API'et** med bevaret HTTPS og Host fra en IP i `REVERSE_PROXY_KNOWN_PROXIES`. Det må ikke gå gennem webcontainerens HTTP-nginx på 3000, som ellers omskriver HTTPS til http. Et tomt preview-domæne afviser afvikling med en forklaring i filvisningen; upload og optjent fremskridt bevares.

Eksekverbare filer serveres kun på det særskilte domæne med eksisterende CSP-sandbox, filbillet, ejerskabskontrol, `no-store`, `nosniff` og afviste eksterne script-/netværkskilder. Der accepteres ingen eksterne URL'er som spilupload. Uploads ændrer ikke denne preview-politik.

## Kontrol

`UploadedGameTests` tester ægte SQLite-lagring, rollback ved lager-/databasefejl, private/andre ejere, ugyldige arkiver, idempotens, sene kontoskift, privacy-eksport og PostgreSQL-migreringsmodellen. `ArenaAbilityNetworkTests` følger en rigtig HTTP-multipart-upload gennem gratis oplåsning og to uafhængige HTTP/WebSocket-klienter til LUPPs faktiske projektil. Web-tests og browserkontrollen tester filvalg, genforsøg, live kravopdatering og gratis oplåsning uden produktionsbrugere.
