using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Updates.Automation;

public sealed class PlatformUpdatesAutomationKey(IOptions<PlatformUpdatesAutomationOptions> options)
{
    public bool IsAuthorized(string? supplied)
    {
        if (string.IsNullOrEmpty(supplied) || supplied.Length > 512) return false;
        try
        {
            var file = new FileInfo(options.Value.ServiceKeyFile);
            if (!file.Exists || file.Length is < 32 or > 1024) return false;
            var expected = File.ReadAllText(file.FullName).Trim();
            if (expected.Length is < 32 or > 512) return false;
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
                SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { return false; }
    }
}
