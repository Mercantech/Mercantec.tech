namespace Mercantec.Tech.Api.Integrations.OpenAI;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = "";
    /// <summary>Default: gpt-6-luna (OpenAI billigste capable chat, $0.10/$0.50 per 1M).</summary>
    public string Model { get; set; } = "gpt-6-luna";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string EditorModel { get; set; } = "gpt-6.1-sol";
    public string EditorReasoningEffort { get; set; } = "high";
    public int EditorResponseTokens { get; set; } = 32768;
    public int EditorRunTokens { get; set; } = 131072;
    public int EditorRunMinutes { get; set; } = 10;
    public int EditorMaxBatches { get; set; } = 128;
    public int EditorRetentionDays { get; set; } = 30;
    public int EditorWorkerConcurrency { get; set; } = 2;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
