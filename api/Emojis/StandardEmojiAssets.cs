namespace Mercantec.Tech.Api.Emojis;

public static class StandardEmojiAssets
{
    public const string KeyPrefix = "standard-emojis/";

    public static bool IsStandard(string key) =>
        key.StartsWith(KeyPrefix, StringComparison.Ordinal);

    public static Stream? Open(string key) =>
        IsStandard(key) ? typeof(StandardEmojiAssets).Assembly.GetManifestResourceStream(key) : null;
}
