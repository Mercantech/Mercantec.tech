namespace Mercantec.Tech.Api.Integrations.OpenAI;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = "";
    /// <summary>Default: gpt-6-luna (OpenAI billigste capable chat, $0.10/$0.50 per 1M).</summary>
    public string Model { get; set; } = "gpt-6-luna";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
