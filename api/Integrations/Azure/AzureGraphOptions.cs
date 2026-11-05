namespace Mercantec.Tech.Api.Integrations.Azure;

public sealed class AzureGraphOptions
{
    public const string SectionName = "Azure";

    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Valgfri e-mail→UPN-overrides (semikolon-separeret: email=upn;...).
    /// Standard: Kasper → kasc@mercantec.dk, Mathias → mags@mercantec.dk.
    /// </summary>
    public string TeacherUpnMap { get; set; } =
        "kasc@mercantec.dk=kasc@mercantec.dk;mags@mercantec.dk=mags@mercantec.dk";

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
            var email = part[..eq].Trim();
            var upn = part[(eq + 1)..].Trim();
            if (email.Length > 0 && upn.Length > 0)
                map[email] = upn;
        }
        return map;
    }

    public string? ResolveTeacherUpn(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var trimmed = email.Trim();
        var map = ParseTeacherUpnMap();
        if (map.TryGetValue(trimmed, out var mapped))
            return mapped;

        // Samme e-mail som UPN (typisk Mercantec-konti)
        if (trimmed.Contains('@', StringComparison.Ordinal))
            return trimmed;

        return null;
    }
}
