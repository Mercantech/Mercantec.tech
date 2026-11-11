namespace Mercantec.Tech.Web.Theming;

public enum HackerIncidentStage
{
    Investigating,
    Isolated,
    Contained,
    Restored,
    Verified,
    Recovered
}

/// <summary>A single fictional incident. Commands only read or change this instance's exercise data.</summary>
public sealed class HackerIncidentLab
{
    private const string Service = "catalog";
    private const string IncidentAccount = "catalog-preview";
    private const string GoodSnapshot = "0730";
    private const string FailedSnapshot = "0835";

    private static readonly string[] KnownGoodCatalog =
    [
        "MOD-01  Adgang og roller",
        "MOD-02  Loganalyse",
        "MOD-03  Sikker software",
        "MOD-04  Backup og gendannelse",
        "MOD-05  Netværksovervågning",
        "MOD-06  Hændelseshåndtering"
    ];

    private static readonly string[] Files =
        ["incident.txt", "auth.log", "changes.log", "backups.txt", "catalog.txt", "accounts.txt", "activity.log"];

    private const string Help = """
        help                    Vis kommandoer
        ls                      Vis øvelsens filer
        cat <fil>               Læs en fil
        status                  Vis tjeneste, konto og data
        hint                    Få et fagligt spor
        isolate <tjeneste>      Sæt tjenesten på pause
        disable <konto>         Spær en konto i øvelsen
        restore <kopi>          Gendan kataloget fra en kopi
        verify                  Kontrollér de gendannede data
        reopen <tjeneste>       Åbn tjenesten efter kontrollen
        """;

    private const string Brief = """
        08:31. Kursuskataloget viser fire af de seks forventede moduler.
        Find den berørte tjeneste og konto ved at sammenholde loggene.
        Afgræns hændelsen, gendan data fra en kontrolleret kopi, og
        verificér resultatet, før tjenesten åbnes igen.
        """;

    private const string AuthLog = """
        Tid       Tjeneste  Session  Konto              Hændelse
        08:18:02  catalog   K-100    teacher-demo       Planlagt session
        08:24:17  catalog   K-164    sync-worker        Session udløbet
        08:26:40  catalog   K-271    catalog-preview    Session uden for aftalt tidsrum
        08:29:08  status    K-401    monitor            Planlagt statuskontrol
        """;

    private const string ChangeLog = """
        Tid       Tjeneste  Session  Poster  Hændelse
        08:18:04  catalog   K-100    6/6     Planlagt revision
        08:27:02  catalog   K-271    4/6     Uplanlagt ændring af kataloget
        08:31:11  status    K-401    4/6     Driftsalarm
        """;

    private const string Backups = """
        Kopi  Tid    Poster  Kontrol
        0730  07:30  6/6     Godkendt manifest, før ændringen
        0835  08:35  4/6     Manifest afviger, efter ændringen
        """;

    private readonly HashSet<string> _readFiles = new(StringComparer.Ordinal);
    private readonly List<string> _activity = ["08:31  HÆNDELSE  Afvigelse registreret i kursuskataloget."];
    private string[] _catalog = [.. KnownGoodCatalog.Take(2), "MOD-03  Sikker software (kladde)", KnownGoodCatalog[3]];

    public HackerIncidentStage Stage { get; private set; } = HackerIncidentStage.Investigating;

    private bool ServiceIsolated => Stage is HackerIncidentStage.Isolated or HackerIncidentStage.Contained
        or HackerIncidentStage.Restored or HackerIncidentStage.Verified;

    private bool AccountBlocked => Stage is HackerIncidentStage.Contained or HackerIncidentStage.Restored
        or HackerIncidentStage.Verified or HackerIncidentStage.Recovered;

    public string Execute(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Skriv help for at se kommandoerne.";
        if (input.Length > 100) return "Kommandoen er for lang. Se help.";

        var parts = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 2) return "Brug én kommando med højst ét argument. Se help.";

        var command = parts[0].ToLowerInvariant();
        var target = parts.Length == 2 ? parts[1].ToLowerInvariant() : null;
        return (command, target) switch
        {
            ("help", null) => Help,
            ("ls", null) => string.Join('\n', Files),
            ("status", null) => Status(),
            ("hint", null) => Hint(),
            ("cat", { } file) => ReadFile(file),
            ("isolate", { } service) => Isolate(service),
            ("disable", { } account) => Disable(account),
            ("restore", { } snapshot) => Restore(snapshot),
            ("verify", null) => Verify(),
            ("reopen", { } service) => Reopen(service),
            _ => "Kommandoen eller argumentet er ukendt. Se help."
        };
    }

    private string ReadFile(string file)
    {
        var contents = file switch
        {
            "incident.txt" => Brief,
            "auth.log" => AuthLog,
            "changes.log" => ChangeLog,
            "backups.txt" => Backups,
            "catalog.txt" => string.Join('\n', _catalog),
            "accounts.txt" => $"teacher-demo     aktiv\nsync-worker      aktiv\n{IncidentAccount}  {(AccountBlocked ? "spærret" : "aktiv")}",
            "activity.log" => string.Join('\n', _activity),
            _ => null
        };
        if (contents is null) return "Filen findes ikke. Brug ls til at se øvelsens filer.";

        _readFiles.Add(file);
        return contents;
    }

    private string Status()
    {
        var serviceState = ServiceIsolated ? "isoleret" : "åben";
        var accountState = AccountBlocked ? "spærret" : "afventer";
        var dataState = Stage switch
        {
            HackerIncidentStage.Restored => "gendannet, afventer kontrol",
            HackerIncidentStage.Verified or HackerIncidentStage.Recovered => "verificeret",
            _ => "afvigelse"
        };
        return $"""
            Tjeneste       {Service} · {serviceState}
            Kontoindgreb   {accountState}
            Katalog        {_catalog.Length}/{KnownGoodCatalog.Length} poster · {dataState}
            """;
    }

    private string Hint() => Stage switch
    {
        HackerIncidentStage.Investigating => "Cyberbien: Læs auth.log og changes.log. Sammenhold tjeneste og sessions-id ved den uplanlagte ændring.",
        HackerIncidentStage.Isolated => "Ræv: Find den samme session i loginloggen, og spær kontoen bag den.",
        HackerIncidentStage.Contained => "Bæver: Læs backups.txt. Vælg en godkendt kopi fra før den uplanlagte ændring.",
        HackerIncidentStage.Restored => "Bæver: Brug verify til at kontrollere, at gendannelsen har de forventede poster.",
        HackerIncidentStage.Verified => "Cyberbien: Kontrollér status, og genåbn tjenesten med reopen.",
        _ => "Gennemgå activity.log. Hvilket signal ville få jer til at opdage hændelsen tidligere næste gang?"
    };

    private string Isolate(string service)
    {
        if (service != Service) return "Tjenesten matcher ikke den uplanlagte ændring i loggen.";
        if (Stage == HackerIncidentStage.Recovered) return "Hændelsen er afsluttet.";
        if (Stage != HackerIncidentStage.Investigating) return "Tjenesten er allerede isoleret.";
        if (!_readFiles.Contains("auth.log") || !_readFiles.Contains("changes.log"))
            return "Læs auth.log og changes.log før isolering, så tjenesten kan knyttes til ændringen.";

        Stage = HackerIncidentStage.Isolated;
        Record("ISOLERET", $"{Service} er sat på pause i øvelsen.");
        return $"{Service} er isoleret. Undersøg, hvilken konto der skal spærres.";
    }

    private string Disable(string account)
    {
        if (Stage != HackerIncidentStage.Isolated)
            return AccountBlocked ? "Den berørte konto er allerede spærret." : "Isolér tjenesten, før du spærrer en konto.";
        if (account != IncidentAccount) return "Kontoen matcher ikke sessionen ved den uplanlagte ændring. Gennemgå auth.log.";

        Stage = HackerIncidentStage.Contained;
        Record("SPÆRRET", $"{IncidentAccount} kan ikke længere starte en session i øvelsen.");
        return $"{IncidentAccount} er spærret. Læs backups.txt, og vælg en kopi.";
    }

    private string Restore(string snapshot)
    {
        if (Stage != HackerIncidentStage.Contained) return "Tjenesten skal være isoleret og kontoen spærret, før en kopi kan indlæses.";
        if (!_readFiles.Contains("backups.txt")) return "Læs backups.txt, og kontrollér kopiens tidspunkt og manifest.";
        if (snapshot == FailedSnapshot) return "Kopi 0835 afviger fra manifestet. Gendannelsen blev afbrudt før ændring af kataloget.";
        if (snapshot != GoodSnapshot) return "Kopien findes ikke. Se mulighederne i backups.txt.";

        _catalog = [.. KnownGoodCatalog];
        Stage = HackerIncidentStage.Restored;
        Record("GENDANNET", $"Kopi {GoodSnapshot} indlæst med {_catalog.Length} katalogposter.");
        return $"Kopi {GoodSnapshot} er indlæst. Kataloget har {_catalog.Length} poster. Kør verify før genåbning.";
    }

    private string Verify()
    {
        if (Stage is HackerIncidentStage.Verified or HackerIncidentStage.Recovered) return "Kataloget er allerede verificeret.";
        if (Stage != HackerIncidentStage.Restored) return "Gendan kataloget fra en godkendt kopi før kontrol.";
        if (!_catalog.SequenceEqual(KnownGoodCatalog)) return "Kontrollen fandt en afvigelse. Tjenesten forbliver isoleret.";

        Stage = HackerIncidentStage.Verified;
        Record("VERIFICERET", $"Alle {_catalog.Length} poster matcher kontrolmanifestet.");
        return $"Kontrol gennemført. Alle {_catalog.Length} poster matcher manifestet.";
    }

    private string Reopen(string service)
    {
        if (service != Service) return "Angiv den tjeneste, som blev isoleret.";
        if (Stage == HackerIncidentStage.Recovered) return "Tjenesten er allerede genåbnet.";
        if (Stage != HackerIncidentStage.Verified) return "Kontrollér den gendannede kopi med verify, før tjenesten åbnes igen.";

        Stage = HackerIncidentStage.Recovered;
        Record("GENÅBNET", $"{Service} er åbnet. {IncidentAccount} forbliver spærret.");
        return $"{Service} er genåbnet med {_catalog.Length} verificerede poster. Den berørte konto er stadig spærret. Se forløbet i activity.log.";
    }

    private void Record(string action, string detail) => _activity.Add($"Trin {_activity.Count}  {action}  {detail}");
}
