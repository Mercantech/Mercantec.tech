using System.Security.Cryptography;
using System.Text;

namespace Mercantec.Tech.Api.Notebooks;

/// <summary>The existing private document service credential. Never sent to the browser.</summary>
public sealed class NotebookServiceKey(IConfiguration configuration)
{
    public string? Read()
    {
        var secret = configuration["Notion:ServiceSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            var path = configuration["Notion:ServiceSecretFile"];
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return null;
            try
            {
                using var stream = File.OpenRead(path);
                if (stream.Length > 1024) return null;
                using var reader = new StreamReader(stream);
                secret = reader.ReadToEnd().Trim();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
        }
        return secret is { Length: >= 32 and <= 1024 } ? secret : null;
    }

    public bool Accepts(HttpRequest request)
    {
        var expected = Read();
        if (expected is null || !request.Headers.TryGetValue("X-Notion-Service-Key", out var supplied)
            || supplied.Count != 1 || supplied[0] is not { Length: <= 1024 } value) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
