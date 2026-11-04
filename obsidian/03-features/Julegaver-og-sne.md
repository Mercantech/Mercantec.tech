---
type: feature
status: implemented
tags: [feature, christmas, gifts, blazor]
---

# Julegaver og sne

Implementeret 4. november 2026 efter Kaspers ønske om rigtig levering mellem brugere. Gaver gemmes af API'et i Postgres og kan åbnes fra en anden computer. Status her beskriver koden; installationens deployment skal kontrolleres særskilt.

## Gaveforløb

Gaveknappen i juletemaet åbner Modtaget, Send en gave og Sendt. Afsender vælger en modtager, en julehilsen, indpakning og højst 240 tegn. Pakken kan ses før afsendelse. Modtageren får en tæller, klikker på pakken og ser låget åbne og hilsenen komme frem. Afsender kan se leveret/åbnet. Indbakken opdateres hvert 20. sekund samt ved åbning og handlinger.

Uåbnede gaver gør knappen synlig også uden for juletemaet. Korte afbrydelser giver en genprøv-knap. Gensendelse af samme forsøg bruger samme ID og opretter ikke en ekstra gave. Åbning gemmes kun første gang. Skift af konto rydder indbakken, og navigation lukker gavevinduet.

Det er gratis hilsner: En pose juleglæde, Tak for hjælpen og Lidt ekstra varme. Der overføres ikke guld, XP, møbler eller avatartøj. Butik og serverøkonomi er fortsat separate leverancer. Visning uden login kan afprøve en pakke, men sender ingen gave.

## Modtagere og adgang

- Begge personer skal have åbnet den opdaterede LUP med deres rigtige Mercantec Auth-konto. Der registreres en gavekontakt med Auth-sub, visningsnavn og underviserflag.
- Elever kan vælge registrerede personer fra egne faktiske hold og holdets forløbsopretter. Undervisere/admin kan vælge andre registrerede LUP-kontakter.
- E-mailadresser vises ikke i modtagervælgeren. Admin-kataloget i Auth udleveres ikke til elever.
- API'et validerer modtageradgang igen ved afsendelse. Identitet og afsendernavn kommer fra serveren. Kun afsender og modtager kan læse en gave, og kun modtageren kan åbne den.

Begge klienter skal bruge **samme API og database**. To lokale installationer har hver sin indbakke. Brug den fælles installation på mercantec.tech, når både API og web er deployet, eller en aftalt fælles udviklingsinstallation. Et lokalt besøg hos Kasper sender ikke automatisk til Mathias' separate lokale database.

## API og database

| Metode | Route | Funktion |
| --- | --- | --- |
| POST | /api/gifts/profile | Registrér/opdatér egen gavekontakt fra verificerede claims |
| GET | /api/gifts/recipients | Adgangsfiltrerede modtagere |
| GET | /api/gifts | Seneste 50 modtagne og 50 sendte; samlet antal uåbnede |
| POST | /api/gifts | Send med ID, recipientSub, kind, wrapping og message |
| POST | /api/gifts/{id}/open | Åbn som modtager; idempotent tidspunkt |

Alle endpoints kræver almindelig Mercantec Auth JWT. Der er ikke tilføjet en ny loginløsning. Migrationen ChristmasGifts opretter christmas_gifts og gift_contacts med indekser og forbud mod at sende til sig selv. Migrering køres automatisk ved API-start.

Privatlivseksporten indeholder egen gavekontakt samt sendte/modtagne gaver. Den eksisterende sletteanmodning udføres stadig manuelt af drift: medtag gift_contacts.UserSub og christmas_gifts.SenderSub/RecipientSub i den verificerede sletning. API'et logger ikke gavehilsner.

## Sne

Sneindstillinger har tre tilstande: Følg systemet, Bevægelig sne og Stille sne. I systemtilstand respekteres prefers-reduced-motion. Start sne vælger aktivt bevægelig sne, også hvis computeren foretrækker reduceret bevægelse. Dette tilsidesætter kun sneen; øvrige animationer følger stadig systemets præference.

Valget gemmes lokalt som lup-snow-mode (auto/on/off). Et tidligere lup-snow-paused-valg respekteres, indtil brugeren vælger på ny. Kontrollen forklarer den aktuelle tilstand. Sne stopper ved skjult fane og uden for juletemaet; ét canvas, højst 90 fnug og begrænset pixelratio bevares. Mus/finger påvirker vinden.

## Lokal start og kontrol

Start API og Postgres:

~~~bash
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --build api db
dotnet run --project web --launch-profile http
~~~

Åbn http://localhost:3000. Denne port er registreret til Auth-callback. Port 5253 giver invalid_redirect_uri i den nuværende Auth-konfiguration. HTTP-startprofilen sætter Development, så web bruger http://localhost:8080/api/; en kørsel uden startprofil skal sætte miljøet særskilt. Docker-web på port 3000 bruger i stedet nginx og samme-origin /api/.

Kontrolleret: Blazor-build, tre sne-regressionstests (bevægelse under reduceret bevægelse, genindlæsning og skjult fane), otte relationelle API-tests (adgang, modtagere, validering, gensendelse, indbakke, åbning og kontaktprofil), lokal Postgres-migrering og API-health. Gavevisning, åbning, mobilbredde og sneens eksplicitte start under reduceret bevægelse er afprøvet i browseren. Ingen gaver er sendt til rigtige personer som led i testen. API-build har fortsat de eksisterende NuGet-advarsler om System.Security.Cryptography.Pkcs 9.0.0; de kommer ikke fra gavefunktionens afhængigheder.

Produktionsdeployment følger [[Dokploy-drift]]. Begge tjenester skal opdateres; et web-deployment alene opretter ikke tabeller eller endpoints.
