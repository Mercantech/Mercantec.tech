# H2 — komplet Teori for dagen

Kilde: Notion H2 Booking. Alle teori-toggles + relevante teorisektioner. Uden elevdata/datoer/passwords.

## 1. Teori for dagen

Teori for dagen

Slides til følgende del findes her - - 

På H2 bygger vi videre på de ting som i lærte på jeres H1 forløb  . 

Her fik vi en grundlæggende forståelse inden for følgende emner:

,  - specielt  &  samt netværksteknologier på !

Det skal vi alt sammen bygge videre på under H2. Den største ændring er selvfølgelig kompleksiteten af jeres projekt samt at vi nu skal bruge  teknologier til at gøre vores applikation mere moderne. Vi går også fra ADO.NET til  som er et nyt værktøj i vores værktøjskasse som vi skal mestre!

H2 tager, ligesom H1, 10 uger. I vil opleve at der er en smule mindre styring end der var på jeres H1, vi giver jer længere tid til direkte at lave projektet. 

Ligesom på H1, modtager I et starter repository igennem . I kan se standart udgaven her -  og neden for kan I læse den inkluderede README.md fil.  

Forudsætninger for H2

Forinden H2 har I selvfølgelig bestået GF2 og H1.

## 2. Teori for dagen

Teori for dagen

Se H2-U1 - [https://h2-u1.mercantec.tech/oop](https://h2-u1.mercantec.tech/oop)

## 3. Teori for dagen

Teori for dagen

Se H2-U1 - [https://h2-u1.mercantec.tech/error-handling](https://h2-u1.mercantec.tech/error-handling)

## 4. Teori for dagen

Teori for dagen

[https://h2-u1.mercantec.tech/data-serialization](https://h2-u1.mercantec.tech/data-serialization)

## 5. Teori for dagen — kontekst: Fredag 🟢 U1 d. \| Fremlæggelse eller aflevering af API Projekt

Teori for dagen

# Backend programmering med C# og Entity Framework

## Introduktion

Slides til følgende emne findes under - - 

Når vi skal bygge en API, er det  og forretningslogik som er hoved fokus. Vi skal dog, ligesom på H1, have en måde at snakke med databasen. Vi bruger som standart, her på H2  som data acces lag og  som datastore. 

## Hvad er EFCore

Entity Framework Core (EF Core) er Microsofts moderne ORM (Object-Relational Mapper) til .NET. Med EF Core kan du arbejde med databaser i C#-kode uden at skrive SQL direkte. Det gør det nemt at oprette, læse, opdatere og slette data i en database via objekter og klasser.

I dette forløb lærer du at bygge et Web API med ASP.NET Core og EF Core, hvor du kan gemme og hente data fra en database (fx Postgres på Supabase). Du kommer til at arbejde med datamodeller, migrationer, endpoints og test af dit API.

## Læringsmål

- Forstå hvad EF Core er, og hvordan det bruges i .NET-projekter.
- Oprette og konfigurere en database med EF Core.
- Oprette datamodeller og DbContext-klasser.
- Udføre migrationer og opdatere databasen.
- Bygge et Web API med CRUD-endpoints (Create, Read, Update, Delete).
- Teste API’et med fx Postman.
- Forstå sammenhængen mellem API, EF Core og database.

## API - Applikation Programming Interface

I har allerede lært at bruge en REST API i mindre grad, men nu skal vi kigge på bygge en selv. På jeres første hoved forløb, lavede vi en databaseservices fil som håndterede alt logikken til at kontakte vores relations database. Her er det 

### Hvorfor bruge en 3-lags arkitektur?

 På H1 brugte vi en 2-lags med  på vores præsentationslag og applikationslag som havde direkte adgang til vores  igennem . Der virkede ret fint og var forholdsvis nemt at holde styr på, men hvorfor skal vi så bruge en 3-lags struktur som ses nedenfor? ⬇

Med en 3-lags struktur har opdeler vi præsentationslaget og applikationslaget, som ellers var samlet før.  

## Opsætning af EFCore med Postgres

Vi skal opsætte EFCore, måske for første gang, herunder er der en guide til hvordan vi kommer i gang med det! \*Projektet er allerede opretter i jeres GitHub Repo så første del er bare dokumentation på hvordan det er gjort! 

# Opsætning og filgennemgang

## Opsætning af standart API Projekt

ASP.NET Core Web API er standarten indenfor API-teknologi med C# og .NET. Der kommer en del med, når vi laver projektet, men langt det meste skal vi selv opbygge efter behov!

Vi bruger .NET 8.0 og tilføjer alt udover Authentication, fordi vi selv skal bygge det og Aspire, da det kan skabe lidt forvirring! Dog vil vi gerne have container support, så vi kan bruge !

Vi skal starte med at installere følgende pakker til vores ASP.NET Core Web API

Gælder kun hvis I bruger , som vi gøre på følgende guide!

Følgende skal bruges, hvis man udvikler på  også kaldet SQL Server

## Filgennemgang

Efter vi har hentet pakkerne, kan vi starte på at opsætte projektet. På nuværende tidspunkt burde i gerne have følgende filer I jeres løsning! Lad os lige gå igennem dem alle og se på hvad de gør

`Connected Services` bruger vi ikke for den her løsning, så den går vi ikke over

`Dependencies` kommer vi til at bruge i løbet af projektet, jeres Nuget Packages burde gerne matche den her →

`Properties` er som for alle andre ASP.NET projekter. Her styre vi om det skal startes som HTTP, HTTPS, IIS eller Docker. 

`Controllers` er vigtig for vores projekt, det er her vi specificere hvad der skal være tilgængelig på vores API og hvilke end-points der skal være tilgængelige. Vi gennemgår den herunder!

`appsettings.json` er som vi kender den, her kan vi holde vigtig information som vores database streng. 

`Dockerfile` er filen som gør at vi kan starte applikationen i en container fx på !

`Program.cs` indeholder vores builder, her starter vi vores program. Den gennemgår vi også senere

`WeatherForecast.cs` indeholder vores klasse, fordi som meget andet C# bygges projektet med  i fokus! 

`WebAPI.http` kan vi bruge til at teste vores API, vi kommer dog til at fokusere på  og  til debugging og generelle test!

### `Controllers` & `Program.cs`

Man kan se `WeatherForecastController.cs` controlleren neden under. Vi tager lige nogle vigtig linjer ud! 

`[ApiController]` & `[Route("[controller]")]` fortæller ASP.NET at det efterfølgende er en API-Controller samt at ruten til at tilgå det specificeres ud fra hver enkel klasse. Så i det her eksempel er 
**`https://localhost:7213/WeatherForecast`**** **ruten for at tilgå vores kontroller.

`[HttpGet(Name = "GetWeatherForecast")]` her specifiere vi hvilken  protokol vi skal bruge. Den er kun konfigureret til `GET` som svare til et read på Database lag, senere ser vi hvordan vi konfigurere resten af CRUD operationerne, altså `POST`, `PUT` og `DELETE` 

```cs
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger _logger;

        public WeatherForecastController(ILogger logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
```

`Program.cs` er noget kortere og noget vi har set i mange andre ASP.NET projekter! 

Mange af dem handler om at tilføje vores kontrollers til API’en. Det er hoveddelen af vores projekt. Et par andre der er værd at nævne er `AddEndpointsApiExplorer();` & `AddSwaggerGen();` de tilføjer  som er et OpenAPI produkt, det gør det dejlig nemt at teste vores applikation!

Hvis man vil teste det på en hosting løsning, er det vigtigt at fjerne vores if-statement. Ellers bliver Swagger-ui ikke tilføjet, så det kan være mere besværligt at teste. Dog skal det fjernes, hvis det er et reelt produkt.

```cs
namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
```

Udskift Swagger med Scalar - Optional 

Swagger bliver fjernet som en standart fra .NET 9.0 og frem. Man kan stadig tilføje den, hvis man er glad for deres UI og måden man bruger det på! Hvis man hellere vil prøve en ny platform kan man installere Scalar som har nogle fordele fremfor !

Vi skal starte med at hente NuGet pakken her - [AspNetCore.Scalar 1.1.7](https://www.nuget.org/packages/AspNetCore.Scalar/)

Bagefter kan vi tilføje den til vores program.cs her er for .NET 8.0

```cs
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});

app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
});
```

Nu har vi mulighed for at åbne siden `scalar/v1` som har vores nye UI. Som standart åbner .NET 8.0 stadig swagger ved opstart, det kan vi ændre i `launchSettings.json` med følgende 

```json
 "https": {
   "commandName": "Project",
   "launchBrowser": true,
   "launchUrl": "scalar/v1",
   "environmentVariables": {
     "ASPNETCORE_ENVIRONMENT": "Development"
   },
   "dotnetRunMessages": true,
   "applicationUrl": "https://localhost:7087;http://localhost:5145"
 },
```

## Opsætning af database

Database på Neon.tech 

I guiden her bruger vi [neon.tech](http://neon.tech) til at opsætte en  database som vi kan bruge til vores projekt.

Når I har oprettet jer, kan man fra ens dashboard få adgang til den connection string vi skal bruge. 

Den skal formateres om til at passe til .NET og det kan vi gøre på følgende måde!

**Original**

postgresql://Elev:\*\*\*\*\*\*\*\*@ep-shrill-king-02053617.eu-central-1.aws.neon.tech/Mercantec?sslmode=require

Version som virker i .NET

I kan eventuelt bruge følgende converter, for nemhed: [Blazor.Mercantec.Tech/DBConverter](https://blazor.mercantec.tech/DBConverter)

Den placere vi i appsettings.json til senere brug i hele applikationen.

```json
"ConnectionStrings": {
  "DefaultConnection": 
  "Host=ep-shrill-king-02053617.eu-central-1.aws.neon.tech;
  Username=Elev;
  Database=Mercantec;
  sslmode=require;"
}
```

Husk at lave en .gitignore fil for jeres appsettings.json, så I ikke komme til at offentliggøre jeres database streng på GitHub! 

## Opsætning af Entity Framework :efcore-logo-ppl:

EF Core er vores valgte **ORM** (Object Relational Mapping). Det er Microsofts officielle løsning, som gør det muligt at arbejde med databaser gennem C#-objekter i stedet for at skrive al SQL manuelt.

Dokumentationen findes her: [EF Core på Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/)

En ORM fungerer som et **bindeled mellem din kode og databasen**:

- Du skriver klasser i C# (f.eks. `User`, `Booking`, `Room`)
- EF Core oversætter automatisk disse klasser til tabeller i databasen
- Når du ændrer objekter i koden, genererer EF Core SQL-forespørgsler i baggrunden

Fordelene ved at bruge en ORM som EF Core er:

- Mindre manuel SQL – fokus på logik fremfor syntaks
- Stærk typesikkerhed (compile-time fejl i stedet for runtime fejl)
- Bedre vedligeholdelse, da model og database holdes synkroniseret
- Mulighed for **migrations**, som automatisk opdaterer databasen, når modellen ændres

---

Vi har hentet alle de pakker vi skal bruge og er nu klar til at opsætte EFCore i vores projekt. Vi starter med at tilføje følgende til `Program.cs` -filen. Husk at gøre det inden linjen med - `var app = builder.Build();`

```cs
IConfiguration Configuration = builder.Configuration;

string connectionString = Configuration.GetConnectionString("DefaultConnection") 
?? Environment.GetEnvironmentVariable("DefaultConnection");

builder.Services.AddDbContext(options =>
		options.UseNpgsql(connectionString));
```

Som vi kan se bruger vi `AppDBContext`, som er en fil vi selv har lavet. Den ser vi lidt på her! 

```cs
public class AppDBContext : DbContext
{
    public AppDBContext(DbContextOptions options)
        : base(options)
    {
    }
}
```

Da vi har konfigureret vores connection-streng i `Program.cs` filen, behøver vi ikke yderligere information indtil nu med vores `AppDBContext.cs`. 

Nu er EFCore teknisk set blevet sat op, men vi mangler at tilføje nogle modeller som skal mappes til tabeller i vores SQL-database. 

```cs
public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is Common &&
                            (e.State == EntityState.Added 
                            || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var entity = (Common)entry.Entity;

                if (entry.State == EntityState.Added)
                {
                    if (string.IsNullOrEmpty(entity.Id))
                    {
                        entity.Id = Guid.NewGuid().ToString();
                    }
                    entity.CreatedAt = DateTime.UtcNow;
                }

                entity.UpdatedAt = DateTime.UtcNow;
            }
        }   
```

Design af User tabel 

Den første tabel i vores system, er næsten altid en User tabel. 

### Opsætning af User modellen til JWT og BCrypt

Vi opretter 2 modeller (klasser) til at starte med, en `Common.cs` og en `User.cs`. Begge bliver enten placeret i en “Models” mappe i vores API projekt eller i et Class Library

Vi starter med at se på `Common.cs`

```cs
public class Common
{
    [Key]
    public string Id { get; set; } // Kan erstattes med "int Id"
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Formålet med `Common.cs` er at alle klasser som har relation til databasen skal arve fra den. Id, CreatedAt og UpdatedAt er alle gode at have i vores database som standart.

```cs
public class User : Common
{
    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string Salt {  get; set; }
    public DateTime LastLogin { get; set; }
    
    // Only for educational purposes, not in the final product!
}
```

Vi laver en ret standart `User.cs` klasse som arver fra vores Common. Der kan være langt flere felter, som vi tilføjer senere, men det her er en god basis for en User klasse. 

### Tilføje klasser til databasen - DbSet\

For at mappe vores klasser til tabeller i databasen skal vi bruge den indbyggede DbSet fra EFCore. [DbSet\ Class (Microsoft.EntityFrameworkCore)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbset-1?view=efcore-8.0)

Her redigere vi vores `AppDBContext.cs` klasse, til også at inkludere vores User model. 

```cs
public DbSet Users { get; set; }
```

Nu er vi klar til at sende vores ændringer ned til en database, inden det skal vi lave en migration

### Migrationer - add-migraiton

Migrationer er vores måde at oprette og vedligeholde tabeller i vores database. Med EFCore kan vi automatisk genere migrationer ud fra vores `AppDBContext` og ud fra hvordan vores klasser er struktureret. Her bruger vi pakken  - som giver os adgang til kommandoer i Package Manager Console (PM). Den første kommando vi skal lære at bruge er `add-migration` - vores `AppDBContext` er faktisk klar til den første migration. Vi åbner PM og skriver følgende `add-migration init-user` , herefter bliver der oprettet mappen “Migrations” og en fil der slutter med `init-user.cs` . I den fil er der beskrevet hvordan migrationen adaptere databasen til at passe med vores klasser. 

```cs
public partial class inituser : Migration
{
    /// 
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column(type: "text", nullable: false),
                Email = table.Column(type: "text", nullable: false),
                Username = table.Column(type: "text", nullable: false),
                Salt = table.Column(type: "text", nullable: false),
                LastLogin = table.Column(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });
    }

    /// 
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Users");
    }
}
```

### Visualisere databasen - EFCore Powertools

Vi kan bruge en Visual Studio Extension kaldet [**EF Core Power Tools**](https://marketplace.visualstudio.com/items?itemName=ErikEJ.EFCorePowerTools)**. **

Der er mange funktioner, men det vi bruger her hedder **Add DbContext Diagram. **Den laver et diagram over vores database ud fra migrations-mappen, det betyder også at vi ikke behøver at opdatere vores database for at få diagrammet og vi derfor kan tjekke inden, hvad vi faktisk har konfigureret. 

### Opdatere databasen - Update-database

Efter at vi har tjekket vores migrationsfil og eventuelt DbContext Diagrammet, er vi nu klar til at skubbe det til vores database. Det gør vi også fra vores PM med kommandoen `Update-database` som selv holder øje med hvor mange migrationer som mangler at bliver lagt ud på databasen. 

Hvis den ikke melder fejl, burde alt være i orden på databasesiden. Det kan vi tjekke med et værktøj såsom . 

Vi forbinder med de samme informationer som er i vores appsettings.json

Herfra kan vi verificere at vores SQL struktur matcher vores ønsker fra EFCore. Udover det ser vi også en \_EFMigrationsHistory tabel, som indeholder et ID på vores migration og matcher med vores migrationsmappe fra projektet.

# 🐘 Opsætning af PostgreSQL med Docker

Vi har tidligere arbejdet med databaser i skyen, eksempelvis gennem Neon.tech. Men vi kan også køre vores egen PostgreSQL-database lokalt ved hjælp af Docker!

Det giver os mulighed for at udvikle og teste vores applikationer uden at være afhængige af en ekstern databaseudbyder.

Vi kommer til at arbejde med to forskellige metoder:

1. **Docker Run** – Vi starter PostgreSQL med én kommando.
2. **Docker Compose** – Vi opsætter PostgreSQL sammen med pgAdmin, så vi kan administrere vores database direkte fra browseren.

## 🐳 Metode 1 – PostgreSQL med én Docker-kommando

Den første metode er den hurtigste. Her starter vi en PostgreSQL-container direkte gennem terminalen.

### Trin 1 – Start Docker Desktop

Inden vi går i gang, skal Docker Desktop være installeret og startet.

Åbn en terminal, eksempelvis PowerShell, og kontrollér at Docker fungerer:

```txt
docker --version
docker info
```

Hvis Docker fungerer, er vi klar!

### Trin 2 – Opret PostgreSQL-containeren

Vi bruger det officielle PostgreSQL Docker-image.

Kør følgende kommando i PowerShell:

```txt
```

**Hvad betyder de forskellige parametre?**

| Parameter | Forklaring |
| --- | --- |
| `docker run` | Opretter og starter en container |
| `-d` | Kører containeren i baggrunden |
| `--name postgres-db` | Navnet på vores container |
| `POSTGRES_USER` | Brugernavnet til databasen |
| `POSTGRES_DB` | Navnet på databasen, som oprettes |
| `-p 5432:5432` | Gør PostgreSQL tilgængelig på vores computer |
| `-v postgres_data:...` | Gemmer databasedata i et Docker-volume |
| `postgres:17` | PostgreSQL version 17 |

**Hvorfor bruger vi et volume?**

Normalt kan data i en container forsvinde, når containeren slettes.

Med et Docker-volume gemmer vi databasen separat fra selve containeren. Det betyder, at vi kan stoppe, slette og genoprette containeren uden at miste vores data, så længe vi genbruger samme volume.

### Trin 3 – Kontrollér at databasen kører

```txt
docker ps
```

Her burde vi kunne se vores `postgres-db` container.

Vi kan også åbne Docker Desktop og finde containeren under **Containers**.

Hvis vi vil kontrollere, at PostgreSQL er klar til forbindelser:

```txt
docker exec postgres-db pg_isready -U admin -d mercantec
```

Vi burde få beskeden:

`accepting connections`

### Trin 4 – Forbind fra ASP.NET Core

Nu kan vi forbinde vores API til databasen med en connection string.

I `appsettings.Development.json` kan vi skrive:

```txt
{
  "ConnectionStrings": {
  }
}
```

Herefter kan vi bruge EF Core præcis som tidligere i guiden.

**Bemærk:** Eksemplet bruger en simpel adgangskode til lokal undervisning. I rigtige projekter skal vi bruge stærke adgangskoder og holde dem ude af Git.

---

# 🐳 Metode 2 – Docker Compose med PostgreSQL og pgAdmin

Når vi arbejder med større projekter, har vi ofte brug for flere containere.

Vi kan eksempelvis have:

- PostgreSQL – Vores database.
- pgAdmin – En webbaseret brugerflade til databasen.
- ASP.NET Core API – Vores backend, som vi senere også kan tilføje til Compose.

I stedet for at starte hver container manuelt kan vi bruge **Docker Compose** til at definere hele vores miljø i én fil.

## Trin 1 – Opret projektmappen

Opret en ny mappe, eksempelvis:

`PostgresDocker`

Inde i mappen opretter vi to filer:

```txt
PostgresDocker/
├── compose.yaml
└── .env
```

`compose.yaml` indeholder vores Docker-konfiguration.

`.env` indeholder miljøvariabler, såsom brugernavne og adgangskoder.

## Trin 2 – Opret vores .env-fil

```txt
POSTGRES_USER=admin
POSTGRES_DB=mercantec

PGADMIN_EMAIL=admin@mercantec.dk
```

Vi bruger disse værdier i vores Compose-fil, så vi ikke behøver at skrive dem flere steder.

**Vigtigt:** Tilføj `.env` til `.gitignore`, så adgangskoderne ikke bliver uploadet til GitHub.

## Trin 3 – Opret compose.yaml

```txt
services:
  postgres:
    image: postgres:17
    container_name: mercantec-postgres
    restart: unless-stopped

    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_DB: ${POSTGRES_DB}

    ports:
      - "127.0.0.1:5432:5432"

    volumes:
      - postgres_data:/var/lib/postgresql/data

    healthcheck:
      test:
        - CMD-SHELL
        - pg_isready -U "$$POSTGRES_USER" -d "$$POSTGRES_DB"
      interval: 10s
      timeout: 5s
      retries: 5

  pgadmin:
    image: dpage/pgadmin4:9
    container_name: mercantec-pgadmin
    restart: unless-stopped

    environment:
      PGADMIN_DEFAULT_EMAIL: ${PGADMIN_EMAIL}

    ports:
      - "127.0.0.1:8080:80"

    volumes:
      - pgadmin_data:/var/lib/pgadmin

    depends_on:
      postgres:
        condition: service_healthy

volumes:
  postgres_data:
  pgadmin_data:
```

### Hvad har vi oprettet?

Vi har nu defineret to services i samme Docker Compose-projekt.

**PostgreSQL**

Vores database, som bliver tilgængelig på `localhost:5432`.

**pgAdmin**

En webbaseret databaseadministrator, som bliver tilgængelig på `http://localhost:8080`.

**Volumes**

Vi har oprettet to volumes, så både databaseindhold og pgAdmin-indstillinger bliver gemt.

**depends\_on**

Vi fortæller Docker, at pgAdmin først skal starte, når PostgreSQL er klar.

## Trin 4 – Start hele miljøet

Åbn PowerShell i mappen med vores `compose.yaml`.

Kør:

```txt
docker compose up -d
```

Docker downloader automatisk de nødvendige images og starter vores containere.

Vi kan kontrollere status med:

```txt
docker compose ps
```

Nu burde vi have to containere kørende i Docker Desktop!

## Trin 5 – Åbn pgAdmin i browseren

Åbn følgende adresse:

[http://localhost:8080](http://localhost:8080/)

Log ind med oplysningerne fra vores `.env`-fil.

**Email:** `admin@mercantec.dk`

Vi har nu adgang til pgAdmin, men vi skal stadig oprette forbindelsen til vores PostgreSQL-server.

## Trin 6 – Forbind pgAdmin til PostgreSQL

I pgAdmin:

1. Højreklik på **Servers**.
2. Vælg **Register → Server**.
3. Under **General** angiver vi navnet `Mercantec PostgreSQL`.
4. Under **Connection** udfylder vi følgende:

| Felt | Værdi |
| --- | --- |
| Host name/address | `postgres` |
| Port | `5432` |
| Maintenance database | `mercantec` |
| Username | `admin` |

Tryk på **Save**.

Vi kan nu navigere rundt i vores PostgreSQL-database direkte fra browseren.

### Hvorfor bruger vi postgres og ikke localhost?

Det er en vigtig forskel!

Når pgAdmin kører inde i Docker, betyder `localhost` selve pgAdmin-containeren.

Docker Compose opretter automatisk et internt netværk, hvor containerne kan finde hinanden ved hjælp af deres service-navne.

Derfor bruger vi:

`postgres`

Når vi forbinder fra vores egen computer, eksempelvis Visual Studio, bruger vi derimod:

`localhost`

---

# 🔗 Forbind vores ASP.NET Core API med databasen

Vi kan nu forbinde vores eksisterende ASP.NET Core-projekt til PostgreSQL.

Hvis vores API kører direkte fra Visual Studio, bruger vi følgende connection string:

```txt
{
  "ConnectionStrings": {
  }
}
```

I vores `Program.cs` registrerer vi vores DbContext:

```txt
using Microsoft.EntityFrameworkCore;

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

builder.Services.AddDbContext(options =>
    options.UseNpgsql(connectionString));
```

Dette skal placeres inden:

```txt
var app = builder.Build();
```

Når vi har oprettet vores modeller og DbContext, kan vi lave migrationer fra Package Manager Console:

```txt
Add-Migration InitialCreate
Update-Database
```

Vi kan også bruge .NET CLI, hvis `dotnet-ef` er installeret:

```txt
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Nu kan vi åbne pgAdmin, navigere til vores database og finde tabellerne under:

`Databases → mercantec → Schemas → public → Tables`

Vi burde også kunne finde tabellen `__EFMigrationsHistory`, som EF Core bruger til at holde styr på gennemførte migrationer.

---

# 🚀 Ekstra – Kør også vores API i Docker Compose

Vi kan faktisk udvide vores Compose-miljø med endnu en container, som kører vores ASP.NET Core API.

Så har vi tre services:

```txt
Docker Compose
│
├── PostgreSQL
│   └── Port 5432
│
├── pgAdmin
│   └── Port 8080
│
└── ASP.NET Core API
    └── Port 5000
```

Vi kan tilføje følgende service under `services:` i vores eksisterende `compose.yaml`:

```txt
  api:
    build:
      context: ./WebAPI
      dockerfile: Dockerfile

    container_name: mercantec-api
    restart: unless-stopped

    ports:
      - "127.0.0.1:5000:8080"

    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_HTTP_PORTS: 8080

    depends_on:
      postgres:
        condition: service_healthy
```

Her forudsætter vi, at vores ASP.NET Core-projekt ligger i undermappen `WebAPI`, og at projektet har en fungerende Dockerfile, som starter API'et på port 8080.

Bemærk især denne del:

```txt
ConnectionStrings__DefaultConnection:
```

I .NET kan vi bruge dobbelt underscore `__` i miljøvariabler til at angive en sektion i vores konfiguration.

Det svarer til `ConnectionStrings:DefaultConnection` i `appsettings.json`.

Og fordi vores API nu kører i Docker, bruger vi `Host=postgres` frem for `Host=localhost`.

Start derefter alle tre services:

```txt
docker compose up -d --build
```

Nu har vi både database, databaseadministration og backend samlet i ét Docker Compose-projekt!

---

# 🛠️ Nyttige Docker-kommandoer

| Kommando | Funktion |
| --- | --- |
| `docker compose up -d` | Starter vores services |
| `docker compose up -d --build` | Genbygger og starter services |
| `docker compose ps` | Viser status |
| `docker compose logs -f` | Viser logs |
| `docker compose restart` | Genstarter services |
| `docker compose down` | Stopper og fjerner containere |
| `docker compose down -v` | Stopper og sletter også volumes |
| `docker volume ls` | Viser Docker-volumes |

⚠️ **Pas på med ****`docker compose down -v`****!**

Denne kommando sletter vores Compose-volumes og dermed også de data, vi har gemt i PostgreSQL. Brug den kun, hvis databasen gerne må nulstilles.

Hvis port 5432 allerede er optaget, eksempelvis af PostgreSQL fra metode 1, skal vi stoppe den eksisterende container eller ændre porten i Compose.

---

# 🎯 Opgave – Opsæt jeres egen database

Nu skal I selv prøve at opsætte PostgreSQL med Docker.

**Del 1 – Docker**

- Start PostgreSQL med én Docker-kommando.
- Kontrollér at containeren kører i Docker Desktop.
- Opret forbindelse til databasen.
- Stop og start containeren igen.

**Del 2 – Docker Compose**

- Opret en `compose.yaml` og `.env`.
- Tilføj PostgreSQL og pgAdmin.
- Start begge containere med én kommando.
- Åbn pgAdmin i browseren.
- Opret forbindelse til PostgreSQL.

**Del 3 – Entity Framework Core**

- Forbind jeres ASP.NET Core API til PostgreSQL.
- Opret en model og tilføj den til jeres DbContext.
- Opret en migration.
- Opdater databasen.
- Kontrollér tabellen i pgAdmin.

**Ekstra udfordring**

Få jeres ASP.NET Core API til også at køre i Docker Compose, så hele løsningen kan startes med:

```txt
docker compose up -d --build
```

Når I er færdige, skal I kunne forklare forskellen på `localhost` og et Docker-service-navn, hvorfor vi bruger volumes, og hvad fordelen er ved Docker Compose frem for almindelige Docker-kommandoer.

# 🖥️ Alternativer til pgAdmin – Find din favorit

pgAdmin er langt fra det eneste værktøj, vi kan bruge til at administrere PostgreSQL.

Der findes mange forskellige databaseværktøjer, og de har hver deres fordele. Nogle kører direkte i browseren, mens andre installeres på computeren.

Det vigtigste er, at de alle kan forbinde til den samme PostgreSQL-database!

## 1. CloudBeaver – Moderne databaseadministration i browseren

CloudBeaver er en webbaseret databaseadministrator fra udviklerne bag DBeaver.

**Fordele:**

- Moderne webinterface med SQL-editor.
- Understøtter PostgreSQL og flere andre databaser.
- Kan køres i Docker.
- God til udviklingsmiljøer og samarbejde.

**Installation med Docker:**

```txt
docker run -d --name cloudbeaver -p 127.0.0.1:8978:8978 -v cloudbeaver_data:/opt/cloudbeaver/workspace dbeaver/cloudbeaver:latest
```

Åbn `http://localhost:8978` og gennemfør førstegangsopsætningen.

Hjemmeside: [https://dbeaver.com/cloudbeaver/](https://dbeaver.com/cloudbeaver/)

## 2. Adminer – Simpelt og hurtigt

Adminer er et meget let databaseværktøj, som fungerer direkte i browseren.

Det er især velegnet, hvis vi bare hurtigt skal undersøge tabeller, data eller udføre SQL-forespørgsler.

**Fordele:**

- Meget simpelt interface.
- Kræver få ressourcer.
- Understøtter flere databasesystemer.
- Kan startes med én Docker-kommando.

**Installation med Docker:**

```txt
docker run -d --name adminer -p 127.0.0.1:8081:8080 adminer:6
```

Åbn `http://localhost:8081`.

Hvis Adminer skal forbinde til PostgreSQL fra vores tidligere Compose-projekt, skal containerne være på samme Docker-netværk. Alternativt kan Adminer tilføjes som en service i samme `compose.yaml`.

Hjemmeside: [https://www.adminer.org/](https://www.adminer.org/)

## 3. DBeaver – Databaseværktøj til udviklere

DBeaver er et populært databaseprogram, som installeres på computeren.

Det understøtter mange forskellige databasesystemer og indeholder en lang række funktioner til både udvikling og databaseadministration.

DBeaver installeres normalt direkte på Windows, macOS eller Linux frem for at køre som en webcontainer.

Hjemmeside: [https://dbeaver.io/](https://dbeaver.io/)

**Fordele:**

- Gratis Community Edition.
- Avanceret SQL-editor.
- Database- og relationsdiagrammer.
- Import og eksport af data.
- Understøtter PostgreSQL, MySQL, SQL Server og mange andre.

## 4. TablePlus – Minimalistisk og moderne

TablePlus er et databaseprogram med fokus på et enkelt og overskueligt interface.

Det er især interessant for udviklere, som gerne vil have et hurtigt værktøj uden alt for mange menuer.

**Fordele:**

- Moderne og minimalistisk design.
- Nem navigation mellem tabeller.
- Indbygget SQL-editor.
- Understøtter mange databasesystemer.
- Findes til både Windows og macOS.

TablePlus har en gratis version med begrænsninger og tilbyder også betalte licenser.

Hjemmeside: [https://tableplus.com/](https://tableplus.com/)

---

## ⚖️ Hvilket værktøj skal I vælge?

| Værktøj | Type | Docker | Gratis mulighed |
| --- | --- | --- | --- |
| pgAdmin | Web / Desktop | ✅ | ✅ |
| CloudBeaver | Web | ✅ | ✅ |
| Adminer | Web | ✅ | ✅ |
| DBeaver | Desktop | Ikke nødvendigt | ✅ |
| TablePlus | Desktop | Ikke nødvendigt | Begrænset |

**Min anbefaling:** Prøv mindst to forskellige værktøjer. Hvis I gerne vil have en databaseadministrator i Docker, er CloudBeaver et interessant alternativ til pgAdmin. Hvis I foretrækker et program på computeren, er DBeaver et godt valg.

### 🎯 Ekstraopgave

- Vælg et alternativ til pgAdmin.
- Installér eller start værktøjet.
- Forbind til jeres PostgreSQL-database.
- Find tabellerne oprettet gennem EF Core.
- Udfør en simpel `SELECT`-forespørgsel.
- Forklar, hvilket værktøj I foretrækker, og hvorfor.

**Husk:** Værktøjerne er blot forskellige brugerflader til databasen. Selve PostgreSQL-databasen og vores EF Core-kode ændrer sig ikke, når vi skifter værktøj.

## 6. Teori for dagen — kontekst: Tirsdag 🟢 U2 d. \| Roller og Hashing

Teori for dagen

## Roller og grupper

Her dækker vi hvordan man lave en 1:1 relation i vores database. Hver “User” skal have en af de 4 roller, som er defineret i opgave beskrivelsen. 

- 
Bruger/Kunde

- 
Admin

- 
Receptionist 

- 
Rengøring

Hvordan vi i programmet konfiguere og bruger de 4-roller kommer i næste sektion!

## Introduktion til API-sikkerhed

Når vi bygger API'er, udsætter vi data og funktionalitet for omverdenen. Derfor er det vigtigt at sikre API’en, så:

- Kun godkendte brugere har adgang til visse ressourcer.
- Brugere ikke kan foretage ændringer, de ikke har rettigheder til.
- Systemet ikke kan misbruges af automatiserede angreb eller uautoriserede aktører.

### **👮 Autentifikation vs Autorisation**

- **Autentifikation**: Hvem er du? (F.eks. login med brugernavn og kodeord)
- **Autorisation**: Hvad må du gøre? (F.eks. må du kun læse data, ikke opdatere)

Vi dækker det basale af begge emner her og under sektionen - JWT - JSON Web Token :jwt: 

## Roller og Autorisation i Applikationer

Når vi bygger et system, hvor brugere har forskellige rettigheder og adgangsniveauer, har vi brug for et **rollesystem**. Det giver os mulighed for at kontrollere, **hvem der må hvad** – altså **autorisation**.

### 🎯 Hvorfor et rollesystem?

- Brugere skal have forskellige rettigheder (f.eks. kunde, receptionist, admin)
- Vi skal kunne beskytte visse endpoints eller data
- Det skaber struktur og sikkerhed i backend-logikken

---

## 🔐 Typer af rollesystemer

### **RBAC – Role-Based Access Control**

- Brugeren får tildelt én eller flere roller.
- Adgang er defineret ud fra disse roller.
- Let at administrere og forstå - Anbefalet på H2!

[https://www.ibm.com/think/topics/rbac](https://www.ibm.com/think/topics/rbac)

**Eksempel:**

> En bruger med rollen Receptionist kan se alle bookinger, men ikke slette brugere.

```txt
User → Role → Permissions
```

### **ABAC – Attribute-Based Access Control**

- Adgang baseres på attributter – både på brugeren og det objekt, de prøver at tilgå.
- Mere fleksibelt men også mere komplekst.

[https://aws.amazon.com/blogs/security/control-access-to-amazon-elastic-container-service-resources-by-using-abac-policies/](https://aws.amazon.com/blogs/security/control-access-to-amazon-elastic-container-service-resources-by-using-abac-policies/)

**Eksempel:**

> En bruger må kun tilgå bookinger, hvis user.Department == booking.Department.

```txt
Access = f(User attributes, Resource attributes, Environment)
```

---

## Hvor gemmer vi rollerne?

VI har flere muligheder:

### 1. **I databasen (mest normalt og fleksibelt)**

- En separat `Role` tabel (Anbefalet på H2)
- Brugeren har en relation til én eller flere roller

### 2. **Hardcodet i koden**

- Upraktisk og svært at vedligeholde
- Kan bruges i små projekter eller til hurtige tests

### 3. **I claims på JWT-tokenet**

- Når en bruger logger ind, tilføjes deres rolle(r) som claims i tokenet.
- Effektivt til at kontrollere adgang uden at slå op i databasen for hver request

Her på H2, kombinere vi 1. og 3. - Det gør vi primært for at få det bedste af begge verdner. Vi bruger databasen til at have et centralt sted at opbevare og redigere rollerne for hver bruger, og vi gemmer det i vores JWT, for at give vores frontend kontekst til brugerens rettigheder uden database kald! Det kommer dog med nogle udfordringer som vi tager senere!

---

## 🧑‍💻 Eksempel i C# og EF Core (1:N relation)

### 🔧 Datamodeller

```cs
// Common.cs
public class Common
{
    [Key]
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Role.cs
public class Role : Common
{
    public required string Name { get; set; }

    // Navigation til brugeren (valgfrit ved 1:N)
    public List Users { get; set; } = new();
}
```

```cs
// User.cs
public class User : Common
{
    public required string Email { get; set; }

    // FK + navigation til rolle (én rolle pr. bruger)
    public string RoleId { get; set; } = default!;  // navigation 
    public Role Role { get; set; } = default!;
}
```

### 📘 DbContext

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // 1:n: User -> Role
    modelBuilder.Entity()
        .HasOne(u => u.Role)
        .WithMany(r => r.Users)
        .HasForeignKey(u => u.RoleId)
        .OnDelete(DeleteBehavior.Restrict); 
        // undgå at slette rolle hvis der findes users

    // (Nice-to-have) Unikt navn på roller
    modelBuilder.Entity()
        .HasIndex(r => r.Name)
        .IsUnique();
}
```

---

## ✅ Eksempel på brug i praksis

Når du logger ind og genererer et JWT-token, kan du lægge brugerens rolle med som claim:

```cs
new Claim(ClaimTypes.Role, user.Role.Name)
```

Og senere beskytte endpoints med:

```cs
[Authorize(Roles = "Admin,Receptionist")]
```

Her har vi lavet en 1:1 relation, det snakker vi lidt om i næste sektion - både omkring hvordan vi udvider det og hvordan vi med den nuværende relation kan lave et hierarkiets system som ofte kan være nemmere at administrere for et mindre system. Til H2 anbefaler vi dog at bruge et multirollesystem, som vi kigger på i næste del!

## Fordele ved 1:N-relation til roller

I vores første version af booking systemet ser vi på **ét rollesystem pr. bruger** – altså en **1:N-relation mellem User og Role**.

Det betyder, at **hver bruger kun har én rolle ad gangen**, og det skaber et **tydeligt hierarki og en simpel adgangsstruktur**.

### 🔑 Hierarkisk rollesystem

Rollerne er designet sådan, at **hver højere rolle nedarver rettighederne fra den forrige**:

Rolle

Rettigheder

**Admin**

Alt, inkl. oprettelse af brugere og systemstyring

**Receptionist**

Alt hvad **Rengøring** kan, plus bookinger

**Rengøring**

Se værelser, som skal gøres rene

**Kunde**

Book værelser og se egne bookinger

Det vil sige at en **Receptionist** kan både se værelser (som rengøringspersonalet) **og** administrere bookinger. En **Admin** kan det hele – inkl. oprette brugere, slette data, mv.

### Hvorfor vælge 1:N?

- ✅ **Simpelt at forstå og implementere**
  
  Kun ét felt (`RoleId`) pr. bruger – intet behov for mange-til-mange-tabeller
  

- ✅ **Let at kode og validere**
  
  Vi kan nemt bruge `[Authorize(Roles = "Receptionist")]` i vores controller
  

- ✅ **Sikkert og forudsigeligt**
  
  Ingen overlap – du ved præcis hvad en bruger må
  

- ✅ **God læring for begyndere**
  
  Fokus på principperne bag autorisation og hierarki – uden ekstra kompleksitet
  

### Klar til udvidelse: Multirolle-system

Selvom vi starter med at forklare 1:N-model, er systemet **designet med tanke på fremtiden**. Hvilket betyder at vi ønsker at **tillade flere roller pr. bruger** (fx en bruger som både er rengøring og receptionist), kan vi opgradere til et **m:n. **Det er primært ud fra at vi gerne på sigt vil implementere nogle unikke rettigheder, hvor der ikke nødvendigvis er tydeligt hierarki

### Hvorfor bruge BCrypt?

- Langsomt og derfor svært at brute-force
- Automatiseret salt (beskytter mod rainbow table-angreb)
- Veletableret og gennemtestet

### 📦 Installation

Tilføj pakken `BCrypt.Net-Next` via NuGet - her er kommandoen for at gøre det i terminalen og her er pakken man kan hente gennem VS - [BCrypt.Net-Next 4.0.3](https://www.nuget.org/packages/BCrypt.Net-Next)

```shell
dotnet add package BCrypt.Net-Next
```

### Oprettelse af hash

Når en bruger opretter sig:

```cs
```

### Validering ved login

Når en bruger forsøger at logge ind:

```cs
```

- Når vi kalder `Verify`, sker følgende:
  3. Den **sammenligner de to hashes** – hvis de matcher, returnerer den `true`.

### Eksempel:

```cs

```

- Hvis ikke, bliver det `false`.

### ⚠️ Bemærk:

### Hvorfor bruger vi salt? 🧂

### Scenario 1: Uden Salt (Usikkert)

Hash (uden salt)

`e10adc3949ba59abbe56e057f20f883e`

`sommer2024`

`a8f5f167f44f4964e6c998dee827110c`

`e10adc3949ba59abbe56e057f20f883e`

### Scenario 2: Med BCrypt's Automatiske Salt (Sikkert)

Bruger

Salt (Auto-genereret)

Resulterende Hash (Unik)

Alice

`salt_ABC`

`$2a$11$salt_ABC...hash_result_1`

Bob

`salt_XYZ`

`$2a$11$salt_XYZ...hash_result_2`

### Brug det i en Controller

I din `UserController` eller login-registrerings flow:

2. **Ved login:** Hent hash fra databasen og brug `Verify`.

Før at vi kan lave et godt endpoint i vores controller, skal vi bruge en DTO, altså et data-transfer-objekt. Dem kan vi lave på følgende måde

```cs
// DTO til registrering
public class RegisterDto
{
    public required string Email { get; set; } = string.Empty;
    public required string Username { get; set; }

}

// DTO til login
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
}
```

Eksempel med et `register`-controller:

```cs
// POST: api/user/register
[HttpPost("register")]
public IActionResult Register([FromBody] RegisterDto dto)
{
	if (_context.Users.Any(u => u.Email == dto.Email))
	  return BadRequest("En bruger med denne email findes allerede.");

  var user = new User
  {
	   Id = Guid.NewGuid().ToString(),
		 Email = dto.Email,
		 Username = dto.Username,
  };

  _context.Users.Add(user);
  _context.SaveChanges();

  return Ok(new { message = "Bruger oprettet!", user.Email });
}
```

Eksempel i en `login`-controller:

```cs
public IActionResult Login(LoginDto dto)
{
    var user = _context.Users.FirstOrDefault(u => u.Email == dto.Email);
        return Unauthorized("Forkert brugernavn eller adgangskode");

    // Fortsæt med at generere JWT osv.
    return Ok("Login godkendt!");
}
```

Eksempel på komplet Usercontroller med `register` og `login`

```cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/user/register
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterDto dto)
    {
        if (_context.Users.Any(u => u.Email == dto.Email))
            return BadRequest("En bruger med denne email findes allerede.");

        var user = new User
        {
            Email = dto.Email,
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        return Ok(new { message = "Bruger oprettet!", user.Email });
    }

    // POST: api/user/login
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        var user = _context.Users.FirstOrDefault(u => u.Email == dto.Email);
        if (user == null)
            return Unauthorized("Forkert brugernavn eller adgangskode.");

            return Unauthorized("Forkert brugernavn eller adgangskode.");

        // Her kunne vi generere en JWT og returnere den
        return Ok(new { message = "Login godkendt!", user.Email, user.Role });
    }
}

```

## 7. Teori for dagen — kontekst: Tirsdag 🟢 U2 d. \| Roller og Hashing

Teori for dagen

Når vi designer vores database til et hotelsystem, er det vigtigt at forstå de tre centrale relationstyper i datamodellering:

- **1:1** – Én entitet har præcis én relateret entitet
- **1:n** – Én entitet har flere relaterede entiteter
- **n:m** – Mange entiteter kan være relateret til mange andre

Alle tre typer skal bruges i vores bookingsystem – og vi implementerer dem med **Entity Framework Core (EF Core)**, som oversætter vores C#-modeller til **SQL-tabeller og relationer**.

---

### 🧍 1:1 – **User og Userinfo**

I et hotelsystem har vi ofte brug for at gemme både **loginoplysninger** og mere **detaljerede profiloplysninger** om en bruger.

Hvis vi gemmer alt i én stor tabel, vil alle queries hente en masse data, vi ikke altid har brug for – hvilket gør systemet tungere.

Ved at **normalisere** deler vi data op i to tabeller med en **1:1-relation**:

- **UserInfo**: Ekstra oplysninger, vi kun henter, når vi faktisk har brug for dem (adresse, telefon, avatar osv.).

Dette betyder:

- Hurtigere queries på de mest almindelige kald (login, brugeroversigter).
- Mindre belastning af databasen, da vi undgår at hente “kolde” data unødvendigt.

---

### Modelstruktur

```cs
// User.cs
public class User : Common
{
    public required string Email { get; set; }
    public required string Username { get; set; }

    public UserInfo? Info { get; set; } // 1:1 navigation
}
```

```cs
// UserInfo.cs
public class UserInfo
{
    public string UserId { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? AvatarUrl { get; set; }

    public User User { get; set; } = default!;
}
```

---

### ⚙️ Konfiguration i `OnModelCreating`

```cs
modelBuilder.Entity()
    .HasKey(i => i.UserId); // Shared PK

modelBuilder.Entity()
    .HasOne(u => u.Info)
    .WithOne(i => i.User)
    .HasForeignKey(i => i.UserId);
```

---

- **Login**: Vi slår kun op i `Users`tabellen (hurtigt, lille SELECT).
- **Vis brugerprofil**: Vi laver et join til `UserInfo` for at hente ekstra data.
- **Liste over alle brugere**: Vi henter kun felterne fra `Users`, så vi kan vise fx navn og email uden at hente adresser, telefonnumre og billeder.

---

### Fordele ved denne 1:1-normalisering

Fordel

Forklaring

⚡ Hurtigere queries

“Hot” data er samlet i en lille tabel med få kolonner

📦 Mindre payload

Frontend får kun det nødvendige – hurtigere svar og mindre JSON

🛠 Let at vedligeholde

Profiloplysninger kan ændres uden at røre ved login-tabellen

🔒 Sikkerhed

Loginoplysninger og personfølsomme oplysninger ligger i adskilte tabeller

---

### Eksempel på query

**Hurtig login-query:**

```cs
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == email);
```

Henter kun data fra `Users`.

**Detaljeret profil-query:**

```cs
var userDetails = await _context.Users
    .Include(u => u.Info)
    .FirstOrDefaultAsync(u => u.Id == id);
```

Henter både `Users` og `UserInfo`.

---

### 🏨 1:n – **Hotel og Rooms**

Et hotel har mange værelser, men et værelse tilhører kun ét hotel.

### Modelstruktur

```cs
// Hotel.cs
public class Hotel : Common
{
    public required string Name { get; set; }
    public string Address { get; set; } = "";

    public List Rooms { get; set; } = new(); // 1:n
}
```

```cs
// Room.cs
public class Room : Common
{
    public required string Number { get; set; }
    public int Capacity { get; set; }

    public string HotelId { get; set; } = string.Empty;
    public Hotel? Hotel { get; set; }
}
```

### ⚙️ Konfiguration i `OnModelCreating`

```cs
modelBuilder.Entity()
    .HasMany(h => h.Rooms)
    .WithOne(r => r.Hotel)
    .HasForeignKey(r => r.HotelId);
```

### 💡 Scenarie

- Hotel A har værelse 101, 102, 103
- Hotel B har værelse 201 og 202
  
  Vi kan nu spørge: *“Hvilke værelser tilhører dette hotel?”* eller *“Hvilket hotel tilhører dette værelse?”*
  

  

## Én bruger med flere roller - (1:n)

I stedet for at give hver bruger **én rolle (1:1)**, kan vi vælge at give dem **flere roller**. Det kaldes et **1:n multirolle-system**, hvor:

- Én **bruger** kan have flere **roller**
- Hver **rolle** tilhører kun én **bruger**

Det er en mellemvej mellem 1:1 og n:m – og fungerer godt i små systemer, hvor det ikke giver mening at dele roller på tværs af brugere.

---

### Datamodel i EF Core

```cs
// User.cs
public class User : Common
{
    public required string Email { get; set; }

    // 1:n: Én bruger kan have flere roller
    public List Roles { get; set; } = new();
}

```

```cs
// Role.cs
public class Role : Common
{
    public required string Name { get; set; } // "Admin", "Receptionist", "Rengøring"

    // Foreign key til brugeren
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
}
```

---

### Hvad sker der i databasen?

- En bruger kan have flere roller som rækker i `Roles` tabellen
- Hver rolle har en `UserId` kolonne, som fortæller hvilken bruger den tilhører

Dette er **ikke det samme som et globalt rollesystem**, men det giver mening, hvis du tænker på roller som *funktioner/ansvarsområder* knyttet til den enkelte bruger.

---

### ⚙️ EF Core konfiguration (valgfri)

```cs
modelBuilder.Entity()
    .HasMany(u => u.Roles)
    .WithOne(r => r.User)
    .HasForeignKey(r => r.UserId);
```

---

### Fordele ved 1:n multirolle

Fordel

Forklaring

✅ Simpelt

Nemmere end n:m – ingen join-tabel nødvendig

✅ Håndterer flere roller pr. bruger

Brugeren kan være både receptionist og rengøring

✅ God introduktion til multirolle-tankegangen

Gør det let at bygge videre senere

✅ JWT-venlig

Du kan stadig sende flere roller som claims i JWT-tokenet

---

### Claims og autorisation

Ved login kan du stadig bygge JWT-tokenet med alle brugerens roller:

```cs
var claims = new List();
foreach (var role in user.Roles)
{
    claims.Add(new Claim(ClaimTypes.Role, role.Name));
}
```

Du kan stadig bruge `[Authorize(Roles = "Admin,Receptionist")]` – og det virker, så længe bare én af rollerne matcher.

---

### Brug på H2

Denne model anbefales til projekter hvor:

- Brugere kan have **flere opgaver i systemet**
- I **ikke ønsker at lave n:m join-tabeller endnu**
- I vil **prøve kræfter med flere roller og ****`Claims`**** i JWT**
- Det matcher **gruppe**-systemet I møder senere, når I skal bruge et **Active Directory (AD)**

---

### 📆 n:m – **Bookings mellem Users og Rooms**

En bruger kan booke mange værelser, og et værelse kan være booket af mange brugere (på forskellige datoer).

Derfor bruger vi en **mange-til-mange** relation, hvor **Booking** er en separat entitet.

### Modelstruktur

```cs
// Booking.cs
public class Booking : Common
{
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public string RoomId { get; set; } = string.Empty;
    public Room? Room { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
```

```cs
// User.cs (uddrag)
public List Bookings { get; set; } = new();
```

```cs
// Room.cs (uddrag)
public List Bookings { get; set; } = new();
```

### ⚙️ Konfiguration (valgfri, men anbefalet)

```cs
modelBuilder.Entity()
    .HasOne(b => b.User)
    .WithMany(u => u.Bookings)
    .HasForeignKey(b => b.UserId);

modelBuilder.Entity()
    .HasOne(b => b.Room)
    .WithMany(r => r.Bookings)
    .HasForeignKey(b => b.RoomId);
```

### 🧠 Fordele

- Vi kan søge på: “Hvilke værelser har denne bruger booket?”
- Vi kan begrænse adgang: kun brugeren der bookede må afbestille
- Vi kan udvide med datoer, priser, status m.m.

---

### 📊 Oversigt over relationerne i databasen

Entitet

Relation

Type

User → Role

1:1

Én bruger har én rolle

Hotel → Room

1:n

Et hotel har mange værelser

User ↔ Room via Booking

n:m

En bruger kan booke mange værelser, og et værelse kan bookes af mange brugere

---

### ✅ Klar til migration?

Når modellerne er oprettet og `DbContext` er sat op korrekt:

```bash
add-migration AddHotelSystemRelations
update-database
```

Du kan visualisere relationerne med **EF Core Power Tools** for at få en grafisk oversigt over dine tabeller og relationer.

## DTO - Data Transer Objekt

Vores modeller som mapper til databasen gemmer ofte værdier, som ikke nødvendigvis skal vises på vores frontend. Der er flere fordele ved at lave DTO’er, såsom at vi minimere den mængde data vi skal sende og dermed laver vores API hurtigere og mere efficient. Det giver os også mulighed for at sammenkoble data, så vi kan få et komplet objekt fra flere database tabeller ud i et kald!

## 8. Teori for dagen — kontekst: Tirsdag 🟢 U2 d. \| Roller og Hashing

Teori for dagen

## JWT - JSON Web Token :jwt:

### **👮 Autentifikation vs Autorisation**

- **Autentifikation / Authentication  **: Hvem er du? (F.eks. login med brugernavn og kodeord)
- **Autorisation / Authorization **: Hvad må du gøre? (F.eks. må du kun læse data, ikke opdatere)

## Hvad er JWT :jwt:

Her dækker vi kun introduktionen til JWT - For en udvidet og mere dybdegående sektion  

JSON Web Tokens er en moderne og sikker måde at logge en bruger ind på en hjemmeside og API. Vi kan styre rettigheder og roller direkte i vores token med en høj grad af sikkerhed!

En JWT består af 3 centrale komponenter:

1. **Header**
   
   Indeholder metadata om selve token’et – typisk hvilken algoritme der er brugt til signeringen (f.eks. HS256), og at det er et JWT-token.
   

2. **Payload**
   
   Dette er selve dataen – det vi kalder "claims". Her kan vi gemme informationer som brugernavn, roller, rettigheder og mere. Det er vigtigt at huske, at payload ikke er krypteret – kun base64-kodet – så den må ikke indeholde hemmelige oplysninger.
   

3. **Signature**
   
   Signaturen bruges til at verificere, at token’et ikke er blevet ændret. Den er baseret på header og payload, samt en hemmelig nøgle kun serveren kender. Hvis nogen forsøger at manipulere token’et, vil signaturen ikke længere passe.
   

Disse tre dele bliver sat sammen med punktummer – og hele vores token kan så sendes med som f.eks. en Bearer Token i en HTTP-header.

## Hvordan Implementere vi JWT på vores API

For at bruge **JWT** i vores API, skal vi have to ting på plads:

1. **API’et skal kunne godkende og bruge JWT** som adgangskontrol til beskyttede endpoints.
2. **API’et skal kunne udstede JWT-tokens**, typisk gennem en *UserController*, når en bruger logger ind.

I praksis betyder det, at vi først opsætter JWT-validering i `Program.cs`, og derefter opretter en dedikeret service, der håndterer alt omkring oprettelse, validering og decoding af JWT.

Nedenfor viser vi:

- Hvordan vi konfigurerer JWT i `Program.cs`.
- Hvordan vi opretter en **JWT Service**, der står for generering og håndtering af tokens.

Vi skal starte med at hente nogle pakker som gør at vi kan bruge JWT - både som Authenticaiton som en Bearer-token og at vi kan lave tokens!

```cs

```

Bagefter kan vi lave en dedikeret service, som vi kan bruge på tværs af vores applikation! 

```cs
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DomainModels;

namespace API.Services
{
    /// 
    /// Service til håndtering af JWT tokens - generering, validering og decoding
    /// 
    public class JwtService
    {
        private readonly IConfiguration _configuration;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expiryMinutes;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
	          _secretKey = _configuration["Jwt:SecretKey"] 
	          ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

            _issuer = _configuration["Jwt:Issuer"] 
            ?? Environment.GetEnvironmentVariable("JWT_ISSUER");
            
            _audience = _configuration["Jwt:Audience"] 
            ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE");
            
            _expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] 
            ?? Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES");
        }

        /// 
        /// Genererer en JWT token for en bruger
        /// 
        /// Brugeren der skal have en token
        /// JWT token som string
        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);

            var claims = new List
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("userId", user.Id),
                new Claim("username", user.Username),
                new Claim("email", user.Email)
            };

            // Tilføj rolle claim hvis brugeren har en rolle
            if (user.Role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role.Name));
                claims.Add(new Claim("role", user.Role.Name));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_expiryMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }     
    }
}
```

Ekstra JWT metoder

```cs
				/// 
        /// Validerer en JWT token
        /// 
        /// Token der skal valideres
        /// ClaimsPrincipal hvis token er gyldig, ellers null
        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, 
                validationParameters, out SecurityToken validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        /// 
        /// Udtrækker bruger ID fra en JWT token
        /// 
        /// JWT token
        /// Bruger ID som string, eller null hvis ikke fundet
        public string? GetUserIdFromToken(string token)
        {
            var principal = ValidateToken(token);
            return principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// 
        /// Udtrækker claims fra en JWT token
        /// 
        /// JWT token
        /// Dictionary med claims, eller null
        public Dictionary? GetClaimsFromToken(string token)
        {
            var principal = ValidateToken(token);
            if (principal == null) return null;

            return principal.Claims.ToDictionary(c => c.Type, c => c.Value);
        }

        /// 
        /// Tjekker om en token er udløbet
        /// 
        /// JWT token
        /// True hvis token er udløbet, ellers false
        public bool IsTokenExpired(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                
                return jwtToken.ValidTo < DateTime.UtcNow;
            }
            catch
            {
                return true;
            }
        }
```

## 9. Teori for dagen — kontekst: Tirsdag 🟢 U2 d. \| Roller og Hashing

Teori for dagen

## Dokumentation af jeres API med XML-kommentarer

Når I bygger en API, er det vigtigt, at både den som har lavet kontrolleren og andre udviklere hurtigt kan forstå, hvad de forskellige endpoints gør. Derfor skal I **dokumentere jeres controllere og endpoints**, og det gør vi i .NET med **XML-kommentarer**.

### Hvad er XML-kommentarer?

XML-kommentarer er specialformat, som .NET og værktøjer som Swagger kan læse og vise som **automatisk API-dokumentation**.

De skrives som `///` lige over metoder, klasser og parametre. Eksempel:

```cs
/// 
/// Henter alle brugere.
/// 
/// En liste af brugere.
[HttpGet]
public async Task>> GetUsers()
{
    return await _context.Users.ToListAsync();
}
```

### De vigtigste tags

Her er de mest brugte tags du skal kende:

Tag

Funktion

``

Kort beskrivelse af hvad metoden gør

``

Beskriver en indparameter

``

Forklarer hvad metoden returnerer

``

(Til Web API) Hvilke HTTP-statuskoder der kan returneres og hvorfor

### Best Practice - Hvad I burde gøre, jo mindre I har en god grund til at gøre noget andet!

1. **Start altid med ****``** – Tænk: "Hvad gør denne metode?"
2. **Forklar alle parametre** – Det gør det tydeligt, hvad der forventes.
3. **Vis mulige fejl** – Brug `` til at vise f.eks. `404` eller `400`.
4. **Vær kortfattet men præcis** – Forestil dig, at en anden skal bruge din API uden at kende koden.

---

### Eksempel: Dokumenteret endpoint

```cs
/// 
/// Henter en specifik bruger ud fra ID.
/// 
/// Brugerens unikke ID.
/// Brugerens detaljer.
/// Bruger ikke fundet.
[HttpGet("{id}")]
public async Task> GetUser(string id)
{
    ...
}
```

---

### Bonus: Sådan aktiverer du dokumentation i Swagger

1. Åbn din `csproj` og tilføj:

```xml

  true

```

1. Tilføj dette i `Program.cs` hvis du bruger Swagger:

```cs
builder.Services.AddSwaggerGen(c =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});
```

Nu vises dine kommentarer som lækker dokumentation i Swagger UI!

---

### Øvelse til jer

Gå igennem din controller og tilføj XML-kommentarer på alle endpoints. Brug ``, `` og ``, og tilføj mindst én `` pr. endpoint.

Husk: En veldokumenteret API er nemmere at teste, bruge og fejlfinde – både for dig og andre!

For en konkret implementering, kan man se følgende commit med XML til 2 controllers

XML er også generelt acceptere på mange API-værktøjer, såsom Postman, Swagger og Bruno!

## 10. Teori for dagen — kontekst: Tirsdag 🟢 U2 d. \| Roller og Hashing

Teori for dagen

## Fejlhåndtering

Når en klient kommunikerer med en server (API), kan der opstå fejl undervejs — og der er mange ting, der kan gå galt.

I denne sektion kigger vi på:

1. **Almindelige fejlscenarier** i API-kommunikation.
2. **Hvordan vi håndterer fejl** på en struktureret måde.
3. **Best practices** for at gøre fejlhåndtering mere robust og let at vedligeholde.

Udover vores pensum har Microsoft og deres officielle dokumentation her: [Handle errors in ASP.NET Core controller-based web APIs](https://learn.microsoft.com/en-us/aspnet/core/web-api/handle-errors)

### **Almindelige fejlscenarier** i API-kommunikation.

### Netværksfejl

- **Timeout**: Når serveren ikke svarer inden for den forventede tid
- **Forbindelsesfejl**: Når klienten ikke kan oprette forbindelse til serveren
- **DNS fejl**: Når serverens adresse ikke kan opløses

### HTTP Status Fejl

- **400 Bad Request**: Ugyldig forespørgsel fra klienten
- **401 Unauthorized**: Manglende eller ugyldig autentificering
- **403 Forbidden**: Utilstrækkelige rettigheder
- **404 Not Found**: Ressource ikke fundet
- **409 Conflict**: Konflikt med ressourcens nuværende tilstand
- **500 Internal Server Error**: Intern serverfejl

### Database Fejl

- **DbUpdateConcurrencyException**: Når flere brugere ændrer samme data samtidigt
- **DbUpdateException**: Generelle database opdateringsfejl
- **Connection failures**: Når forbindelsen til databasen mistes

### Validering Fejl

- **Data Annotations**: Automatisk validering baseret på attributter
- **Forretningslogik fejl**: Specifikke valideringsregler (f.eks. email allerede eksisterer)

## Struktureret Fejlhåndtering

### Logging Implementation

Vi bruger ILogger til systematisk logning i alle controller metoder:

```cs
private readonly ILogger _logger;

public UsersController(AppDBContext context, JwtService jwtService, ILogger logger)
{
    _context = context;
    _jwtService = jwtService;
    _logger = logger;
}

```

### Log Niveauer

- **LogInformation**: For normale operationer og succesfulde handlinger
- **LogWarning**: For situationer der kræver opmærksomhed men ikke er fejl
- **LogError**: For exceptions og kritiske fejl

### Eksempel på Logging

```cs
public async Task>> GetUsers()
{
    try
    {
        _logger.LogInformation("Henter alle brugere - anmodet af administrator");

        var users = await _context.Users
            .Include(u => u.Role)
            .ToListAsync();

        _logger.LogInformation("Hentet {UserCount} brugere succesfuldt", users.Count);
        return Ok(users);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fejl ved hentning af alle brugere");
        return StatusCode(500, "Der opstod en intern serverfejl ved hentning af brugere");
    }
}
```

### Try-Catch Mønster

Alle controller metoder følger samme mønster:

```cs
public async Task> MethodName(parameters)
{
    try
    {
        // 1. Log start af operation
        _logger.LogInformation("Operation startet med parametre: {Parameters}", parameters);

        // 2. Validering (hvis nødvendigt)
        if (validationFails)
        {
            _logger.LogWarning("Validering fejlede: {ValidationError}", error);
            return BadRequest("Fejlbesked");
        }

        // 3. Forretningslogik
        var result = await DoBusinessLogic();

        // 4. Log succes
        _logger.LogInformation("Operation gennemført succesfuldt");
        return Ok(result);
    }
    catch (SpecificException ex)
    {
        // Håndter specifikke exceptions
        _logger.LogWarning(ex, "Specifik fejl opstod: {Details}", details);
        return StatusCode(409, "Specifik fejlbesked");
    }
    catch (Exception ex)
    {
        // Håndter generelle exceptions
        _logger.LogError(ex, "Uventet fejl opstod i {MethodName}", nameof(MethodName));
        return StatusCode(500, "Der opstod en intern serverfejl");
    }
}
```

### Specielle Exception Typer

### DbUpdateConcurrencyException

Håndteres særskilt for at give brugeren en klar besked om konflikter:

```cs
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogWarning(ex, 
    "Concurrency konflikt ved opdatering af bruger: {UserId}", id);

    if (!UserExists(id))
    {
        return NotFound();
    }
    else
    {
        throw; // Re-throw for global handling
    }
}
```

## Data Annotations Validering

Vi bruger Data Annotations til automatisk validering af DTO'er:

```cs
public class RegisterDto
{
    [EmailAddress(ErrorMessage = "Ugyldig email adresse")]
    [Required(ErrorMessage = "Email er påkrævet")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Brugernavn er påkrævet")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Adgangskode er påkrævet")]
    [MinLength(8, ErrorMessage = "Adgangskoden skal være mindst 8 tegn lang")]
    [RegularExpression
    (@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[@$!%*?&])[A-Za-z\\d@$!%*?&]{8,}$",
        ErrorMessage = @"Adgangskoden skal indeholde mindst ét tal, 
        ét stort bogstav, ét lille bogstav og et specialtegn")]
}

```

Validering håndteres automatisk af ASP.NET Core ModelState:

```cs
[HttpPost("register")]
public async Task Register([FromBody] RegisterDto dto)
{
    try
    {
        // ModelState.IsValid tjekkes automatisk af [ApiController] attributten

        _logger.LogInformation("Registrerer ny bruger med email: {Email}", 
        dto.Email);

        // Forretningslogik...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fejl ved registrering af bruger: {Email}", dto?.Email);
        return StatusCode(500, "Der opstod en intern serverfejl ved oprettelse af bruger");
    }
}

```

## Best Practices

### Logging Best Practices

   ```cs
   // Godt
   _logger.LogInformation("Login forsøg for email: {Email}", dto.Email);

   _logger.LogInformation("Login forsøg: {LoginData}", dto);

   ```
2. **Kontekst Information**: Inkluder relevante ID'er og parametre
   ```cs
   _logger.LogError(ex, "Fejl ved opdatering af bruger: {UserId}", id);

   ```

### Exception Handling Best Practices

1. **Specifik til Generel**: Håndter specifikke exceptions før generelle
2. **Re-throw Når Nødvendigt**: Brug `throw;` til at bevare stack trace
3. **Meningsfulde Fejlbeskeder**: Giv brugeren klare, handlingsrettede beskeder
4. **Log Før Return**: Altid log fejlen før du returnerer en fejlrespons

### Validering Best Practices

1. **Data Annotations Først**: Brug built-in validering hvor muligt
2. **Forretningslogik Validering**: Håndter kompleks validering i controlleren
3. **Tidlig Validering**: Valider input så tidligt som muligt
4. **Klare Fejlbeskeder**: Giv specifikke fejlbeskeder til brugeren

### HTTP Status Koder

Brug korrekte HTTP status koder:

- **200 OK**: Succesfuld operation med data
- **204 No Content**: Succesfuld operation uden data (f.eks. DELETE)
- **400 Bad Request**: Valideringsfejl eller ugyldig forespørgsel
- **401 Unauthorized**: Autentificeringsfejl
- **404 Not Found**: Ressource ikke fundet
- **409 Conflict**: Concurrency eller forretningslogik konflikt
- **500 Internal Server Error**: Uventet serverfejl

## 5. Monitoring og Observability

### Log Queries

Brug struktureret logging til at kunne søge i logs:

```bash
# Find alle fejl for en specifik bruger
logs | where UserId == "12345" and Level == "Error"

# Find alle concurrency konflikter
logs | where Message contains "Concurrency konflikt"

# Find performance problemer
logs | where Duration > 5000
```

### Metrics

Overvej at tilføje metrics for:

- Response times per endpoint
- Error rates per endpoint
- Database query performance
- Authentication failures

## Rate Limiting og Sikkerhed

### Login Rate Limiting

For at beskytte mod brute force angreb har vi implementeret et avanceret rate limiting system for login forsøg:

### Funktionalitet

- **Progressive delays**: Hver mislykket forsøg resulterer i længere ventetid
- **Account lockout**: Efter 5 mislykkede forsøg låses kontoen i 15 minutter
- **Memory-baseret tracking**: Bruger in-memory cache til at tracke forsøg
- **Automatisk cleanup**: Succesfuldt login rydder fejl-cache

### Konfiguration

```cs
private const int MaxAttempts = 5; // Maksimalt antal forsøg før lockout
private const int LockoutMinutes = 15; // Lockout periode i minutter
private const int DelayIncrementSeconds = 2; // Sekunder at tilføje per forsøg
```

### Implementering i Login Endpoint

```cs
[HttpPost("login")]
public async Task Login(LoginDto dto)
{
    try
    {
        // 1. Tjek om email er låst
        if (_loginAttemptService.IsLockedOut(dto.Email))
        {
            var remainingSeconds = _loginAttemptService.GetRemainingLockoutSeconds(dto.Email);
            return StatusCode(429, new {
                message = "Konto midlertidigt låst på grund af for mange mislykkede login forsøg.",
                remainingLockoutSeconds = remainingSeconds
            });
        }

        // 2. Valider credentials
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        {
            // 3. Registrer mislykket forsøg og påfør delay
            var delaySeconds = _loginAttemptService.RecordFailedAttempt(dto.Email);

            if (delaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                return Unauthorized(new {
                    message = "Forkert email eller adgangskode",
                    delayApplied = delaySeconds
                });
            }
        }

        // 4. Succesfuldt login - ryd fejl cache
        _loginAttemptService.RecordSuccessfulLogin(dto.Email);

        // ... fortsæt med normal login logik
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Fejl ved login for email: {Email}", dto?.Email);
        return StatusCode(500, "Der opstod en intern serverfejl ved login");
    }
}
```

### HTTP Status Koder for Rate Limiting

- **429 Too Many Requests**: Når konto er låst eller for mange forsøg
- **401 Unauthorized**: Ved forkerte credentials (med delay information)

### Login Status Endpoint

Administratorer kan tjekke login status for en email:

```cs
[Authorize(Roles = "Admin")]
[HttpGet("login-status/{email}")]
public IActionResult GetLoginStatus(string email)
{
    var attemptInfo = _loginAttemptService.GetLoginAttemptInfo(email);
    var isLockedOut = _loginAttemptService.IsLockedOut(email);

    return Ok(new {
        email = email,
        isLockedOut = isLockedOut,
        failedAttempts = attemptInfo?.FailedAttempts ?? 0,
        lastAttempt = attemptInfo?.LastAttempt,
        lockoutUntil = attemptInfo?.LockoutUntil,
        remainingLockoutSeconds = _loginAttemptService.GetRemainingLockoutSeconds(email)
    });
}
```

### LoginAttemptService

Servicen håndterer alle aspekter af login attempt tracking:

```cs
public class LoginAttemptService
{
    // Tjek om email er låst
    public bool IsLockedOut(string email)

    // Registrer mislykket forsøg og returner delay
    public int RecordFailedAttempt(string email)

    // Ryd cache ved succesfuldt login
    public void RecordSuccessfulLogin(string email)

    // Hent resterende lockout tid
    public int GetRemainingLockoutSeconds(string email)

    // Hent detaljeret attempt info
    public LoginAttemptInfo? GetLoginAttemptInfo(string email)
}
```

### Sikkerhedsfordele

1. **Brute Force Protection**: Forhindrer automatiserede angreb
2. **Progressive Delays**: Gør angreb ineffektive og tidskrævende
3. **Temporary Lockouts**: Beskytter konti mod vedvarende angreb
4. **Logging**: Detaljeret sporing af alle sikkerhedshændelser
5. **Admin Oversight**: Administratorer kan overvåge mistænkelig aktivitet

### Monitoring og Alerts

Overvej at implementere alerts for:

- Mange mislykkede forsøg fra samme IP
- Gentagne lockouts for samme email
- Unormalt høj fejlrate på login endpoint
- Mistænkelige mønstre i login forsøg

## CORS - Cross Origin Resource Sharing

CORS (Cross Origin Resource Sharing) er en vigtig sikkerhedsmekanisme i webudvikling. Lad os se på hvorfor det er vigtigt for jeres hotelbooking projekt:

Hvis I implementerer en løs CORS-politik i jeres hotel-API, kan det medføre flere sikkerhedsrisici:

- Uautoriseret adgang: Ondsindede websites kunne potentielt få adgang til jeres kunders personlige data og bookinger.
- Misbrug af brugerdata: Andre websites kunne potentielt udføre handlinger på vegne af jeres brugere, såsom at:
- Lave uautoriserede bookinger
- Få adgang til personlige oplysninger
- Ændre eller slette eksisterende reservationer

Dette er særligt kritisk for et hotelbookingsystem, hvor I håndterer:

- Brugerlogin og authentication
- Personlige brugerdata
- Bookingoplysninger

For at beskytte jeres system bør I implementere en strikt CORS-politik, der kun tillader adgang fra jeres godkendte frontend-domæner. Dette er særligt vigtigt når I arbejder med funktioner som brugerlogin og bookinghandlinger.

### Hvorfor har vi CORS?

**The cross-domain vulnerability**

> Let’s say you browse to a malicious website *https://evilunicorns.com* while logged into *https://examplebank.com*. Without same-origin policy, that hacker website could make authenticated malicious AJAX calls to *https://examplebank.com/api* to `POST /withdraw` even though the hacker website doesn’t have direct access to the bank’s cookies. [Source](https://www.moesif.com/blog/technical/cors/Authoritative-Guide-to-CORS-Cross-Origin-Resource-Sharing-for-REST-APIs/)

Her ser vi at uden CORS, ville en side kunne få adgang til blandt andet cookies som indeholder vores personlige data og i nogle eksempler få adgang til agere på vores vegne. 

Her er det blandt andet vigtigt at [*examplebank.com*](http://examplebank.com)* *har CORS sikkerhed, så den ikke tillader at lave kommandoer fra vores computer og på vores vegne. 

Videoen her forklare CORS i simple termer med gode eksempler

Her ser vi på vores postnumre-api, at de har en \* i deres control-allow-origin header - det betyder at den er åben for alle hosts. Standarten er at den kun ville acceptere kald fra adressen “data.forsningen.dk”

**Cross Site Scripting - XSS**

*Cross Site Scripting, også kaldet XSS, er ulovligt, så man skal **ikke** prøve det på sider andre end ens egne.*

Cross Site Scripting er hvor man skriver ondsindet kode, i form af JavaScript, på en offentlig hjemmeside som har et ikke-valideret inputfelt eller tekstboks. Dette kan få browseren til at køre koden som man har skrevet, endda på andres computer. 

Et eksempel på en XSS som spredte sig hurtigt var [Samy](). 

Det var en kodeblock som blev skrevet ind på MySpace som ændrede brugerens ens profils beskrivelse og sendte en venneanmodning til en profil som hed Samy. Relativt harmløs virus/XSS, men den nåede at sprede sig hurtig. Koden kan findes her: [https://samy.pl/myspace/tech.html](https://samy.pl/myspace/tech.html)

Der er lavet en short-doc omkring det, som man kan se her: 

Siden er ofte nede, men her er videon på yt - [https://www.youtube.com/watch?v=9yOjLV5wlIo](https://www.youtube.com/watch?v=9yOjLV5wlIo)

En rigtig fin forklaring på problemet og hvordan det virker teknisk er her og pensum.

## 11. Teori for dagen

Teori for dagen

Vi snakker omkring Bogus (Faker) i C# til at lave data ud fra vores modeller og så kigger vi vores flow igennem for S i , altså vi arbejder med single responsibility!

# 🎭 Bogus (Faker) Guide - Generer Realistisk Test Data

## Hvad er Bogus?

**Bogus** er et C# bibliotek til at generere **fake/mock data** til testformål. Det er inspireret af det populære JavaScript bibliotek **Faker.js** og giver os mulighed for at oprette realistiske testdata på en hurtig og konsistent måde. Vi kan bruge det til at gøre følgende

- **Realistiske data**: Navne, adresser, telefonnumre, emails, osv.
- **Fleksible regler**: Definer præcist hvordan data skal genereres
- **Relationer**: Håndter komplekse data-relationer
- **Performant**: Genererer tusindvis af records hurtigt

1. **Realistisk Test Data**
   ```cs
   // I stedet for:
   var user = new User { Name = "Test User", Email = "test@test.com" };

   // Får vi:
   var user = new User { Name = "Mads Hansen", Email = "mads.hansen@gmail.com" };

   ```
2. **Skalerbarhed**
   - Generer 1 eller 10.000 records med samme kode
   - Perfekt til performance testing
3. **Konsistens**
   - Samme data struktur hver gang
   - Reproducerbare tests med seeds
4. **Tidsbesparelse**
   - Ingen manuel oprettelse af test data
   - Automatisk population af development databaser
5. **Bedre Testing**
   - Test med varieret, realistisk data
   - Opdager edge cases lettere

## 📦 Installation

### NuGet Package Manager:

```bash
dotnet add package Bogus
```

## Grundlæggende Koncepter

### 1. **Faker Objekt**

```cs
var faker = new Faker("en");
var name = faker.Person.FullName; // "Lars Andersen"
var email = faker.Internet.Email(); // "lars.andersen@gmail.com"

```

### 2. **Typed Faker**

```cs
var userFaker = new Faker("en")
    .RuleFor(u => u.Name, f => f.Person.FullName)
    .RuleFor(u => u.Email, f => f.Internet.Email())
    .RuleFor(u => u.Age, f => f.Random.Int(18, 80));

var user = userFaker.Generate(); // Genererer én bruger
var users = userFaker.Generate(100); // Genererer 100 brugere

```

### 3. **Lokalisering**

```cs
var danishFaker = new Faker("da"); // Danske navne og adresser
var englishFaker = new Faker("en"); // Engelske navne og adresser

```

## Implementering i vores Hotel System

### 📁 **Struktur:**

```txt
API/
├── Services/
│   └── DataSeederService.cs    # Hovedservice til seeding
├── Controllers/
│   └── DataSeederController.cs # API endpoints til seeding
└── Program.cs                  # DI registrering

```

### 🔧 **DataSeederService.cs**

```cs
public class DataSeederService
{
    private readonly AppDBContext _context;
    private readonly ILogger _logger;

    // Seed brugere
    private async Task- > SeedUsersAsync(int count)
    {
        var faker = new Faker("en")
            .RuleFor(u => u.Id, f => Guid.NewGuid().ToString())
            .RuleFor(u => u.Email, f => f.Internet.Email().ToLower())
            .RuleFor(u => u.Username, (f, u) => u.Email.Split('@')[0])
            .RuleFor(u => u.RoleId, f => 
            f.PickRandom("user-role-id", "admin-role-id"))
            .RuleFor(u => u.LastLogin, f => f.Date.Recent(30))
            .RuleFor(u => u.CreatedAt, f => f.Date.Past(2))
            .RuleFor(u => u.UpdatedAt, (f, u) => 
            f.Date.Between(u.CreatedAt, DateTime.UtcNow));

        return faker.Generate(count);
    }

    // Seed hoteller
    private async Task- > SeedHotelsAsync(int count)
    {
        var hotelNames = new[] { "Hotel Royal", "Grand Hotel", "Scandic Hotel" };
        var cities = new[] { "København", "Aarhus", "Odense" };

        var faker = new Faker("en")
            .RuleFor(h => h.Id, f => Guid.NewGuid().ToString())
            .RuleFor(h => h.Name, f => f.PickRandom(hotelNames) + " " 
            + f.PickRandom(cities))
            .RuleFor(h => h.Address, f => f.Address.FullAddress())
            .RuleFor(h => h.CreatedAt, f => f.Date.Past(5))
            .RuleFor(h => h.UpdatedAt, (f, h) => 
            f.Date.Between(h.CreatedAt, DateTime.UtcNow));

        return faker.Generate(count);
    }
}
```

### 🎮 **Controller Endpoints:**

```cs
[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class DataSeederController : ControllerBase
{
    // Seed komplet database
    [HttpPost("seed")]
    public async Task SeedDatabase(
        [FromQuery] int userCount = 50,
        [FromQuery] int hotelCount = 10,
        [FromQuery] int roomsPerHotel = 20,
        [FromQuery] int bookingCount = 100)

    // Ryd database
    [HttpDelete("clear")]
    public async Task ClearDatabase()

    // Hent statistikker
    [HttpGet("stats")]
    public async Task GetDatabaseStats()
}
```

## 🔥 Avancerede Funktioner

### 1. **Betingede Regler**

```cs
var faker = new Faker("en")
    .RuleFor(u => u.Age, f => f.Random.Int(18, 80))
    .RuleFor(u => u.IsStudent, (f, u) => u.Age  u.Income, (f, u) => u.IsStudent ?
        f.Random.Decimal(0, 15000) :
        f.Random.Decimal(25000, 80000));
```

### 2. **Vægtede Valg**

```cs
var faker = new Faker("en")
    .RuleFor(r => r.Type, f => f.Random.WeightedRandom(
        new[] { "Standard", "Deluxe", "Suite" },
        new[] { 0.6f, 0.3f, 0.1f })); // 60% Standard, 30% Deluxe, 10% Suite
```

### 3. **Komplekse Relationer**

```cs
// Først opret hoteller
var hotels = hotelFaker.Generate(10);

// Derefter rum der refererer til hoteller
var roomFaker = new Faker("en")
    .RuleFor(r => r.HotelId, f => f.PickRandom(hotels).Id)
    .RuleFor(r => r.Number, f => f.Random.Int(101, 999).ToString());

var rooms = roomFaker.Generate(200);
```

### 4. **Undgå Dubletter**

```cs
var usedEmails = new HashSet();

var userFaker = new Faker("en")
    .RuleFor(u => u.Email, f => {
        string email;
        do {
            email = f.Internet.Email();
        } while (usedEmails.Contains(email));

        usedEmails.Add(email);
        return email;
    });
```

### 5. **Seeds for Reproducerbare Tests**

```cs
var faker = new Faker("en");
faker.Random = new Randomizer(12345); // Fast seed

// Genererer samme data hver gang
var user1 = faker.Person.FullName; // Altid samme navn
```

## 🎯 Best Practices

1. **Valider Constraints**
   ```cs
   .RuleFor(u => u.Email, f => f.Internet.Email().ToLower()) // Lowercase emails
   .RuleFor(u => u.Age, f => f.Random.Int(18, 120)) // Realistisk alder
   ```
2. **Håndter Relationer Korrekt**
   ```cs
   // Opret users (parents) først, bookings (children) bagefter
   var users = userFaker.Generate(100);
   var bookings = bookingFaker.Generate(500); // Referencer users
   ```
3. **Brug Meaningful Data**
   ```cs
   .RuleFor(h => h.StarRating, f => f.Random.Int(1, 5)) // Hotel stjerner
   .RuleFor(r => r.PricePerNight,
   f => f.Random.Decimal(200, 2000)) // Realistiske priser
   ```

### ❌ **Undgå:**

1. **For Mange Records i Tests**
   ```cs
   // Dårligt - langsom test
   var users = faker.Generate(10000);

   // Godt - hurtig test
   var users = faker.Generate(10);

   ```
2. **Hardcodede Værdier**
   ```cs
   // Dårligt
   .RuleFor(u => u.Name, f => "Test User")

   // Godt
   .RuleFor(u => u.Name, f => f.Person.FullName)

   ```

## Eksempler

### 🏨 **Hotel Booking System**

```cs
// 1. Opret brugere
var userFaker = new Faker("da")
    .RuleFor(u => u.Email, f => f.Internet.Email())
    .RuleFor(u => u.Name, f => f.Person.FullName);

var users = userFaker.Generate(50);

// 2. Opret hoteller
var hotelFaker = new Faker("da")
    .RuleFor(h => h.Name, f => "Hotel " + f.Address.City())
    .RuleFor(h => h.Address, f => f.Address.FullAddress())
    .RuleFor(h => h.StarRating, f => f.Random.Int(1, 5));

var hotels = hotelFaker.Generate(10);

// 3. Opret rum
var roomFaker = new Faker("da")
    .RuleFor(r => r.HotelId, f => f.PickRandom(hotels).Id)
    .RuleFor(r => r.Number, f => f.Random.Int(101, 999).ToString())
    .RuleFor(r => r.Type, f => f.PickRandom("Standard", "Deluxe", "Suite"))
    .RuleFor(r => r.PricePerNight, f => f.Random.Decimal(500, 3000))
    .RuleFor(r => r.Capacity, f => f.Random.Int(1, 4))
    .RuleFor(r => r.HasBalcony, f => f.Random.Bool(0.3f)) // 30% har balkon
    .RuleFor(r => r.HasSeaView, f => f.Random.Bool(0.2f)); // 20% har havudsigt

var rooms = roomFaker.Generate(200);

// 4. Opret bookinger (uden overlap)
var bookingFaker = new Faker("da");
var bookings = new List();

for (int i = 0; i 
        b.RoomId == room.Id &&
        b.StartDate  startDate);

    if (!hasOverlap)
    {
        bookings.Add(new Booking
        {
            UserId = user.Id,
            RoomId = room.Id,
            StartDate = startDate,
            EndDate = endDate,
            NumberOfGuests = faker.Random.Int(1, room.Capacity),
            TotalPrice = room.PricePerNight * nights,
            Status = faker.PickRandom("Confirmed", "Pending", "Cancelled")
        });
    }
}

```

### 🔧 **API Endpoints Usage**

```bash
# Seed komplet database
POST /api/dataseeder/seed?userCount=100&hotelCount=15&roomsPerHotel=25&bookingCount=200

# Seed kun brugere
POST /api/dataseeder/seed-users?count=50

# Seed kun hoteller og rum
POST /api/dataseeder/seed-hotels?hotelCount=5&roomsPerHotel=20

# Hent database statistikker
GET /api/dataseeder/stats

# Ryd hele databasen
DELETE /api/dataseeder/clear

```

## 🔐 **Sikkerhed og Miljøer**

⚠️ **VIGTIGT**: Brug kun Bogus i **development** og **test** miljøer!

```cs
// Sikkerhedstjek i controller
if (!_environment.IsDevelopment())
{
    return BadRequest("Database seeding er kun tilladt i development miljø");
}

```

## 🎯 **Konklusion**

Bogus er et godt **værktøj** til moderne .NET udvikling. Det gør det nemt at:

- ✅ Oprette realistisk test data
- ✅ Populere development databaser
- ✅ Teste med varieret data
- ✅ Spare tid på manuel data-oprettelse
- ✅ Forbedre demo-præsentationer

### 🚀 **Næste Skridt:**

1. Installer Bogus i dit projekt
2. Opret en DataSeederService
3. Definer Faker regler for dine modeller
4. Byg API endpoints til seeding
5. Brug i development og testing

**Happy Coding! 🎭✨**

Tjek din viden omkring Bogus med en quiz på [videnstjek.mags.dk/bogus-faker](https://videnstjek.mags.dk/quiz/bogus-faker)

---

Slides til følgende emne findes under - -

## 12. Teori for dagen

Teori for dagen

## Deployment på Windows 2022 Server i Datahouse

## Eager vs Lazy Loading i EF Core

Når vi arbejder med relationer i Entity Framework Core, har vi ofte brug for at hente data, der hænger sammen – fx:

- Et hotel og dets værelser
- Et værelse og dets bookinger
- En bruger og dennes bookinger

Her skal vi vælge mellem to måder at hente relaterede data på: **Eager Loading** og **Lazy Loading**.

---

### 🟡 Lazy Loading – “Vent til jeg beder om det”

EF Core **henter kun hovedobjektet først** – relaterede data hentes **senere**, når man prøver at tilgå dem.

**Fordel:**

- Hurtig initial hentning
- Bruges når man sjældent har brug for de relaterede data

**Ulempe:**

- Kan føre til **N+1-problemer** (mange små forespørgsler)
- Sværere at debugge og styre, især i API’er

```cs
// Forudsætter Lazy Loading proxies er slået til
var hotel = _context.Hotels.FirstOrDefault(h => h.Id == "abc");

// EF henter først værelser, når vi tilgår dem:
foreach (var room in hotel.Rooms)
{
    Console.WriteLine(room.Number);
}
```

**⚠️ Kræver ekstra konfiguration:**

```cs
builder.Services.AddDbContext(options =>
    options.UseLazyLoadingProxies().UseNpgsql(connectionString));
```

---

### 🟢 Eager Loading – “Hent det hele med det samme”

Her siger vi eksplicit: “hent det relaterede data nu!”

Vi bruger `.Include(...)` – fx:

```cs
var hotel = _context.Hotels
    .Include(h => h.Rooms)
    .FirstOrDefault(h => h.Id == "abc");
```

**Fordel:**

- **Alt hentes i én SQL-forespørgsel**
- Ingen overraskelser – du bestemmer hvad der hentes

**Ulempe:**

- Kan blive tungt, hvis du henter for meget unødvendigt data

---

### 🧠 Hvornår bruger vi hvad?

Situation

Anbefalet loading

Du bygger en API og returnerer komplekse objekter

**Eager Loading**

Du arbejder med simpel data i konsol/wpf

**Lazy Loading (valgfrit)**

Du har dybe relationer (booking → room → hotel)

Eager – evt. med `ThenInclude`

---

### 🔁 Eksempel: Booking med bruger og værelse

```cs
var booking = _context.Bookings
    .Include(b => b.User)
    .Include(b => b.Room)
    .ThenInclude(r => r.Hotel)
    .FirstOrDefault(b => b.Id == id);
```

Her hentes:

- Booking
- Bruger (User)
- Værelset (Room)
- Det hotel værelset tilhører (Hotel)

Alt i én forespørgsel!

---

### ✅ Brug i en API-controller

```cs
[HttpGet("{id}")]
public IActionResult GetBooking(string id)
{
    var booking = _context.Bookings
        .Include(b => b.Room)
        .ThenInclude(r => r.Hotel)
        .Include(b => b.User)
        .FirstOrDefault(b => b.Id == id);

    if (booking == null) return NotFound();

    return Ok(new
    {
        Id = booking.Id,
        Start = booking.StartDate,
        End = booking.EndDate,
        Room = booking.Room.Number,
        Hotel = booking.Room.Hotel.Name,
        User = booking.User.Email
    });
}
```

---

### 💡 Bonus: Projection for performance

Hvis du **kun skal bruge enkelte felter**, så brug `Select(...)`:

```cs
var booking = _context.Bookings
    .Include(b => b.Room)
    .ThenInclude(r => r.Hotel)
    .Include(b => b.User)
    .Where(b => b.Id == id)
    .Select(b => new
    {
        b.Id,
        b.StartDate,
        b.EndDate,
        Hotel = b.Room.Hotel.Name,
        Room = b.Room.Number,
        Email = b.User.Email
    })
    .FirstOrDefault();
```

## Filtering i Query Parameters

Når vores API vokser, vil vi sjældent have endpoints som:

```txt
GET /bookings
```

Der bare returnerer **alle bookinger i hele databasen**. Det er både ineffektivt og upraktisk.

I stedet ønsker vi, at klienten kan **filtrere** data – fx:

- Bookinger for en bestemt bruger
- Bookinger fra en bestemt dato og frem
- Bookinger på et specifikt værelse

Det gøres med **query parameters** i URL’en:

```txt
/bookings?userId=abc&from=2024-08-01
```

---

### 🧑‍💻 Eksempel i Controller

```cs
[HttpGet]
public IActionResult GetBookings([FromQuery] string? userId, [FromQuery] DateTime? from)
{
    var query = _context.Bookings
        .Include(b => b.Room)
        .Include(b => b.User)
        .AsQueryable();

    if (!string.IsNullOrEmpty(userId))
        query = query.Where(b => b.UserId == userId);

    if (from.HasValue)
        query = query.Where(b => b.StartDate >= from.Value);

    var result = query.Select(b => new
    {
        b.Id,
        b.StartDate,
        b.EndDate,
        Room = b.Room.Number,
        Email = b.User.Email
    }).ToList();

    return Ok(result);
}
```

---

### Hvad sker der?

- Vi starter med `.AsQueryable()` – det betyder, vi bygger queryen dynamisk
- For hver parameter der er sat, **tilføjer vi en ****`Where()`****filter**
- Til sidst kalder vi `.ToList()`, som **udfører SQL’en**

---

### Eksempler på kald

URL

Effekt

`/bookings`

Returnerer alle bookinger

`/bookings?userId=abc123`

Returnerer alle bookinger for bruger med ID `abc123`

`/bookings?from=2024-08-01`

Bookinger med startdato senere end 1. august

`/bookings?userId=abc123&from=2024-08-01`

Begge filtre aktivt

---

### Bonus: Brug af Dto til filtrering

Du kan gøre controlleren renere med en filtreringsklasse:

```cs
public class BookingFilter
{
    public string? UserId { get; set; }
    public DateTime? From { get; set; }
}
```

Og så ændres din controller til:

```cs
[HttpGet]
public IActionResult GetBookings([FromQuery] BookingFilter filter)
{
    var query = _context.Bookings
        .Include(b => b.Room)
        .Include(b => b.User)
        .AsQueryable();

    if (!string.IsNullOrEmpty(filter.UserId))
        query = query.Where(b => b.UserId == filter.UserId);

    if (filter.From.HasValue)
        query = query.Where(b => b.StartDate >= filter.From.Value);

    var result = query.Select(b => new
    {
        b.Id,
        b.StartDate,
        b.EndDate,
        Room = b.Room.Number,
        Email = b.User.Email
    }).ToList();

    return Ok(result);
}
```

---

### Fordele ved filtering via query parameters

- ✅ Skalerbart – kan nemt udvides med flere filtre
- ✅ REST-venligt og nemt at forstå for frontend
- ✅ Reducerer datamængde og forbedrer performance

## Sorting med Query Parameters

Vi har allerede lært at **filtrere data** i vores API – men brugere vil ofte også gerne kunne **sortere** data:

- Sortér værelser efter **pris**
- Sortér bookinger efter **startdato**
- Sortér brugere alfabetisk

Dette kan vi gøre med query parameters som:

```txt
GET /rooms?sortBy=price&desc=true
```

---

### 🧑‍💻 Eksempel i Controller

```cs
[HttpGet]
public IActionResult GetRooms([FromQuery] string? sortBy, [FromQuery] bool desc = false)
{
    var query = _context.Rooms.AsQueryable();

    // Bestem sorteringsfelt
    query = sortBy switch
    {
        "price" => desc ? query.OrderByDescending(r => r.Price) : query.OrderBy(r => r.Price),
        "capacity" => desc ? query.OrderByDescending(r => r.Capacity) : query.OrderBy(r => r.Capacity),
        _ => query.OrderBy(r => r.Number) // Default sortering
    };

    var result = query.Select(r => new
    {
        r.Id,
        r.Number,
        r.Capacity,
        r.Price
    }).ToList();

    return Ok(result);
}
```

---

### 🧠 Hvad sker der?

- Brugeren kan vælge **sorteringsfelt** (f.eks. `"price"` eller `"capacity"`)
- Brugeren kan også tilføje `desc=true` for at sortere **faldende**
- Vi bruger en `switch` til at **styre sorteringen**
- Der er en fallback (`default`) hvis parameteren mangler eller er forkert

---

### 🧪 Eksempler på kald

URL

Sortering

`/rooms`

Sortér efter værelsesnummer (default)

`/rooms?sortBy=price`

Sortér stigende efter pris

`/rooms?sortBy=price&desc=true`

Sortér faldende efter pris

`/rooms?sortBy=capacity`

Sortér efter kapacitet (antal senge)

---

### 🛠 Tip: Sortering + Filtering kombineret

Vi kan nemt **kombinere sorting med filtering**, så du fx kan vise:

```txt
/rooms?hotelId=abc&sortBy=price&desc=true
```

Controlleren skal da kombinere begge teknikker:

```cs
if (!string.IsNullOrEmpty(hotelId))
    query = query.Where(r => r.HotelId == hotelId);

// ... derefter sortering som vist ovenfor
```

---

### 📋 Fordele ved sorting i API’er

- ✅ Brugervenligt for frontend – styr rækkefølge i UI
- ✅ Fleksibelt og nemt at udvide med flere felter
- ✅ Gør det muligt at implementere "pris fra lav til høj", "seneste bookinger" osv. direkte på serveren, så vi ikke “over fetcher” eller skal lave kald for at håndtere dem i brugerens måske begrænset ram

## Route Naming og Struktur – RESTful Conventions

Når vi bygger API’er, handler det ikke kun om funktionalitet – det handler også om **struktur og læsbarhed**.

Et godt navngivet API gør det nemt for både frontend og udviklere at forstå, hvad der sker. Derfor bruger vi **RESTful routing**.

---

### 🔄 Hvad er RESTful routing?

REST står for **Representational State Transfer**. Det betyder, at vi organiserer vores API **efter ressourcer**, ikke efter handlinger.

Vi bruger **HTTP-metoder** til at angive handlingen:

HTTP-metode

Hvad den betyder

`GET`

Hent data

`POST`

Opret ny ressource

`PUT` / `PATCH`

Opdater en ressource

`DELETE`

Slet en ressource

---

### 🏨 Eksempel: Ressourcen “rooms”

Formål

Route

HTTP

Beskrivelse

Hent alle værelser

`/rooms`

`GET`

Returnerer alle værelser

Hent ét værelse

`/rooms/{id}`

`GET`

Returnerer værelse med bestemt ID

Opret værelse

`/rooms`

`POST`

Opretter nyt værelse

Opdater værelse

`/rooms/{id}`

`PUT`

Ændrer info om værelse

Slet værelse

`/rooms/{id}`

`DELETE`

Fjerner værelse fra databasen

---

### 👨‍👩‍👧 Eksempel: Bookinger for en bruger

Nogle gange har vi relationer – fx bookinger som tilhører en bruger:

```txt
GET /users/{userId}/bookings
```

Det giver mening, fordi:

- Bookinger er en **underressource** af bruger
- Det afspejler relationen i databasen (n:m mellem user og room)

---

### ✅ Best Practices

Gør dette ✅

Undgå dette ❌

`/users`

`/getUsers`, `/fetchAllUsers`

`/rooms/{id}`

`/getRoomById?id=123`

`/bookings?userId=abc`

`/getBookingsForUser`

Brug flertal (`/rooms`)

Brug ikke ental (`/room`)

Brug understregning i body (ikke i routes)

Brug ikke camelCase i routes

---

### 📦 Eksempel i Controller

```cs
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAllRooms() { }

    [HttpGet("{id}")]
    public IActionResult GetRoomById(string id) { }

    [HttpPost]
    public IActionResult CreateRoom(RoomDto dto) { }

    [HttpPut("{id}")]
    public IActionResult UpdateRoom(string id, RoomDto dto) { }

    [HttpDelete("{id}")]
    public IActionResult DeleteRoom(string id) { }
}
```

---

### 🧠 REST = Ressourcer + Metoder

**Du bør aldrig have en route som hedder ****`createBooking`**. Brug i stedet:

```txt
POST /bookings
```

Og lad **HTTP-metoden** fortælle, hvad du vil.

---

### 💡 Bonus: Special routes

I nogle tilfælde kan du bruge **custom routes**, men de skal stadig være beskrivende og læsbare:

Funktion

Route

Hent mine bookinger

`/bookings/mine`

Check ledige værelser

`/rooms/available?from=2024-08-01&to=2024-08-05`

Registrer en bruger

`/Users/register`

Login som bruger

`/Users/login`

Brug kun disse når du **ikke** nemt kan repræsentere funktionen som en klassisk ressourcerute.

---

### Øvelse til jer - Tjek op på jeres endpoints

Tjek op på jeres endpoints om de følger strukturen som er herunder!

- `GET /bookings` – hent alle bookinger
- `GET /bookings/{id}` – hent én booking
- `GET /users/{userId}/bookings` – hent bookinger for én bruger
- `POST /bookings` – opret ny booking
- `DELETE /bookings/{id}` – slet booking

## Caching af API-responses

Jo flere brugere og data dit API håndterer, desto vigtigere bliver **performance**.

Nogle endpoints returnerer **det samme resultat igen og igen**, og det er spild at hente det fra databasen hver gang.

Her kommer **caching** ind i billedet.

---

### 🧠 Hvad er caching?

Caching betyder at **gemme et svar midlertidigt**, så vi slipper for at hente det fra databasen hver gang.

- Det gør vores API **hurtigere**
- Det reducerer **belastningen på databasen**
- Det forbedrer **skalerbarhed**

---

## 🗃️ Typer af caching i .NET

Type

Beskrivelse

Pensum på H2?

`MemoryCache`

Lokal cache i serverens hukommelse

✅ Ja, nem og god til udvikling

Redis :redis: 

Delt cache over netværk (fx mellem flere servere)

🚫 Ikke på H2, men god til produktion. Endelig leg med det!

---

## Eksempel: Cache `/rooms` endpoint

Vi vil gerne cache listen over værelser, da den sjældent ændrer sig:

🔧 Opsætning i `Program.cs` 

```cs
builder.Services.AddMemoryCache();
```

---

### Eksempel i vores Controller

---

```cs
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public RoomsController(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet]
    public IActionResult GetRooms()
    {
        // Definér cache-nøglen
        string cacheKey = "all_rooms";

        // Forsøg at hente fra cache først
        if (_cache.TryGetValue(cacheKey, out List cachedRooms))
        {
            return Ok(cachedRooms);
        }

        // Hvis ikke fundet, hent fra DB og tilføj til cache
        var rooms = _context.Rooms.ToList();

        // Tilføj til cache i 30 sekunder
        _cache.Set(cacheKey, rooms, TimeSpan.FromSeconds(30));

        return Ok(rooms);
    }
}
```

---

### 🕓 Hvor længe skal man cache?

Det afhænger af dataens "friskhed":

Data

Anbefalet cachetid

Værelsesliste

30-60 sekunder

Brugeres bookinger

5-10 sekunder

Priser eller statistik

1-5 minutter

Realtime info

Skal ikke caches!

---

### Hvad hvis data ændrer sig?

Hvis du f.eks. opretter et nyt værelse, **skal du slette cachet** manuelt:

```cs
_cache.Remove("all_rooms");
```

Det kan du gøre efter en `POST /rooms`:

```cs
[HttpPost]
public IActionResult CreateRoom(RoomDto dto)
{
    var room = new Room { ... };
    _context.Rooms.Add(room);
    _context.SaveChanges();

    _cache.Remove("all_rooms"); // Fjern cache så den bliver genskabt

    return Ok(room);
}
```

---

## Hvad med Redis? :redis:

`MemoryCache` virker kun så længe **serveren kører**. Ved load balancing eller flere instanser bør man bruge Redis:

- Deler cache mellem alle servere
- Hurtig og skalerbar (kører som en separat proces)
- Bruger fx `IDistributedCache` i .NET

Men i H2 nøjes vi med `IMemoryCache` – det er hurtigt og simpelt.

---

## Fordele ved caching

- 🚀 Markant hurtigere svartider (Brug enten custom-kode eller Aspire til at se forskellen)
- 🧠 Mindre pres på databasen
- 💡 Let at implementere i .NET

---

## Øvelse til elever

Lav cache på følgende endpoints:

1. `GET /rooms`
2. `GET /bookings/mine` (caches i 5 sekunder)
3. `GET /hotels` (caches i 60 sekunder)

Husk at rydde cache ved `POST` eller `DELETE`!

## 13. Teori for dagen — kontekst: Uge 4 (35) - Bygge en SPA frontend på vores API ( - )

Teori for dagen 

# State Management i Blazor WebAssembly (WASM)

## Introduktion

Blazor WebAssembly (WASM) kører direkte i browseren og giver mulighed for at bygge rige, interaktive webapplikationer med C#. En vigtig del af enhver webapp er at holde styr på brugerens state – f.eks. om brugeren er logget ind, hvilke roller de har, og andre session-relaterede data.

## Måder at håndtere state på i Blazor WASM

Der findes flere måder at håndtere state på i Blazor WASM:

1. **In-memory state**
   - Data gemmes i services (f.eks. singleton services via Dependency Injection).
   - Fordel: Hurtigt og nemt.
   - Ulempe: State forsvinder, hvis brugeren opdaterer siden eller lukker browseren. Så det minder meget om sessionStorage
2. **localStorage/sessionStorage**
   - Data gemmes i browserens storage, så det overlever sideopdateringer og genstart af browseren (localStorage).
   - sessionStorage varer kun så længe fanen er åben.
   - Fordel: Simpelt, og data bevares mellem sessioner (localStorage).
   - Ulempe: Data er let tilgængeligt for brugeren og kan manipuleres.
3. **Server-side storage**
   - Data gemmes på serveren, og klienten får kun et token eller en session-id.
   - Fordel: Mere sikkert, da følsomme data ikke ligger i browseren.
   - Ulempe: Kræver server-side logik og ofte mere kompleksitet.

## Gemme state i localStorage

I Blazor WASM kan man bruge JavaScript interop til at gemme og hente data fra localStorage:

```cs
// Gem data
await JS.InvokeVoidAsync("localStorage.setItem", "key", "value");
// Hent data
var value = await JS.InvokeAsync("localStorage.getItem", "key");

```

Dette er hurtigt og nemt, men data ligger i klartekst i browseren.

## Valgfri AES-kryptering

For at beskytte følsomme data kan man vælge at kryptere det, før det gemmes i localStorage. Et eksempel er at bruge AES-kryptering via et JavaScript-bibliotek som CryptoJS:

```javascript
const encrypted = CryptoJS.AES.encrypt(value, key).toString();
localStorage.setItem(storageKey, encrypted);
// ...
const bytes = CryptoJS.AES.decrypt(encrypted, key);
const decrypted = bytes.toString(CryptoJS.enc.Utf8);

```

Dette gør det sværere for en angriber at læse data direkte, men **nøglen til krypteringen skal også ligge i browseren**, hvilket begrænser sikkerheden.

## Hvorfor er det svært at gøre ting sikre i en browser?

- **Alt kode og data på klientsiden kan læses og manipuleres**: Brugeren har fuld adgang til browserens hukommelse, localStorage, JavaScript-kode osv.
- **Krypteringsnøgler skal være tilgængelige for at dekryptere data**: Hvis du kan dekryptere i browseren, kan en angriber også.
- **Man-in-the-browser-angreb**: Hvis en bruger har malware eller ondsindede browser-udvidelser, kan de læse og ændre alt.

Derfor bør man aldrig stole på data, der kun ligger i browseren, og aldrig gemme hemmeligheder eller følsomme oplysninger på klientsiden.

## Hvorfor bruger vi JWT (JSON Web Token)?

JWT er en standard til at overføre information sikkert mellem klient og server:

- JWT er signeret (ofte med HMAC eller RSA), så serveren kan verificere, at data ikke er ændret.
- JWT kan indeholde claims (f.eks. bruger-id, roller), som serveren kan stole på, hvis signaturen er gyldig.
- JWT gemmes typisk i localStorage eller sessionStorage på klienten, men **følsomme data bør aldrig ligge i selve tokenet** – kun oplysninger, som ikke er hemmelige.
- JWT gør det muligt at have stateless authentication, hvor serveren ikke behøver at gemme sessioner.

**Bemærk:** JWT beskytter ikke mod tyveri af tokenet! Hvis en angriber får fat i JWT fra localStorage, kan de bruge det. Derfor er det vigtigt at beskytte mod XSS-angreb og overveje at bruge HttpOnly cookies, hvis muligt.

## Konklusion

- State i Blazor WASM kan gemmes i memory, localStorage eller på serveren.
- localStorage er praktisk, men ikke sikkert – kryptering kan bruges, men giver kun begrænset ekstra sikkerhed.
- Alt på klientsiden kan manipuleres, så følsomme data bør aldrig ligge i browseren.
- JWT bruges til at sikre, at data ikke kan manipuleres, men beskytter ikke mod tyveri af tokenet.

**Best practice:** Brug localStorage til ikke-følsomme data, og brug JWT til authentication/authorization, men vær opmærksom på sikkerhedsrisici i browseren.

## 14. Teori for dagen — kontekst: Uge 4 (35) - Bygge en SPA frontend på vores API ( - )

Teori for dagen

Der er ikke noget oplæg på klassen - men som lektier skal I se følgende video og udvælger noget at implementere, så den bruger dele af !  

Lemon er webudvikler og har lavet et rigtig godt oplæg omkring PWA - hvor vi skal tage et par ting fra!

## 15. Teori for dagen — kontekst: Uge 6 (37) - Integration af Active Directory ( - )

Teori for dagen

Følgende video skal I se i løbet af dagen eller som lektie til dagen! Andy, forklare rigtig godt omkring lokalopsætning af lokalt AD og teknologierne bag!

## 16. Teori for dagen — kontekst: Uge 6 (37) - Integration af Active Directory ( - )

Teori for dagen

### LDAP - **Lightweight Directory Access Protocol**

### **7 Principper i **Information Technology Infrastructure Library

ITIL 4's 7 principper giver en grundlæggende ramme for effektiv IT Service Management. Disse principper hjælper organisationer med at skabe værdi gennem services og forbedre deres service management praksis.

Principperne fokuserer på at skabe værdi, starte hvor man er, og arbejde iterativt med feedback for at opnå bedre resultater.

### **1. Focus on Value**

Alt hvad vi gør, skal skabe værdi for kunder og stakeholders.

**Eksempler:**

- Automatisering af rutineopgaver
- Forbedring af brugeroplevelse
- Reduktion af nedetid

### **2. Start Where You Are**

Brug eksisterende ressourcer og kapabiliteter som udgangspunkt.

**Eksempler:**

- Byg videre på eksisterende systemer
- Udnyt nuværende kompetencer
- Forbedre i stedet for at erstatte

### **3. Progress Iteratively with Feedback**

Gør små, inkrementelle forbedringer baseret på feedback.

**Eksempler:**

- Agile development
- Continual improvement
- Regular reviews

### **4. Collaborate and Promote Visibility**

Arbejd sammen og gør arbejdet synligt.

**Eksempler:**

- Cross-functional teams
- Transparent kommunikation
- Delte dashboards

### **5. Think and Work Holistically**

Overvej hele systemet, ikke kun dele.

**Eksempler:**

- End-to-end service view
- Integration mellem teams
- System thinking

### **6. Keep It Simple and Practical**

Vælg den enkleste løsning der virker.

**Eksempler:**

- Automatisering af simple opgaver
- Standardisering af processer
- Brug af eksisterende værktøjer

### **7. Optimize and Automate**

Optimér og automatisér hvor muligt.

**Eksempler:**

- Automatisk monitoring
- Self-service portals
- Automated testing
- Real-time dashboards (se \[\[SignalR\]\] for live data)

## 17. Teori for i dag - Mail Service — kontekst: Uge 7 (38) - Udvidelse af projekt generelt! ( - )

Teori for i dag - Mail Service

At sende mails med vores API kan hjælpe med at gøre vores program mere gennemført og måske også implementere et lag af sikkerhed! For at starte på at lave en Email service, skal vi først bruge en email - i følgende bruger vi en gratis Google Gmail, men med nogle små ændringer kan man også bruge en gratis Outlook-mail. 

## 🔧 Gmail Konfiguration

1. **Gå til din Google Account indstillinger**
   - Besøg: [https://myaccount.google.com/](https://myaccount.google.com/)
   - Vælg "Security" i venstre menu
2. **Aktiver 2-Factor Authentication**
   - Gå til "2-Step Verification"
   - Følg instruktionerne for at aktivere 2FA
   - Vælg "Mail" som app type
   - Vælg "Other" som enhed og navngiv det "H2-MAGS API"

### 2. Opdater appsettings.json

Erstat placeholder værdierne i `appsettings.json`:

```json
{
  "MailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "din-email@gmail.com",
    "FromEmail": "din-email@gmail.com",
    "FromName": "H2-MAGS System"
  }
}

```

## 🏗️ Hvordan Fungerer Det?

### 1. **SMTP Protokol**

```txt
[API Server] --SMTP--> [Gmail Server] --Internet--> [Modtager]
```

- **SMTP** = Simple Mail Transfer Protocol
- **Port 587** = Sikker email sending (TLS/SSL)
- **Gmail Server** = [smtp.gmail.com](http://smtp.gmail.com/)

### 2. **Email Flow i H2-MAGS**

```mermaid
graph TD
    A["Bruger registreres"] --> B["API opretter bruger i database"]
    B --> C["MailService.SendWelcomeEmailAsync()"]
    C --> D["Opret HTML template"]
    D --> E["SMTP forbindelse til Gmail"]
    E --> F["Send email via Gmail"]
    F --> G["Email leveret til bruger"]

    style A fill:#e1f5fe
    style G fill:#c8e6c9
    style C fill:#fff3e0

```

## 🔧 Teknisk Implementation

### **MailService.cs - Hovedkomponenten**

```cs
public class MailService
{
    // SMTP konfiguration fra appsettings.json
    private readonly string _smtpServer = "smtp.gmail.com";
    private readonly int _smtpPort = 587;
    private readonly string _smtpUsername; // Din Gmail

    // Send email via Gmail SMTP
    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        using var smtpClient = new SmtpClient(_smtpServer, _smtpPort)
        {
            EnableSsl = true,        // Gmail kræver SSL
        };

        await smtpClient.SendMailAsync(mailMessage);
    }
}
```

### **Integration i UsersController**

```cs
[HttpPost("register")]
public async Task Register([FromBody] RegisterDto dto)
{
    // 1. Opret bruger i database
    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    // 2. Send velkommen email AUTOMATISK
    await _mailService.SendWelcomeEmailAsync(dto.Email, dto.Username, userRole.Name);

    return Ok("Bruger oprettet og email sendt!");
}

```

### **Integration i BookingsController**

```cs
[HttpPost]
public async Task> PostBooking(BookingPostDto dto)
{
    // 1. Opret booking i database
    var booking = BookingMapping.ToBookingFromPostDto(dto, room.PricePerNight);
    _context.Bookings.Add(booking);
    await _context.SaveChangesAsync();

    // 2. Send booking bekræftelse email AUTOMATISK
    await _mailService.SendBookingConfirmationEmailAsync(
        user.Email, user.Username, room.Number,
        hotel.Name, booking.StartDate, booking.EndDate,
        booking.NumberOfGuests, booking.TotalPrice, booking.Id
    );

    return CreatedAtAction("GetBooking", new { id = booking.Id }, bookingDto);
}

```

## 📋 Email Typer i H2-MAGS

### 1. **Velkommen Email** 🎉

- **Når**: Ved brugeroprettelse
- **Indhold**: HTML template med brugerinfo
- **Automatisk**: Ja, ingen brugerinteraktion

### 2. **Test Email** 🧪

- **Når**: Admin tester konfiguration
- **Indhold**: Simpel test besked
- **Automatisk**: Ja, via test endpoint

### 3. **Booking Bekræftelse Email** 🏨

- **Når**: Ved booking oprettelse
- **Indhold**: Detaljeret booking information med pris og datoer
- **Automatisk**: Ja, sendes umiddelbart efter booking oprettelse

### 4. **Fremtidige Email Typer** 📅

- Booking ændringer og annulleringer
- System notifikationer
- Rapporter og alerts

## 🔒 Sikkerhed og Konfiguration

1. **2-Factor Authentication** påkrævet
3. **SMTP credentials** gemt sikkert i appsettings.json

```json
{
  "MailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "din-email@gmail.com",
    "FromEmail": "din-email@gmail.com",
    "FromName": "H2-MAGS System"
  }
}

```

## 🎨 HTML Email Templates

### **Professionel Styling**

- **Responsive design** - virker på alle enheder
- **Branding** - H2-MAGS farver og logo
- **Struktur** - Header, content, footer
- **Interaktivt** - Knapper og links

### **Template Eksempler**

### **Velkommen Email Template**

```html

    
        .container { max-width: 600px; margin: 0 auto; }
        .header { background: #007bff; color: white; }
        .button { background: #007bff; padding: 12px 25px; }
    

    
        🎉 Velkommen til H2-MAGS!
        Hej {username}, din konto er oprettet!

        [
            Log ind på systemet
        ]()
    

```

### **Booking Bekræftelse Email Template**

```html

    
        .container { max-width: 600px; margin: 0 auto; }
        .header { background: #28a745; color: white; }
        .booking-details { background: #f8f9fa; padding: 20px; }
        .price-highlight { background: #d4edda; text-align: center; }
        .booking-info { display: grid; grid-template-columns: 1fr 1fr; gap: 15px; }
    

    
        🏨 Booking Bekræftelse
        
            📋 Booking Detaljer
            
                Hotel: {hotelName}
                Rum: #{roomNumber}
                Check-in: {startDate}
                Check-out: {endDate}
                Gæster: {numberOfGuests}
                Nætter: {nights}
            
            
                Total pris: {totalPrice:C}
            
        
    

```

## 🎯 Praktiske Eksempler

### **Booking Email Flow**

```mermaid
graph TD
    A["Bruger opretter booking"] --> B["API validerer booking data"]
    B --> C["Booking gemmes i database"]
    C --> D["MailService.SendBookingConfirmationEmailAsync()"]
    D --> E["HTML template genereres"]
    E --> F["Email sendes via Gmail SMTP"]
    F --> G["Bruger modtager bekræftelse"]

    style A fill:#e1f5fe
    style G fill:#c8e6c9
    style D fill:#fff3e0

```

### **Email Data Flow**

| **Input Data** | **Email Output** |
| --- | --- |
| Username | Personaliseret hilsen |
| Room Number | Rum information |
| Hotel Name | Hotel detaljer |
| Start/End Date | Check-in/out datoer |
| Number of Guests | Gæste information |
| Total Price | Pris oversigt |
| Booking ID | Reference nummer |

## 🔧 Test og Debugging

### **Test Booking Email**

```bash
# Opret en test booking
POST /api/bookings
{
  "userId": "user-id",
  "roomId": "room-id",
  "startDate": "2025-02-01",
  "endDate": "2025-02-03",
  "numberOfGuests": 2
}

# Email sendes automatisk til brugeren

```

### **Email Logging**

```cs
_logger.LogInformation("📧 Sender booking bekræftelse email til: {Email}", userEmail);
_logger.LogInformation("✅ Booking bekræftelse email sendt succesfuldt til: {Email}", userEmail);
_logger.LogWarning("⚠️ Kunne ikke sende booking bekræftelse email til: {Email}", userEmail);

```

# Mere end bare SMTP

Selvom SMTP er standarden til at **sende** e-mails, er der andre protokoller der håndterer **modtagelse** og **synkronisering** af e-mails. Her er de vigtigste:

### **IMAP - Internet Message Access Protocol**

**Hvad er IMAP?**

- **Synkroniserer** e-mails mellem server og klient
- **Bevarer** e-mails på serveren
- **Multi-device** support - samme e-mails på alle enheder
- **Mappe struktur** synkroniseres

**Hvornår bruges IMAP?**

- **Moderne e-mail klienter** (Outlook, Apple Mail, Thunderbird)
- **Mobil enheder** og tablets
- **Fleksibel adgang** fra flere lokationer
- **Team miljøer** hvor flere skal se samme e-mails

**Fordele:**

- ✅ E-mails tilgængelige overalt
- ✅ Synkroniseret mappestruktur
- ✅ Hurtig søgning på server
- ✅ Delte mailbokse mulige

**Ulemper:**

- ❌ Kræver internet forbindelse
- ❌ Kan være langsommere end POP3
- ❌ Bruger mere server plads

---

### **POP3 - Post Office Protocol version 3**

**Hvad er POP3?**

- **Henter** e-mails fra server til lokal klient
- **Downloader** e-mails til computeren
- **Fjerner** e-mails fra serveren (standard)
- **Offline** adgang til hentede e-mails

**Hvornår bruges POP3?**

- **Lokale e-mail klienter** på en specifik computer
- **Begrænset server plads**
- **Offline arbejde** uden internet
- **Sikkerhed** - e-mails gemmes lokalt

**Fordele:**

- ✅ Hurtig offline adgang
- ✅ Sparer server plads
- ✅ Ingen internet krævet efter download
- ✅ Simpel protokol

**Ulemper:**

- ❌ E-mails kun på én enhed
- ❌ Ingen synkronisering
- ❌ Kan miste e-mails ved computer skade
- ❌ Ingen mappe synkronisering

---

## Sammenligning af E-mail Protokoller

| Aspekt | **SMTP** | **IMAP** | **POP3** |
| --- | --- | --- | --- |
| **Formål** | Sende e-mails | Synkronisere e-mails | Hente e-mails |
| **Port** | 25/587 | 143/993 | 110/995 |
| **Sikkerhed** | TLS/SSL | TLS/SSL | TLS/SSL |
| **Server lagring** | Midlertidig | Permanent | Midlertidig |
| **Multi-device** | N/A | ✅ Ja | ❌ Nej |
| **Offline adgang** | N/A | ❌ Begrænset | ✅ Ja |
| **Kompleksitet** | Medium | Høj | Lav |

---

## Hvad bruges mest i dag?

### **Moderne Standard: IMAP + SMTP**

```txt
📧 Moderne Setup:
├── SMTP (port 587) → Sende e-mails
└── IMAP (port 993) → Modtage e-mails

```

**Hvorfor IMAP er populært:**

- **Cloud-baseret** arbejdsgang
- **Mobil-first** tilgang
- **Team collaboration** muligheder
- **Backup** og sikkerhed

### **Legacy/Enterprise: POP3 + SMTP**

```txt
📧 Traditionel Setup:
├── SMTP (port 587) → Sende e-mails
└── POP3 (port 995) → Hente e-mails

```

**Hvorfor POP3 stadig bruges:**

- **Legacy systemer** der ikke er opdateret
- **Sikkerheds-kritiske** miljøer
- **Begrænset server plads**
- **Offline arbejdsgange**

---

## Praktiske Eksempler

### **Gmail Konfiguration:**

```txt
📧 Gmail Setup:
├── SMTP: smtp.gmail.com:587 (TLS)
├── IMAP: imap.gmail.com:993 (SSL)
└── POP3: pop.gmail.com:995 (SSL)
```

### **Outlook/Hotmail:**

```txt
📧 Outlook Setup:
├── SMTP: smtp-mail.outlook.com:587 (TLS)
├── IMAP: outlook.office365.com:993 (SSL)
└── POP3: outlook.office365.com:995 (SSL)
```

### **Custom Server:**

```txt
📧 Custom Setup:
├── SMTP: mail.example.com:587 (TLS)
├── IMAP: mail.example.com:993 (SSL)
└── POP3: mail.example.com:995 (SSL)
```

---

## Anbefalinger

### **Til Privat Brug:**

- **IMAP + SMTP** for moderne brugere
- **POP3 + SMTP** kun hvis du foretrækker offline arbejde

### **Til Virksomheder:**

- **IMAP + SMTP** for team collaboration
- **Exchange/Office 365** for enterprise features

### **Til Udviklere:**

- **SMTP** til at sende e-mails fra applikationer
- **IMAP** til at læse e-mails programmatisk
- **API'er** (Gmail API, Microsoft Graph) for moderne integration

---

**Konklusion:** Mens SMTP er uundværligt til at sende e-mails, er valget mellem IMAP og POP3 afhængigt af dine behov. IMAP er det moderne valg for de fleste brugere, mens POP3 stadig har sin plads i specifikke scenarier.

## 18. Teori for dagen — kontekst: Uge 8 (39) - Ticket system med Signal R ( - )

Teori for dagen

# Overvejelser til jeres dashboard

 

Her er 4 ting jeg gerne vil have I overvejer omkring jeres system!

1. Skal en ticket være koblet på en booking?
2. Hvad er vigtigt at vi spørger vores bruger om for at reducere antal gange vi skal kommunikere?
3. Hvordan fordeler I tickets internt i teamet - både mellem afdeling og medarbejdere?
4. Hvornår er en ticket løst?

# SignalR - Real-time Web Communication

## Hvad er SignalR?

SignalR er et .NET bibliotek, der gør det nemt at implementere real-time web funktionalitet i .NET applikationer. Det gør det muligt at sende data fra server til klienter i realtid uden at klienten behøver at spørge serveren om nye data.

## Hvad kan SignalR?

### 1. **Real-time Kommunikation**

- **Server-to-Client Push**: Serveren kan sende data til alle forbundne klienter øjeblikkeligt
- **Client-to-Server**: Klienter kan sende beskeder til serveren
- **Bidirektionel Kommunikation**: To-vejs kommunikation mellem server og klienter

### 2. **Automatisk Transport Valg**

SignalR vælger automatisk den bedste transport metode:

- **WebSockets** (bedste performance)
- **Server-Sent Events** (fallback)
- **Long Polling** (fallback for ældre browsere)

### 3. **Connection Management**

- **Automatisk Reconnection**: Genopretter forbindelse automatisk ved netværksproblemer
- **Connection Groups**: Organiser klienter i grupper for målrettet kommunikation
- **User Management**: Håndter individuelle brugerforbindelser

### 4. **Skalering**

- **Backplane Support**: Redis, SQL Server, Azure Service Bus
- **Load Balancing**: Distribuer forbindelser på tværs af flere servere
- **Horizontal Scaling**: Skaler ud til flere servere

## Use Cases

### **Chat Applikationer**

```cs
// Eksempel: Chat hub
public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}

```

### **Live Dashboards**

- Real-time data opdateringer
- Live statistikker
- System overvågning

### **Collaborative Tools**

- Google Docs-lignende redigering
- Live cursor positioner
- Real-time notifikationer

### **Gaming**

- Multiplayer spil
- Live score opdateringer
- Real-time spil events

### **Trading/Financial Apps**

- Live kurser
- Real-time handelsdata
- Market updates

## SignalR vs. Alternativer

### **Hvad er Alternativerne?**

### **WebSockets**

WebSockets er en protokol der etablerer en persistent, bidirektionel forbindelse mellem klient og server over en enkelt TCP forbindelse.

**Hvordan fungerer det:**

- Etablerer en persistent forbindelse via HTTP handshake
- Kommunikation sker direkte over TCP
- Lav latency og høj performance
- Kræver manuel implementering af connection management

**Eksempel:**

```javascript
const socket = new WebSocket('ws://localhost:5000/ws');
socket.onmessage = function(event) {
    console.log('Modtaget:', event.data);
};
socket.send('Hej fra klient!');

```

### **Server-Sent Events (SSE)**

Server-Sent Events er en web standard der gør det muligt for en server at sende data til en klient automatisk.

**Hvordan fungerer det:**

- Bruger standard HTTP forbindelse
- Kun server → klient kommunikation
- Automatisk reconnection
- Simpel implementering

**Eksempel:**

```javascript
const eventSource = new EventSource('/events');
eventSource.onmessage = function(event) {
    console.log('Modtaget:', event.data);
};

```

### **Long Polling**

Long Polling er en teknik hvor klienten sender en HTTP request og serveren holder forbindelsen åben indtil der er data at sende.

**Hvordan fungerer det:**

- Klient sender request og venter på svar
- Server holder forbindelsen åben indtil data er tilgængelig
- Klient sender ny request efter modtagelse
- Simulerer real-time kommunikation

**Eksempel:**

```javascript
function longPoll() {
    fetch('/poll')
        .then(response => response.json())
        .then(data => {
            console.log('Modtaget:', data);
            longPoll(); // Send ny request
        });
}

```

### **Sammenligningstabel**

| Feature | SignalR | WebSockets | Server-Sent Events | Long Polling |
| --- | --- | --- | --- | --- |
| Real-time | ✅ | ✅ | ✅ (Server→Client) | ❌ |
| Bidirektionel | ✅ | ✅ | ❌ | ❌ |
| Auto Fallback | ✅ | ❌ | ❌ | ❌ |
| .NET Integration | ✅ | ⚠️ | ⚠️ | ⚠️ |
| Skalering | ✅ | ⚠️ | ⚠️ | ❌ |

### Detaljeret Forklaring af Begreberne

### **Real-time Kommunikation**

- **SignalR**: ✅ Fuldt real-time med automatisk optimering
- **WebSockets**: ✅ Lav latency, direkte TCP forbindelse
- **Server-Sent Events**: ✅ Real-time, men kun server → klient
- **Long Polling**: ❌ Simulerer real-time, men med delay

### **Bidirektionel Kommunikation**

- **SignalR**: ✅ Server ↔ Klient i begge retninger
- **WebSockets**: ✅ Fuldt bidirektionel kommunikation
- **Server-Sent Events**: ❌ Kun server → klient (one-way)
- **Long Polling**: ❌ Kun server → klient (one-way)

### **Auto Fallback**

- **SignalR**: ✅ Vælger automatisk bedste transport:
  - WebSockets → Server-Sent Events → Long Polling
- **WebSockets**: ❌ Kræver manuel implementering af fallbacks
- **Server-Sent Events**: ❌ Ingen automatisk fallback
- **Long Polling**: ❌ Ingen automatisk fallback

### **.NET Integration**

- **SignalR**: ✅ Native .NET bibliotek med:
  - Dependency Injection support
  - Middleware pipeline integration
  - Type-safe hubs
  - Built-in authentication/authorization
- **WebSockets**: ⚠️ Kræver manuel implementering:
  - Custom WebSocket handlers
  - Manual connection management
  - Custom serialization
- **Server-Sent Events**: ⚠️ Kræver manuel implementering:
  - Custom HTTP endpoints
  - Manual event streaming
- **Long Polling**: ⚠️ Kræver manuel implementering:
  - Custom polling logic
  - Manual timeout handling

### **Praktiske Eksempler**

### **SignalR - Chat Applikation**

```cs
// Automatisk skalering med Redis
services.AddSignalR().AddRedis("connectionString");

// Type-safe hub
public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}

```

### **WebSockets - Manuel Implementering**

```cs
// Kræver manuel implementering
app.UseWebSockets();
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            // Manuel connection management
        }
    }
});

```

### **Server-Sent Events - Manuel Implementering**

```cs
// Kræver custom endpoint
app.MapGet("/events", async (HttpContext context) =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    // Manuel event streaming
});

```

### **Hvornår Vælge Hvad?**

### **Vælg SignalR når:**

- Du vil have real-time funktionalitet hurtigt
- Du bruger .NET økosystemet
- Du har brug for skalering
- Du vil have robust forbindelseshåndtering

### **Vælg WebSockets når:**

- Du har brug for maksimal performance
- Du vil have fuld kontrol over forbindelsen
- Du ikke bruger .NET (f.eks. Node.js, Python)

### **Vælg Server-Sent Events når:**

- Du kun har brug for server → klient kommunikation
- Du vil have enkel implementering
- Performance ikke er kritisk

### **Vælg Long Polling når:**

- Du har ældre browser support
- Du har meget simpel real-time behov
- Du ikke kan bruge WebSockets

## Hvordan fungerer det?

### 1. **Hub Pattern**

```cs
public class MyHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendToGroup(string groupName, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", message);
    }
}

```

### 2. **Client-side JavaScript**

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub")
    .build();

connection.start().then(function () {
    console.log("SignalR Connected");
});

connection.on("ReceiveMessage", function (user, message) {
    // Håndter modtaget besked
});

```

### 3. **Server Configuration**

```cs
// Program.cs
builder.Services.AddSignalR();

app.MapHub("/chathub");

```

## Fordele ved SignalR

1. **Enkel Implementation**: Minimal kode for real-time funktionalitet
2. **Automatisk Transport**: Vælger bedste transport automatisk
3. **Robust**: Håndterer forbindelsesproblemer automatisk
4. **Skalerbar**: Understøtter load balancing og backplane
5. **Cross-platform**: Virker på alle .NET platforme
6. **Type Safety**: Stærkt typet med C# generics

## Ulemper

1. **Microsoft-specifik**: Primært for .NET økosystemet
2. **Memory Usage**: Kan bruge meget hukommelse med mange forbindelser
3. **Complexity**: Kan blive komplekst ved høj skala
4. **Learning Curve**: Kræver forståelse af hub pattern

## Praktisk Implementering - Ticket Support System

### Vores Implementering

I vores H2 projekt har vi implementeret et komplet ticket support system med SignalR til real-time kommunikation mellem brugere og support medarbejdere.

### **API Side - TicketHub**

```cs
public class TicketHub : Hub
{
    // Tilslut til ticket chat
    public async Task JoinTicketGroup(string ticketId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.ConnectionId;
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Demo User";

        // Valider adgang til ticket
        if (!await ValidateTicketAccess(ticketId, userId, userRole))
        {
            await Clients.Caller.SendAsync("Error", "Du har ikke adgang til denne ticket");
            return;
        }

        // Tilslut til ticket gruppe
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Ticket_{ticketId}");

        // Notificer andre i gruppen
        await Clients.Group($"Ticket_{ticketId}").SendAsync("UserJoined", new
        {
            Username = username,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        });
    }

    // Send besked til ticket chat
    public async Task SendMessage(string ticketId, string message, bool isInternal = false)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.ConnectionId;
        var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Demo User";
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "User";

        // Send besked til alle i ticket gruppen
        await Clients.Group($"Ticket_{ticketId}").SendAsync("MessageReceived",
            username, message, isInternal, DateTime.UtcNow);
    }
}

```

### **Blazor Client Side - TicketSignalRService**

```cs
public class TicketSignalRService : INotifyPropertyChanged, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;

    public async Task InitializeAsync(string token)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                // For demo mode, skip JWT authentication
                if (token != "demo-token")
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                }
            })
            .WithAutomaticReconnect()
            .Build();

        // Registrer event handlers
        RegisterEventHandlers();
        await _hubConnection.StartAsync();
    }

    public async Task JoinTicketAsync(string ticketId, string userId, string username, string userRole)
    {
        await _hubConnection.InvokeAsync("JoinTicketGroup", ticketId);
    }

    public async Task SendMessageAsync(string message, bool isInternal = false)
    {
        if (CurrentTicketId != null)
        {
            await _hubConnection.InvokeAsync("SendMessage", CurrentTicketId, message, isInternal);
        }
    }
}

```

### **Blazor UI - TicketChat Component**

```razor
@* H2-MAGS/Blazor/Components/TicketChat.razor *@

    
        Ticket Chat - @TicketNumber
        
            @if (SignalRService.IsConnected)
            {
                🟢 Forbundet
            }
            else
            {
                🔴 Ikke forbundet
            }
        
    

    
        @foreach (var message in Messages)
        {
            
                
                    **@message.AuthorName**
                    @message.Timestamp.ToString("HH:mm")
                    @if (message.IsInternal)
                    {
                        Intern
                    }
                
                @message.Message
            
        }
    

```

### **Konfiguration**

### **API Program.cs**

```cs
// Tilføj SignalR til services
builder.Services.AddSignalR();

// Map SignalR Hub med transport konfiguration
app.MapHub("/tickethub", options =>
{
    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
});

```

### **Blazor Program.cs**

```cs
// Registrer TicketSignalRService
builder.Services.AddScoped();

```

### **Funktioner i vores implementering**

1. **Real-time Chat**: Live messaging mellem brugere og support
2. **Gruppe Management**: Brugere tilslutter specifikke ticket chats
3. **Authentication**: JWT token baseret authentication (med demo mode)
4. **Role-based Access**: Forskellige rettigheder for brugere vs. support
5. **Internal Messages**: Support kan sende interne beskeder
6. **Typing Indicators**: Viser når brugere skriver
7. **Auto Reconnect**: Automatisk genopretning af forbindelse
8. **Connection Status**: Viser forbindelsesstatus i UI

### **Transport Protokoller**

Vores implementering bruger:

- **WebSockets** (primær transport)
- **Long Polling** (fallback for ældre browsere)

### **Skalering**

For fremtidig skalering kan vi tilføje:

- Redis backplane for load balancing
- SQL Server backplane for persistence
- Azure Service Bus for cloud skalering

## Konklusion

SignalR er et kraftfuldt værktøj til real-time web applikationer i .NET. Det gør det nemt at implementere funktionalitet som chat, live dashboards, og collaborative tools uden at skulle håndtere kompleksiteten ved WebSockets eller andre transport protokoller manuelt.

**Vores praktiske implementering viser:**

- Komplet ticket support system med real-time chat
- JWT authentication integration
- Role-based access control
- Robust error handling og reconnection
- Clean separation mellem API og Blazor client

Det er ideelt til:

- Chat applikationer ✅ (Vores ticket chat)
- Live dashboards
- Collaborative tools
- Gaming
- Financial/trading apps
- Eventuelt alle applikationer der kræver real-time kommunikation

For en mere grundgennemgang helt fra bunden anbefaler jeg følgende 2 videoer!

## 19. Teori for i dag — kontekst: Uge 9 (40) - Færdiggørelse af projekt ( - )

Teori for i dag

For Aspire, findes der rigtig mange videoer, guides og generelle informationer! Microsoft pusher rigtig meget på at Aspire skal være en del af alles dotnet oplevelse og specielt fra dotnet 10 som kommer om 1-2 måneder, er der meget vægt på cloud og dermed også Aspire!

Jeg viser et eksempel på klassen, om hvor nemt det er at sætte op og hvad man kan med Aspire! I kan finde kilde koden til det her:
