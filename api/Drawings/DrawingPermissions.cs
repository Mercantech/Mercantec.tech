using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Drawings;

public sealed partial class DrawingService
{
    private async Task<Drawing> ManageAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await FindAsync(id, ct) ?? throw DrawingValidation.Denied();
        if (!await _access.CanManageAsync(drawing, user, ct)) throw DrawingValidation.Denied();
        return drawing;
    }
    public async Task<DrawingContext> SettingsAsync(Guid id, UpdateDrawingSettings input, ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await ManageAsync(id, user, ct);
        if (input.Mode is not ("snapshot" or "live")) throw DrawingValidation.Invalid();
        await using (var transaction = await _db.Database.BeginTransactionAsync(ct))
        {
            await LockParentAsync(drawing.Domain, drawing.DocumentId, ct);
            var written = await _db.Drawings.Where(d => d.Id == id && d.AccessVersion == input.ExpectedAccessVersion)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Mode, input.Mode).SetProperty(d => d.AccessVersion, d => d.AccessVersion + 1), ct);
            if (written != 1) throw DrawingValidation.Changed();
            if (drawing.Mode != input.Mode)
                await TouchParentAsync(drawing.Domain, drawing.DocumentId, DateTimeOffset.UtcNow, UserClaims.EditorName(user), ct);
            await transaction.CommitAsync(ct);
        }
        await InvalidateAsync(id, ct);
        return await ContextAsync(id, user, false, null, ct);
    }
    public async Task<DrawingGrantView[]> GrantsAsync(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        await ManageAsync(id, user, ct);
        return await _db.DrawingGrants.AsNoTracking().Where(g => g.DrawingId == id).OrderBy(g => g.DisplayName)
            .Select(g => new DrawingGrantView(g.Id, g.UserSub, g.HoldId, g.DisplayName)).ToArrayAsync(ct);
    }
    public async Task<DrawingCandidates> CandidatesAsync(Guid id, ClaimsPrincipal user, string? search, CancellationToken ct)
        => await _access.CandidatesAsync(await ManageAsync(id, user, ct), user, search, ct);

    public async Task<DrawingGrantView> GrantAsync(Guid id, SetDrawingGrant input, ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await ManageAsync(id, user, ct);
        if ((input.UserSub is null) == (input.HoldId is null) || input.UserSub == Guid.Empty || input.HoldId == Guid.Empty) throw DrawingValidation.Invalid();
        var existing = await _db.DrawingGrants.AsNoTracking().SingleOrDefaultAsync(g => g.DrawingId == id && g.UserSub == input.UserSub && g.HoldId == input.HoldId, ct);
        if (existing is not null) return new(existing.Id, existing.UserSub, existing.HoldId, existing.DisplayName);
        string name;
        if (input.UserSub is Guid sub)
        {
            var profile = await _db.NotebookProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Sub == sub, ct);
            if (!await _access.CanReadParentAsync(drawing, DrawingAccess.CandidatePrincipal(sub, profile?.IsTeacher == true), ct)) throw DrawingValidation.Invalid("drawing_recipient_must_read_parent");
            name = profile?.DisplayName ?? await _db.Memberships.Where(m => m.UserSub == sub).Select(m => m.DisplayName).FirstOrDefaultAsync(ct) ?? "Bruger";
        }
        else
        {
            var candidates = await _access.CandidatesAsync(drawing, user, null, ct);
            name = candidates.Holds.SingleOrDefault(h => h.Id == input.HoldId)?.DisplayName ?? throw DrawingValidation.Invalid("drawing_hold_must_read_parent");
        }
        var grant = new DrawingGrant { Id = Guid.NewGuid(), DrawingId = id, UserSub = input.UserSub, HoldId = input.HoldId,
            DisplayName = name.Length > 200 ? name[..200] : name, CreatedAtUtc = DateTimeOffset.UtcNow };
        await using (var transaction = await _db.Database.BeginTransactionAsync(ct))
        {
            await LockParentAsync(drawing.Domain, drawing.DocumentId, ct);
            await _db.Drawings.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.AccessVersion, d => d.AccessVersion + 1), ct);
            _db.DrawingGrants.Add(grant); await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        await InvalidateAsync(id, ct);
        return new(grant.Id, grant.UserSub, grant.HoldId, grant.DisplayName);
    }
    public async Task RevokeAsync(Guid id, Guid grantId, ClaimsPrincipal user, CancellationToken ct)
    {
        var drawing = await ManageAsync(id, user, ct);
        await using (var transaction = await _db.Database.BeginTransactionAsync(ct))
        {
            await LockParentAsync(drawing.Domain, drawing.DocumentId, ct);
            await _db.DrawingGrants.Where(g => g.DrawingId == id && g.Id == grantId).ExecuteDeleteAsync(ct);
            await _db.Drawings.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.AccessVersion, d => d.AccessVersion + 1), ct);
            await transaction.CommitAsync(ct);
        }
        await InvalidateAsync(id, ct);
    }
    private async Task InvalidateAsync(Guid id, CancellationToken ct)
    {
        // Permission checks also run for each drawing update. A failed eager disconnect cannot grant access.
        try { await _engine.InvalidateAsync(id, ct); } catch (HttpRequestException) { }
    }
}
