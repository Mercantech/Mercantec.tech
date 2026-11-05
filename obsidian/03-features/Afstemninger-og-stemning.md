---
type: feature
status: implemented
tags: [feature, afstemning, stemning, blazor]
---

# Afstemninger og stemning

## Leveret 5. november 2026

Afstemninger bruger nu den fælles API og Postgres. Den tidligere lokale afstemningsvisning i DemoWorld er erstattet i Aktiviteter og hos underviseren. Almindelig Mercantec Auth bevares. Implementering og deployment er separate trin; begge tjenester skal være opdateret på den fælles installation.

## Oprettelse og design

Underviseren vælger mellem stemningstjek, fælles valg, skala 1–5 og korte svar. Oprettelsen har redigering i venstre side og en interaktiv forhåndsvisning i højre; på mobil står de under hinanden. Stemning har fem tegnede ansigter og faste, navngivne følelser. LUPs DM Sans/Space Grotesk, eksisterende temaer og statusfarver bevares.

Spørgsmål er højst 200 tegn. Valg har 2–6 forskellige svarmuligheder på højst 100 tegn. Skalaen går fra Slet ikke til Helt med. Korte svar er højst 280 tegn og vises kun hos underviseren, som kan gennemgå dem før visning på tavlen.

Gem kladde holder afstemningen skjult for eleverne. Start afstemning åbner for svar. Underviseren kan afslutte, genåbne eller starte en ny runde med samme spørgsmål. En ny runde får et nyt ID og beholder historikken fra den tidligere.

## Adgang og svar

- Afstemninger tilhører et rigtigt hold. Underviserrollen skal komme fra Mercantec Auth.
- Et holds forløbsopretter, en tilknyttet underviser og Admin kan styre afstemningerne. Andre undervisere får ikke automatisk adgang til holdets afstemninger.
- Elever kan se og besvare deres egne holds afstemninger. Lectio-elever uden Auth-koblet medlemskab får adgang, når koblingen er på plads.
- Kun holdets faktiske medlemmer kan tælles som deltagere. En undervisers forhåndsvisning afgiver ikke et svar.
- Der gemmes ét svar pr. Auth-sub pr. runde. Ændringer opdaterer samme række.
- API'et afviser nye eller ændrede svar på en lukket runde. En transaktion med rækkelås på runden koordinerer svar og lukning på tværs af servere.

## Resultater og tavle

Resultater kan frigives efter eget svar, efter afslutning eller kun hos underviseren. Begrænsningen håndhæves i API'et, så skjulte fordelinger heller ikke ligger i elevens netværkssvar. Ved valget efter eget svar bevares kravet om et afgivet svar også efter afslutning. Korte svar er altid forbeholdt underviseren i denne version.

Aktiviteter, underviseroversigt og tavle opdateres hvert 6. sekund. Ved en afbrydelse vises en fejl med genprøvning. Tavlevisningen skjuler navigation og footer og viser spørgsmålet, antal svar og fordelinger i stor skrift. Resultater viser antal og procenter med tekst; farver er et supplement. Der vises ingen elevnavne eller andre elevers Auth-ID'er i resultaterne.

Svarene er **ikke anonyme i databasen**: Auth-sub bruges til adgang og ét svar pr. person. Dette forklares i svarfladen. Egne svar, spørgsmål og tidsstempler er med i privatlivseksporten. Ved en verificeret sletteanmodning skal drift også behandle poll_responses.UserSub og ejerskab via poll_rounds.CreatedBySub; den eksisterende manuelle sletteproces er ikke automatiseret af denne ændring.

## API og drift

| Metode | Sti | Funktion |
| --- | --- | --- |
| GET | /api/polls/holds | Egne tilgængelige hold og undervisningsadgang |
| GET / POST | /api/polls | Adgangsfiltreret liste / opret kladde eller åben runde |
| GET | /api/polls/{id} | Eget svar og tilladte resultater |
| POST | /api/polls/{id}/answer | Afgiv eller ændr eget svar |
| POST | /api/polls/{id}/state | Start, afslut eller genåbn som underviser |

Migrationen SharedPolls tilføjer poll_rounds og poll_responses med holdrelation, indekser og unik nøgle på runde/bruger. Den ændrer ikke eksisterende hold, gaver eller elever. Delte kontrakter ligger i shared/. API og web har derfor build-context i repoets rod, med hver sin Dockerfile. Docker-ignore udelader lokale værktøjer og .env-filer. Dokploy skal bygge begge tjenester fra den opdaterede Compose-fil.

## Kontrol

21 relationelle API-tests dækker gaver og afstemninger, herunder holdadgang, kladder, resultatpolitik, lukning, ændrede svar, korte svar og separate runder. Tre sne-regressionstests består også. To isolerede browserkonti med en separat testdatabase har gennemført kladde → start → elevsvar → ændret svar → tavleresultat → afslutning. Ingen rigtige elever eller afstemninger blev brugt til test. Mobilbredde samt lyst, jul og hacker er visuelt kontrolleret.

API'et har fortsat de kendte, eksisterende NuGet-advarsler om System.Security.Cryptography.Pkcs 9.0.0. De er ikke introduceret af afstemningskontrakterne.

## Opfølgning

[#31](https://github.com/Mercantech/Mercantec.tech/issues/31) kan gennemgås mod ovenstående og afprøves på den fælles installation før lukning. Redigering af gemte kladder, moderation/publicering af korte svar, længere historik og SignalR kan udvides særskilt. Transporten er aktuelt polling; der er ikke indført en AhaSlides-afhængighed.

[[Hold-og-Lectio]] · [[Underviser-og-elev-views]] · [[Aktiviteter-og-status]] · [[LUP-design-og-leveranceplan]]
