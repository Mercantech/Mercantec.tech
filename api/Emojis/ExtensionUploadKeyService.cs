using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Emojis;

public sealed class ExtensionUploadKeyService(AppDbContext db, IOptions<ExtensionUploadOptions> options, TimeProvider clock)
{
    public bool CanManage(ClaimsPrincipal user) => options.Value.CanManage(user);

    public async Task<ExtensionUploadKey?> Authenticate(string? rawKey, CancellationToken ct)
    {
        if (!ExtensionUploadCredentials.IsWellFormed(rawKey)) return null;
        var hash = ExtensionUploadCredentials.Hash(rawKey!);
        var key = await db.ExtensionUploadKeys.SingleOrDefaultAsync(item => item.KeyHash == hash, ct);
        var now = clock.GetUtcNow();
        if (key is null || key.OwnerSub == Guid.Empty || key.RevokedAtUtc is not null || key.ExpiresAtUtc <= now
            || !options.Value.Allows(key.OwnerSub, key.OwnerEmail)) return null;
        if (key.LastUsedAtUtc is null || key.LastUsedAtUtc < now.AddMinutes(-1))
        {
            key.LastUsedAtUtc = now;
            await db.SaveChangesAsync(ct);
        }
        return key;
    }

    public async Task<IReadOnlyList<ExtensionUploadKeyInfo>> List(ClaimsPrincipal user, CancellationToken ct)
    {
        if (!CanManage(user)) return [];
        var sub = UserClaims.GetSub(user)!.Value;
        var keys = await db.ExtensionUploadKeys.AsNoTracking().Where(item => item.OwnerSub == sub).ToListAsync(ct);
        return keys.OrderByDescending(item => item.CreatedAtUtc).Select(Info).ToList();
    }

    public async Task<CreatedExtensionUploadKey?> Create(ClaimsPrincipal user, string label, CancellationToken ct)
    {
        if (!CanManage(user)) throw new UnauthorizedAccessException();
        var sub = UserClaims.GetSub(user)!.Value;
        var now = clock.GetUtcNow();
        var previous = await db.ExtensionUploadKeys.Where(item => item.OwnerSub == sub && item.RevokedAtUtc == null).ToListAsync(ct);
        if (previous.Count(item => item.ExpiresAtUtc > now) >= ExtensionUploadCredentials.MaxActiveKeys) return null;
        var raw = ExtensionUploadCredentials.Create();
        var key = new ExtensionUploadKey
        {
            Id = Guid.NewGuid(), OwnerSub = sub, OwnerEmail = ExtensionUploadOptions.Email(user)!.Trim().ToLowerInvariant(),
            Label = label, KeyHash = ExtensionUploadCredentials.Hash(raw), CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(ExtensionUploadCredentials.LifetimeDays)
        };
        db.ExtensionUploadKeys.Add(key);
        await db.SaveChangesAsync(ct);
        return new(key.Id, raw, key.Label, key.CreatedAtUtc, key.ExpiresAtUtc);
    }

    public async Task<bool> Revoke(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        if (!CanManage(user)) throw new UnauthorizedAccessException();
        var sub = UserClaims.GetSub(user)!.Value;
        var key = await db.ExtensionUploadKeys.SingleOrDefaultAsync(item => item.Id == id && item.OwnerSub == sub, ct);
        if (key is null) return false;
        key.RevokedAtUtc ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static ExtensionUploadKeyInfo Info(ExtensionUploadKey key) =>
        new(key.Id, key.Label, key.CreatedAtUtc, key.ExpiresAtUtc, key.LastUsedAtUtc, key.RevokedAtUtc);
}

public sealed record ExtensionUploadKeyInfo(Guid Id, string Label, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? LastUsedAtUtc, DateTimeOffset? RevokedAtUtc);
public sealed record CreatedExtensionUploadKey(Guid Id, string Key, string Label, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc);
