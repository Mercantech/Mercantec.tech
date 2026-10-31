namespace Mercantec.Tech.Api.Integrations.Lectio;

public class LectioOptions
{
    public const string SectionName = "Lectio";

    /// <summary>Base-URL til Lectio-Collector API, fx https://lc.mags.dk/api/</summary>
    public string BaseUrl { get; set; } = "https://lc.mags.dk/api/";
}
