namespace Mercantec.Tech.Web.Characters;

internal static class BilboTeachingMaterials
{
    private const string ImageFolder = "/images/characters/bilbo/infographics/";
    private const string Contract = "1. HTTP og kontrakt";
    private const string Model = "2. Model og læsning";
    private const string Orders = "3. Konsistente ordrer";
    private const string Operations = "4. Adgang, drift og test";

    private static readonly CharacterLink Http = Source("IETF · HTTP-semantik",
        "https://www.rfc-editor.org/rfc/rfc9110.html");
    private static readonly CharacterLink Minimal = Source("Microsoft Learn · Minimal APIs",
        "https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0");
    private static readonly CharacterLink Handlers = Source("Microsoft Learn · Ruter og handlers",
        "https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0");
    private static readonly CharacterLink Responses = Source("Microsoft Learn · Svar fra Minimal APIs",
        "https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0");
    private static readonly CharacterLink Binding = Source("Microsoft Learn · Parameterbinding",
        "https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0");
    private static readonly CharacterLink Context = Source("Microsoft Learn · DbContext og levetid",
        "https://learn.microsoft.com/ef/core/dbcontext-configuration/");
    private static readonly CharacterLink Async = Source("Microsoft Learn · Asynkrone databasekald",
        "https://learn.microsoft.com/ef/core/miscellaneous/async");
    private static readonly CharacterLink Relations = Source("Microsoft Learn · Én-til-mange-relationer",
        "https://learn.microsoft.com/ef/core/modeling/relationships/one-to-many");
    private static readonly CharacterLink Migrations = Source("Microsoft Learn · Udrulning af migrationer",
        "https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying");
    private static readonly CharacterLink Paging = Source("Microsoft Learn · Paginering",
        "https://learn.microsoft.com/ef/core/querying/pagination");
    private static readonly CharacterLink Tracking = Source("Microsoft Learn · Tracking og no-tracking",
        "https://learn.microsoft.com/ef/core/querying/tracking");
    private static readonly CharacterLink Crud = Source("Microsoft Learn · Gem tilføjelser, ændringer og sletninger",
        "https://learn.microsoft.com/ef/core/saving/basic");
    private static readonly CharacterLink Transactions = Source("Microsoft Learn · Transaktioner i EF Core",
        "https://learn.microsoft.com/ef/core/saving/transactions");
    private static readonly CharacterLink Concurrency = Source("Microsoft Learn · Optimistisk samtidighed",
        "https://learn.microsoft.com/ef/core/saving/concurrency");
    private static readonly CharacterLink Indexes = Source("Microsoft Learn · Unikke indekser",
        "https://learn.microsoft.com/ef/core/modeling/indexes");
    private static readonly CharacterLink Pg = Source("Npgsql · PostgreSQL-udbyder til EF Core",
        "https://www.npgsql.org/efcore/");
    private static readonly CharacterLink PgErrors = Source("Npgsql · PostgreSQL-fejl",
        "https://www.npgsql.org/doc/diagnostics/exceptions_notices.html");
    private static readonly CharacterLink Authn = Source("Microsoft Learn · Validering af bearer-tokens",
        "https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0");
    private static readonly CharacterLink Authz = Source("Microsoft Learn · Ressourcebaseret adgang",
        "https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased?view=aspnetcore-10.0");
    private static readonly CharacterLink Logging = Source("Microsoft Learn · Struktureret logging",
        "https://learn.microsoft.com/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0");
    private static readonly CharacterLink Errors = Source("Microsoft Learn · Fejlhåndtering og Problem Details",
        "https://learn.microsoft.com/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0");
    private static readonly CharacterLink Tests = Source("Microsoft Learn · Integrationstest i ASP.NET Core",
        "https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0");
    private static readonly CharacterLink Strategy = Source("Microsoft Learn · Teststrategi for EF Core",
        "https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy");

    public static IReadOnlyList<CharacterIllustration> Illustrations { get; } =
    [
        Card("01-http-og-status", "HTTP fra ordre til svar",
            "Bilbo følger en bestilling fra lugen til en kvittering, et afvist input eller en tom hylde.",
            Contract,
            "Læs en HTTP-anmodning og vælg et svar, der fortæller klienten, hvad der skete.",
            [
                "Klienten sender en metode, en sti og eventuelt headers og data. GET /orders/42 læser en ordre. POST /orders beder om at oprette en ordre. Svaret har en statuskode, headers og eventuelt en krop.",
                "I casen svarer en ny ordre med 201 Created og en Location til ordren. En læsning giver 200 OK. 400 bruges for ugyldigt input, 404 når ordren ikke kan findes for kunden, og 409 når den ønskede ændring konflikter med den aktuelle tilstand, fx ved tomt lager.",
                "Bordøvelse. Skriv metode, sti, forventet status og relevant header for oprettelse, efterfølgende læsning, antal nul og udsolgt lager. Hvilken sti skal klienten følge efter 201?"
            ],
            null, Http, Responses),
        Card("02-ruter-og-endpoints", "Find den rette rute",
            "Bilbo sender to bestillingssedler til hver sin handler ud fra metode og rute.",
            Contract,
            "Kobl en HTTP-metode og en sti til den handler, der skal udføre handlingen.",
            [
                "I en Minimal API vælger routing ud fra metode og ruteskabelon. GET /orders/42 og POST /orders har to forskellige handlers. {id:long} matcher et heltal, der kan bindes til handlerens long id.",
                "Gruppér ordreadgang under /orders og sæt kundepolitikken på gruppen. GetOrder, ListOrders og CreateOrder er handlers fra de senere kort. Ruten GET /orders/{id} er den sti, Location peger på efter oprettelse.",
                "Kodeøvelse. Tilføj grupperingen i Program.cs efter at app er bygget. Afprøv stierne med en klient, der har et labtoken. Prøv også GET /orders/banan, og undersøg hvilken handler der bliver valgt."
            ],
            """
            var orders = app.MapGroup("/orders")
                .RequireAuthorization("JamCustomer");
            orders.MapGet("", ListOrders);
            orders.MapGet("/{id:long}", GetOrder);
            orders.MapPost("", CreateOrder);
            """, Minimal, Handlers, Binding),
        Card("03-dto-og-validering", "Tjek ordren ved døren",
            "Bilbo kontrollerer en bestillingsseddel og sender en mangelfuld seddel tilbage.",
            Contract,
            "Lad klienten sende de få felter, der hører til en bestilling, og giv præcise valideringsfejl.",
            [
                "En DTO er API'ets datakontrakt. Ordren modtager varenummer, antal og en ny RequestId for hvert nyt købsforsøg. Samme nøgle genbruges ved transportfejl. Kunden findes i adgangstokenet. Lager og ordrenummer fastsættes på serveren.",
                "Tjek format og grænser før opslag. Brug Results.ValidationProblem(errors), når ordbogen har fejl. Varen findes først ved opslag i databasen. Tilstrækkeligt lager afgøres ud fra den aktuelle lagertilstand.",
                "Kodeøvelse. Test et gyldigt antal på 12, grænserne 0 og 13, samt manglende nøgle. Kontrollér, at en ugyldig bestilling giver 400 med feltnavn, og at lageret er uændret. Deklarér typerne som records sidst i filen eller i en separat fil."
            ],
            """
            static Dictionary<string, string[]> Validate(CreateOrderRequest input)
            {
                var errors = new Dictionary<string, string[]>();
                if (input.JamId <= 0)
                    errors[nameof(input.JamId)] = ["Vælg en marmelade."];
                if (input.Quantity is < 1 or > 12)
                    errors[nameof(input.Quantity)] = ["Vælg fra 1 til 12 glas."];
                if (input.RequestId == Guid.Empty)
                    errors[nameof(input.RequestId)] = ["Angiv en bestillingsnøgle."];
                return errors;
            }

            public record CreateOrderRequest(long JamId, int Quantity, Guid RequestId);
            public record OrderDto(long Id, long JamId, int Quantity);
            """, Binding, Responses),
        Card("04-di-og-dbcontext", "Giv hver ordre sin egen kontekst",
            "Bilbo holder to bestillinger i hver sin arbejdsbakke, mens begge læser i det samme arkiv.",
            Contract,
            "Lad hver HTTP-anmodning få sit eget arbejdsområde til databaseændringer.",
            [
                "AddDbContext registrerer konteksten som scoped som standard. ASP.NET Core opretter en scope pr. HTTP-anmodning. Konteksten sporer ændringer for denne arbejdsopgave og bliver ryddet op, når scopet afsluttes.",
                "Registrér JamDbContext i Program.cs før builder.Build(). Installér Npgsql.EntityFrameworkCore.PostgreSQL, og tilføj using Microsoft.EntityFrameworkCore for UseNpgsql. Sæt ConnectionStrings__JamDb via lokal konfiguration eller miljø. Handlers modtager konteksten som parameter.",
                "Kodeøvelse. Log kontekstens ContextId kortvarigt i dit eget lab, og send to separate anmodninger. Sammenlign ID'erne. Behold konteksten inden for sin scope, og afvent hvert kald før næste operation på samme kontekst."
            ],
            """
            var connectionString = builder.Configuration.GetConnectionString("JamDb")
                ?? throw new InvalidOperationException("JamDb mangler i konfigurationen.");

            builder.Services.AddDbContext<JamDbContext>(
                options => options.UseNpgsql(connectionString));
            """, Context, Pg),
        Card("05-async-og-cancellation", "Vent uden at blokere",
            "Bilbo kan tage imod en ny seddel, mens et opslag venter, og viser en separat sti til afbrydelse.",
            Contract,
            "Afvent databasearbejdet asynkront og send requestens afbrydelsessignal videre.",
            [
                "await lader serveren bruge sin tråd til andet, mens I/O venter. Flere EF-operationer på samme DbContext afventes i rækkefølge. En CancellationToken i en Minimal API-handler bindes til HttpContext.RequestAborted.",
                "Send tokenet til EF Core og lad normal afbrydelse nå frameworket. En databaseudbyder kan honorere tokenet under I/O. En klient, der mister forbindelsen omkring commit, kan have en gemt ordre, selv om den aldrig modtog svaret.",
                "Kodeøvelse. Indsæt GetOrder fra uddraget som handler. Test eget og fremmed ordrenummer med hvert sit labtoken. Afbryd derefter en langsom labanmodning, og undersøg hvilke observationer klienten og serveren kan gøre hver for sig."
            ],
            """
            static async Task<IResult> GetOrder(
                long id, ClaimsPrincipal user, JamDbContext db, CancellationToken ct)
            {
                var customerId = user.FindFirst("sub")!.Value;
                var order = await db.Orders.AsNoTracking()
                    .Where(x => x.Id == id && x.CustomerId == customerId)
                    .Select(x => new OrderDto(x.Id, x.JamId, x.Quantity))
                    .SingleOrDefaultAsync(ct);

                return order is null ? Results.NotFound() : Results.Ok(order);
            }
            """, Async, Binding, Authz),
        Card("06-ef-model-og-relationer", "Bind ordre og marmelade sammen",
            "Bilbo tegner forbindelsen mellem én marmelade i varekataloget og flere bestillinger.",
            Model,
            "Byg en EF Core-model med én marmeladevare og mange ordrer, der kan referere til den.",
            [
                "Jam.Id er varens primærnøgle. Order.JamId er en påkrævet fremmednøgle, og Order.Jam er navigationen til varen. Den samme marmelade kan knyttes til mange ordrer. Denne øvecase bruger én varetype pr. ordre.",
                "Stock er heltal for antallet af glas. Version bruges til at opdage samtidige ændringer, og kombinationen af CustomerId og RequestId bruges til gentagelser. DeleteBehavior.Restrict beskytter eksisterende ordrer ved sletning af en vare.",
                "Kodeøvelse. Tilføj typerne til projektet. Tegn kardinalitet, nøgler og sletteregel. Generér migrationen på næste kort, og tjek at den indeholder fremmednøglen og det navngivne unikke indeks."
            ],
            """
            public sealed class Jam
            {
                public long Id { get; set; }
                public string Name { get; set; } = "";
                public int Stock { get; set; }
                public Guid Version { get; set; } = Guid.NewGuid();
            }

            public sealed class Order
            {
                public long Id { get; set; }
                public long JamId { get; set; }
                public Jam Jam { get; set; } = null!;
                public int Quantity { get; set; }
                public string CustomerId { get; set; } = "";
                public Guid RequestId { get; set; }
            }

            public sealed class JamDbContext(DbContextOptions<JamDbContext> options)
                : DbContext(options)
            {
                public DbSet<Jam> Jams => Set<Jam>();
                public DbSet<Order> Orders => Set<Order>();

                protected override void OnModelCreating(ModelBuilder model)
                {
                    model.Entity<Order>().HasOne(x => x.Jam).WithMany()
                        .HasForeignKey(x => x.JamId).OnDelete(DeleteBehavior.Restrict);
                    model.Entity<Jam>().Property(x => x.Version).IsConcurrencyToken();
                    model.Entity<Order>().HasIndex(x => new { x.CustomerId, x.RequestId })
                        .IsUnique().HasDatabaseName("UX_Orders_Customer_Request");
                }
            }
            """, Relations, Context, Concurrency, Indexes),
        Card("07-migrations", "Flyt skemaet ét trin ad gangen",
            "Bilbo sammenligner to skemaer, hvor det nye har fået en ekstra kolonne.",
            Model,
            "Versionér databaseskemaet, så ændringer kan gennemgås og gentages.",
            [
                "En migration beskriver forskellen mellem den nuværende EF-model og det sidst registrerede modelsnapshot. Læs både migrationskoden og genereret SQL. dotnet ef migrations add InitialCreate opretter filer. database update anvender udestående ændringer i øvedatabasen.",
                "Kommandoerne forudsætter dotnet-ef, Microsoft.EntityFrameworkCore.Design, den passende Npgsql-udbyder og en konfigureret lokal database. Gem migrationskoden og snapshot i git. I drift kan et gennemgået SQL-script anvendes af et afgrænset deploytrin.",
                "Øvelse med PostgreSQL. Tilføj en valgfri beskrivelse til marmeladen. Se efter AddColumn i den nye migration. Anvend den på en kopi med eksisterende data, og bekræft at tidligere ordrer fortsat kan læses."
            ],
            """
            dotnet ef migrations add InitialCreate
            dotnet ef migrations script --idempotent
            dotnet ef database update
            """, Migrations, Pg),
        Card("08-linq-og-paginering", "Hent kun den næste side",
            "Bilbo vælger et udsnit af en sorteret liste og markerer, hvor den næste side skal begynde.",
            Model,
            "Filtrér på kunde, sortér entydigt og hent kun de felter og rækker, klienten skal bruge.",
            [
                "LINQ bygger en forespørgsel, der først udføres ved ToListAsync. Select vælger felterne til DTO'en. AsNoTracking viser, at læsningen ikke skal følges af en opdatering. Denne projektion indeholder ingen entiteter til tracking.",
                "Brug senest viste Id som cursor i stigende rækkefølge. Hent én ekstra række for at opdage, om der er en side mere. Når der er flere rækker, returneres id'et på den sidste viste række som nextAfterId. Ved stor datamængde kan et indeks på (CustomerId, Id) støtte netop denne forespørgsel.",
                "Kodeøvelse. Opret mindst tre ordrer som samme kunde og én som en anden. Hent sider med limit=2, og følg nextAfterId. Kontrollér rækkefølge, ingen dubletter, og at den anden kundes ordre ikke vises."
            ],
            """
            static async Task<IResult> ListOrders(
                long? afterId, int? limit, ClaimsPrincipal user, JamDbContext db, CancellationToken ct)
            {
                var take = limit ?? 20;
                if (take is < 1 or > 100 || afterId is < 0)
                    return Results.BadRequest();

                var customerId = user.FindFirst("sub")!.Value;
                var start = afterId ?? 0;
                var page = await db.Orders.AsNoTracking()
                    .Where(x => x.CustomerId == customerId && x.Id > start)
                    .OrderBy(x => x.Id)
                    .Select(x => new OrderDto(x.Id, x.JamId, x.Quantity))
                    .Take(take + 1).ToListAsync(ct);

                var items = page.Take(take).ToArray();
                long? nextAfterId = page.Count > take ? items[^1].Id : null;
                return Results.Ok(new { items, nextAfterId });
            }
            """, Paging, Tracking, Indexes),
        Card("09-crud", "Opret, læs, ret og slet",
            "Bilbo retter et varekort ved siden af fire tavler for oprettelse, læsning, ændring og sletning.",
            Model,
            "Brug et katalog til at skelne mellem de fire almindelige måder at arbejde med ressourcer på.",
            [
                "POST /jams opretter en vare og svarer med 201 og Location. GET /jams/{id} læser den. I denne case angiver PUT /jams/{id} det ønskede navn for den eksisterende vare. Lageret ændres af bestillinger i deres eget flow.",
                "DELETE /jams/{id} kan fjerne en vare uden ordrer. Brug 204 ved succes og 409, hvis fremmednøglen blokerer sletningen. En vare med ordrehistorik kan senere få en arkivstatus. Bevar reglen i databasen, også når ændringen kommer via en anden klient.",
                "Kodeøvelse. Tilføj PUT-handleren og kræv JamAdmin-politikken på ruten. Eksemplet opdaterer Version ved en navneændring og svarer 409 på en samtidig ændring. Afprøv succes, ugyldigt navn og id, der ikke findes. Udvid selv kataloget med de tre andre operationer."
            ],
            """
            app.MapPut("/jams/{id:long}", UpdateJam).RequireAuthorization("JamAdmin");

            static async Task<IResult> UpdateJam(
                long id, UpdateJamRequest input, JamDbContext db, CancellationToken ct)
            {
                var name = input.Name?.Trim();
                if (string.IsNullOrEmpty(name) || name.Length > 80)
                    return Results.BadRequest();

                var jam = await db.Jams.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (jam is null) return Results.NotFound();

                jam.Name = name;
                jam.Version = Guid.NewGuid();
                try
                {
                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Results.Conflict();
                }
            }

            public record UpdateJamRequest(string? Name);
            """, Http, Crud, Relations, Concurrency),
        Card("10-atomisk-ordre", "Gem ordre og lager sammen",
            "Bilbo samler en bestillingsseddel og en lagerændring under ét fælles segl.",
            Orders,
            "Gem ordren og lagertrækket i samme SaveChanges-kald, så begge ændringer hører sammen.",
            [
                "Find varen og kontrollér lageret. EF Core sporer den læste vare. Uddraget ændrer lager og version, opretter en ordre og beder derefter EF Core om at gemme hele arbejdsopgaven.",
                "PostgreSQL kan gemme ændringerne atomisk i en transaktion. Når alle ændringer er samlet i ét SaveChanges-kald, gemmes begge ved succes. Fejler en af dem, rulles ændringerne tilbage. Kontrollen af lageret skal suppleres med samtidighedsbeskyttelsen i næste kort.",
                "Kodeøvelse. Metoden er et uddrag, som CreateOrder kalder efter opslag og validering. Start med fem glas, og bestil to. Kontrollér, at én ordre er gemt, og tre glas er tilbage. Lav en separat databasetest, der kalder SaveOrder direkte med en eksisterende RequestId for samme kunde. Kontrollér, at den unikke nøgle afviser forsøget, og at lageret ikke ændres."
            ],
            """
            static async Task<Order> SaveOrder(
                CreateOrderRequest input, string customerId, Jam jam,
                JamDbContext db, CancellationToken ct)
            {
                jam.Stock -= input.Quantity;
                jam.Version = Guid.NewGuid();
                var order = new Order
                {
                    JamId = jam.Id,
                    Quantity = input.Quantity,
                    CustomerId = customerId,
                    RequestId = input.RequestId
                };
                db.Orders.Add(order);
                await db.SaveChangesAsync(ct);
                return order;
            }
            """, Transactions, Crud),
        Card("11-optimistisk-samtidighed", "Når to ordrer rammer samme lager",
            "To bestillinger forsøger at købe det sidste glas, og den ene får en konfliktbesked.",
            Orders,
            "Lad databasen opdage, om lageret er ændret siden den værdi, du læste.",
            [
                "To anmodninger kan begge læse ét glas. Med Version som concurrency token bruger EF Core den oprindelige version i UPDATE-betingelsen. Hver lageropdatering tildeler en ny version. Når den første gemmer, rammer den anden ikke længere den læste version og får en DbUpdateConcurrencyException.",
                "Ét atomisk SaveChanges-kald ruller også den tabende ordres indsættelse tilbage. Håndter konflikten efter det fejlede gemmeforsøg. Ved en ny bestilling kan klienten få 409 og læse aktuel lagerstatus. Ved samme nøgle skal opslaget for gentagelser på næste kort også køres.",
                "Databaselab. Åbn to separate DbContext-instanser og læs samme vare i begge, mens den har ét glas. Forsøg at gemme fra begge. Forvent én ordre, nul glas og én samtidighedsfejl. Undersøg SQL-loggen i dit lokale øvemiljø og find den oprindelige Version i UPDATE-betingelsen."
            ],
            null, Concurrency, Transactions),
        Card("12-unik-noegle-og-gentagelser", "Samme nøgle, samme ordre",
            "Bilbo sammenligner to sedler med samme segl og finder frem til én eksisterende kvittering.",
            Orders,
            "Genkend en gentaget bestilling ved hjælp af en kundenøgle, som databasen håndhæver.",
            [
                "Klienten sender samme RequestId ved gentagelse. Serveren slår op med kombinationen af kundens sub og RequestId. Samme varenummer og antal giver igen den oprindelige ordres 201-svar og Location. En nøgle, der genbruges med en anden vare eller et andet antal, giver 409.",
                "To samtidige anmodninger kan begge passere det første opslag. Det unikke PostgreSQL-indeks lukker kapløbet. Et lagerkapløb kan også vise sig som en samtidighedsfejl. Uddraget genkender de to forventede fejl, rydder kontekstens fejlede ændringer og læser igen efter rollback. Tilføj using Npgsql for PostgresException og fejlkoden. Andre databasefejl går til fælles fejlhåndtering.",
                "Databaselab. Send samme gyldige ordre to gange med samme token og nøgle. Kontrollér identisk Location, én række og ét lagertræk. Gentag samtidig med to separate forbindelser. Prøv samme nøgle med ændret antal og derefter som en anden kunde."
            ],
            """
            static async Task<IResult> CreateOrder(
                CreateOrderRequest input, ClaimsPrincipal user, JamDbContext db, CancellationToken ct)
            {
                var errors = Validate(input);
                if (errors.Count > 0) return Results.ValidationProblem(errors);

                var customerId = user.FindFirst("sub")!.Value;
                var existing = await FindOrder(db, customerId, input.RequestId, ct);
                if (existing is not null) return Replay(existing, input);

                var jam = await db.Jams.SingleOrDefaultAsync(x => x.Id == input.JamId, ct);
                if (jam is null) return Results.NotFound();

                if (jam.Stock < input.Quantity)
                {
                    // Lageret kan allerede være ændret af en identisk ordre.
                    existing = await FindOrder(db, customerId, input.RequestId, ct);
                    return existing is null ? Results.Conflict() : Replay(existing, input);
                }

                try
                {
                    return CreatedOrder(await SaveOrder(input, customerId, jam, db, ct));
                }
                catch (DbUpdateException ex) when (
                    ex is DbUpdateConcurrencyException ||
                    ex.InnerException is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: "UX_Orders_Customer_Request"
                    })
                {
                    db.ChangeTracker.Clear();
                    existing = await FindOrder(db, customerId, input.RequestId, ct);
                    return existing is null ? Results.Conflict() : Replay(existing, input);
                }
            }

            static Task<Order?> FindOrder(
                JamDbContext db, string customerId, Guid requestId, CancellationToken ct) =>
                db.Orders.AsNoTracking().SingleOrDefaultAsync(
                    x => x.CustomerId == customerId && x.RequestId == requestId, ct);

            static IResult Replay(Order order, CreateOrderRequest input) =>
                order.JamId == input.JamId && order.Quantity == input.Quantity
                    ? CreatedOrder(order)
                    : Results.Conflict();

            static IResult CreatedOrder(Order order) =>
                Results.Created($"/orders/{order.Id}", new OrderDto(order.Id, order.JamId, order.Quantity));
            """, Indexes, Concurrency, Transactions, PgErrors),
        Card("13-identitet-og-adgang", "Tjek identitet og adgang",
            "Bilbo kontrollerer først kundens identitet og derefter om kunden har adgang til ordren.",
            Operations,
            "Validér adgangstokenet, og begræns derefter opslaget til den kunde, ordren tilhører.",
            [
                "Autentifikation kontrollerer tokenets signatur, udsteder, modtager og udløb via den konfigurerede OIDC-udsteder. Autorisation kontrollerer, hvad den identificerede bruger må. Kundepolitikken kræver sub, og databaseopslag filtrerer på den værdi.",
                "Installér Microsoft.AspNetCore.Authentication.JwtBearer, og tilføj et using for samme namespace. Sæt Auth:Authority og Auth:Audience fra labbets godkendte tokenudsteder. JamAdmin kræver en permission-værdi fra øvecasen. Tilpas den til udbyderens claims. Kald app.UseAuthentication() og app.UseAuthorization() før rutegrupperne.",
                "Kodeøvelse. Test 401 uden gyldigt token, 403 med gyldigt token uden den krævede tilladelse, og 404 for en kunde, der spørger efter en andens ordrenummer. En kunde skal kunne læse sin egen ordre med 200."
            ],
            """
            var authority = builder.Configuration["Auth:Authority"]
                ?? throw new InvalidOperationException("Auth:Authority mangler.");
            var audience = builder.Configuration["Auth:Audience"]
                ?? throw new InvalidOperationException("Auth:Audience mangler.");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.Audience = audience;
                    options.MapInboundClaims = false;
                });

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("JamCustomer", policy => policy
                    .RequireAuthenticatedUser().RequireClaim("sub"))
                .AddPolicy("JamAdmin", policy => policy
                    .RequireAuthenticatedUser().RequireClaim("permission", "jam:write"));
            """, Authn, Authz, Minimal),
        Card("14-logs-og-fejl", "Skriv logs, du kan følge",
            "Bilbo forbinder hændelser, udfald og fejlopslag med samme blå spormærke.",
            Operations,
            "Log de felter, der gør en hændelse søgbar, og hold klientens fejlbesked kort.",
            [
                "Skriv en beskedskabelon med navngivne felter. En struktureret log gør det muligt at filtrere på hændelse, udfald og trace-id. Et trace-id følger én anmodning. Ordrenummer kan forbinde hændelser for den samme ordre, hvis politikken for data tillader det.",
                "Log resultatet som gemt efter et succesfuldt SaveChanges. Hold tokens, forbindelsesstrenge og komplette request bodies ude af logs. Brug en afgrænset fælles fejlhåndtering med Problem Details til uventede fejl, og lad interne stack traces blive i et begrænset logmiljø.",
                "Kodeøvelse. Tilføj AddProblemDetails før Build og UseExceptionHandler efter Build. I en handler med HttpContext og en injiceret ILogger<Program> placeres loglinjen efter SaveOrder. Fremprovokér en lokal serverfejl, og kontrollér det neutrale 500-svar samt loggen for samme anmodning."
            ],
            """
            // Før builder.Build()
            builder.Services.AddProblemDetails();

            // Efter builder.Build() og før endpoints
            app.UseExceptionHandler();

            // I handleren, efter at ordren er gemt
            logger.LogInformation("Ordre {OrderId} gemt, spor {TraceId}",
                order.Id, httpContext.TraceIdentifier);
            """, Logging, Errors),
        Card("15-integrationstest", "Test hele ordren gennem API'et",
            "Bilbo sender en bestilling gennem øve-API'et og kontrollerer både svaret og det gemte lager.",
            Operations,
            "Test med HTTP og en rigtig PostgreSQL-testdatabase, så du får hele adfærden med.",
            [
                "Installér Microsoft.AspNetCore.Mvc.Testing i testprojektet. WebApplicationFactory kan starte appen for en testklient. Gør Program synlig for testen med public partial class Program { } nederst i appens Program.cs. Brug en isoleret PostgreSQL-testdatabase med migrationerne anvendt. Test den rigtige JWT-validering særskilt, hvis integrationstesten bruger en testidentitet.",
                "I uddraget har klienten samme testidentitet i begge kald. Der findes en vare med id 1 og lager 5. Testen gemmer og genbruger samme RequestId. HTTP-svarene skal give samme Location, og en ny scope skal se én ordre og lager 3.",
                "Testøvelse. Gentag testen med en ny nøgle og tomt lager, samt med den oprindelige nøgle og et ændret antal. Lav et separat samtidighedsforsøg med to scopes, hvor kun én ordre kan få det sidste glas."
            ],
            """
            // client kommer fra jeres konfigurerede WebApplicationFactory<Program>.
            var input = new CreateOrderRequest(1, 2, Guid.NewGuid());
            using var first = await client.PostAsJsonAsync("/orders", input);
            using var retry = await client.PostAsJsonAsync("/orders", input);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
            Assert.NotNull(first.Headers.Location);
            Assert.Equal(first.Headers.Location, retry.Headers.Location);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<JamDbContext>();
            Assert.Equal(1, await db.Orders.CountAsync());
            Assert.Equal(3, (await db.Jams.SingleAsync(x => x.Id == 1)).Stock);
            """, Tests, Strategy),
        Card("16-saml-ordre-api", "Følg ordren hele vejen",
            "Bilbo følger bestillingen fra lugen gennem validering og fælles gemning til kundens kvittering.",
            Operations,
            "Saml bestillingsflowet og vis både den almindelige vej og det, der sker ved konflikt.",
            [
                "Bestillingsruten kræver identitet, validerer DTO'en, tjekker gentagelser og læser varen. Derefter gemmer den ordre og lagertræk atomisk. En efterfølgende læsning bruger Location og filtreres på kundens identitet.",
                "Gentagelsesnøglen giver samme svar efter et tabt netværkssvar. Version beskytter lageret ved samtidighed. Log hvert relevant udfald, og test både HTTP-svar og den gemte tilstand. En forkert anmodning skal kunne afvises uden lagertræk.",
                "Afsluttende lab. Vis en oprettelse, en gentagelse, et ugyldigt input, et forkert ejeropslag og et kapløb om sidste glas. Tegn rute, scope og transaktion. Vedlæg status, Location, én relevant loglinje og det endelige lager for hvert forsøg."
            ],
            null, Minimal, Transactions, Tests)
    ];

    private static CharacterIllustration Card(string file, string title, string alt, string stage, string summary,
        IReadOnlyList<string> explanation, string? code, params CharacterLink[] sources) =>
        new(title, ImageFolder + file + ".png", alt, 1536, 1024)
        {
            Preview = ImageFolder + file + ".webp",
            Stage = stage,
            Summary = summary,
            Explanation = explanation,
            Code = code,
            Sources = sources
        };

    private static CharacterLink Source(string label, string href) => new(label, href, true, false);
}
