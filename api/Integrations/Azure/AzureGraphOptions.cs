namespace Mercantec.Tech.Api.Integrations.Azure;

public sealed class AzureGraphOptions
{
    public const string SectionName = "Azure";

    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Auth-nøgle → Azure UPN (semikolon-separeret: nøgle=upn;...).
    /// Nøgle kan være Auth <c>sub</c> (GUID) eller e-mail.
    /// Standard: Mathias (sub + Gmail + MAGS) → mags@mercantec.dk; Kasper → kasc@mercantec.dk.
    /// </summary>
    public string TeacherUpnMap { get; set; } =
        "d969fced-f7be-46d4-9bb9-5a2f03ca6974=mags@mercantec.dk;" +
        "mathiasgsteenberg@gmail.com=mags@mercantec.dk;" +
        "mags@mercantec.dk=mags@mercantec.dk;" +
        "kasc@mercantec.dk=kasc@mercantec.dk";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);

    public IReadOnlyDictionary<string, string> ParseTeacherUpnMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in TeacherUpnMap.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0 || eq >= part.Length - 1)
                continue;
            var key = part[..eq].Trim();
            var upn = part[(eq + 1)..].Trim();
            if (key.Length > 0 && upn.Length > 0)
                map[key] = upn;
        }
        return map;
    }

    /// <summary>
    /// Resolver Azure UPN: først Auth <paramref name="sub"/>, derefter <paramref name="email"/>,
    /// ellers e-mail som UPN når den ligner en postkasse.
    /// </summary>
    public string? ResolveTeacherUpn(string? sub, string? email = null)
    {
        var map = ParseTeacherUpnMap();

        if (!string.IsNullOrWhiteSpace(sub) && map.TryGetValue(sub.Trim(), out var bySub))
            return bySub;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var trimmed = email.Trim();
            if (map.TryGetValue(trimmed, out var byEmail))
                return byEmail;

            // Samme e-mail som UPN (typisk Mercantec-konti)
            if (trimmed.Contains('@', StringComparison.Ordinal))
                return trimmed;
        }

        return null;
    }
}
