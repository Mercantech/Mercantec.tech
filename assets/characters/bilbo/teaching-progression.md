# Bilbo Backends marmelade-API

Forløbet bruger ASP.NET Core 10 Minimal APIs, EF Core og PostgreSQL. Bilbo tager imod en bestilling på 1 til 12 glas af én varetype. Kundens identitet, lagertrækket og ordrenummeret fastsættes på serveren.

| Trin | Kort | Det, eleven bygger | Det, eleven kontrollerer |
| --- | --- | --- | --- |
| 1. HTTP og kontrakt | 01–05 | Ordregruppe, DTO, validering, scoped DbContext og asynkront opslag. | Metode, sti, status, Location, kundeafgrænsning og afbrydelse. |
| 2. Model og læsning | 06–09 | Jam og Order, migration, næste side og en beskyttet navneændring i kataloget. | Fremmednøgle, sortering, næste cursor og katalogets sletteregel. |
| 3. Konsistente ordrer | 10–12 | Ét atomisk gemmekald, versionstjek og en unik bestillingsnøgle pr. kunde. | Ét lagertræk ved gentagelse og én vinder ved kapløb om sidste glas. |
| 4. Adgang, drift og test | 13–16 | Tokenvalidering, kundepolitik, struktureret log og integrationstest. | Ejerskab, relevante fejlstatusser, log og den gemte tilstand. |

## Kontrakten i øvecasen

- `POST /orders` modtager `jamId`, `quantity` og en klientvalgt `requestId`. Ny ordre og gentagelse med samme data får `201` og samme `Location`. Genbrug af nøglen til andre data giver `409`.
- `GET /orders/{id}` og `GET /orders?afterId=…&limit=…` filtrerer på den indloggede kundes `sub`. `PUT /jams/{id}` ændrer navnet på en eksisterende vare og kræver katalogtilladelsen `JamAdmin`.
- `Jam.Version` skiftes ved opdateringer. Det unikke PostgreSQL-indeks `UX_Orders_Customer_Request` beskytter `(CustomerId, RequestId)`. Lagertræk og ordre gemmes i én transaktion via samme kontekst.
- Katalogets `POST`, `GET` og `DELETE` er selvstændige udvidelsesopgaver på kort 09.

## Sådan samles kodeuddragene

Opret et ASP.NET Core-projekt til `net10.0` og tilføj matchende versioner af Npgsqls EF Core-udbyder, `Microsoft.EntityFrameworkCore.Design` og `Microsoft.AspNetCore.Authentication.JwtBearer`. Installer `dotnet-ef` i en version, der matcher EF Core. Brug `System.Security.Claims`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.Authentication.JwtBearer` og `Npgsql` i `Program.cs`. Sæt forbindelsen og tokenudstederen i lokalt miljø eller konfiguration.

- **Registrering og opsætning**. Kort 04 og 13 konfigurerer services før `builder.Build()`. Kort 14 tilføjer Problem Details og fælles fejlhåndtering. Brug derefter `app.UseAuthentication()` og `app.UseAuthorization()` før ruterne fra 02 og 09 og afslut med `app.Run()`.
- **Typer og handlers**. Kort 03 og 06 deklarerer typerne. Kort 05, 08, 09, 10 og 12 leverer lokale metoder til de tilhørende ruter. I en fil med top-level statements skal alle klasse- og recorddeklarationer stå efter registrering og lokale funktioner. Brug eventuelt separate typefiler.
- **Migration og log**. Kort 07 viser shellkommandoer efter at modellen er klar. Kør dem mod en lokal øvedatabase. Kort 14 viser et loguddrag til den handler, der gemmer ordren. Tilføj `HttpContext httpContext` og `ILogger<Program> logger` til handlerens parametre.
- **Testuddrag**. Kort 15 hører hjemme i et testprojekt med xUnit og `Microsoft.AspNetCore.Mvc.Testing`. Tilføj `public partial class Program { }` til appen, og konfigurer `WebApplicationFactory<Program>` med en isoleret PostgreSQL-database og testidentitet. Migrér databasen og klargør én marmeladevare med id 1 og lager 5, før testen kører. Testen forudsætter en dedikeret tom ordretabel.

Kortene bruger udsnit af én samlet løsning. Migration og databaseadfærd afprøves med egen PostgreSQL. En klient, der mister forbindelsen efter commit, bruger samme nøgle til det næste forsøg.
