using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Mercantec.Tech.Api.Emojis;

public static class ExtensionUploadCredentials
{
    public const string Scheme = "ExtensionUploadKey";
    public const string KeyIdClaim = "lup_extension_key_id";
    public const string ScopeClaim = "lup_extension_scope";
    public const string UploadScope = "emoji:create";
    public const int LifetimeDays = 90;
    public const int MaxActiveKeys = 5;

    public static string Create() => "lup_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static bool IsWellFormed(string? key) => key is { Length: 47 } && key.StartsWith("lup_", StringComparison.Ordinal)
        && key.AsSpan(4).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".AsSpan()) < 0;

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
