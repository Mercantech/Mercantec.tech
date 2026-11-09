using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Avatars;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mercantec.Tech.Api.Controllers;

[ApiController]
[Route("api/me/avatars")]
[Authorize]
public sealed class AvatarsController(AppDbContext db) : ControllerBase
{
    private const int MaxAttempts = 5;

    [HttpGet]
    public Task<ActionResult<AvatarProfileDto>> Get(CancellationToken ct) =>
        ExecuteAsync(null, ct);

    [HttpPost("{id}/unlock")]
    public Task<ActionResult<AvatarProfileDto>> Unlock(string id, CancellationToken ct)
    {
        var avatar = AvatarEconomy.FindAvatar(id);
        if (avatar is null)
            return Task.FromResult<ActionResult<AvatarProfileDto>>(Failure(404, "avatar_not_found", "Avataren findes ikke."));

        return ExecuteAsync((profile, progress) =>
        {
            if (profile.Avatars.Any(owned => owned.AvatarId == id)) return Mutation.Unchanged;
            if (profile.Level < avatar.Level)
                return Mutation.Fail(409, "level_required", $"Avataren kræver level {avatar.Level}.");
            foreach (var requirement in avatar.Requirements ?? [])
            {
                var matching = progress.Where(item => item.AvatarId == id && item.RequirementId == requirement.Id).ToArray();
                if (matching.Length != 1 || !matching[0].IsMet)
                    return Mutation.Fail(409, "avatar_requirement_not_met",
                        $"Kravet er ikke opfyldt: {requirement.Description} ({matching.FirstOrDefault()?.Current ?? 0}/{requirement.RequiredCount}).");
            }
            if (profile.Gold < avatar.Cost)
                return Mutation.Fail(409, "insufficient_gold", $"Du mangler {avatar.Cost - profile.Gold} guld.");

            profile.Gold -= avatar.Cost;
            profile.Avatars.Add(new AvatarOwnership { UserSub = profile.UserSub, AvatarId = id });
            profile.SelectedAvatarId = id;
            return Mutation.Changed;
        }, ct);
    }

    [HttpPut("selection")]
    public Task<ActionResult<AvatarProfileDto>> Select([FromBody] AvatarSelectionRequest request, CancellationToken ct)
    {
        if (AvatarEconomy.FindAvatar(request.AvatarId) is null)
            return Task.FromResult<ActionResult<AvatarProfileDto>>(Failure(404, "avatar_not_found", "Avataren findes ikke."));

        return ExecuteAsync((profile, _) =>
        {
            if (!profile.Avatars.Any(owned => owned.AvatarId == request.AvatarId))
                return Mutation.Fail(409, "avatar_not_owned", "Lås avataren op, før du vælger den.");
            if (profile.SelectedAvatarId == request.AvatarId) return Mutation.Unchanged;
            profile.SelectedAvatarId = request.AvatarId;
            return Mutation.Changed;
        }, ct);
    }

    [HttpPost("import")]
    public Task<ActionResult<AvatarProfileDto>> ImportLegacy([FromBody] AvatarLegacyImportRequest request, CancellationToken ct)
    {
        if (request.OwnedAvatarIds is null || request.OwnedAvatarIds.Count > 128)
            return Task.FromResult<ActionResult<AvatarProfileDto>>(Failure(400, "invalid_legacy_profile", "Den gemte avatarsamling kunne ikke læses."));

        return ExecuteAsync((profile, _) =>
        {
            if (!profile.LegacyImportAvailable)
                return Mutation.Fail(409, "legacy_import_closed", "Din avatar er allerede gemt på kontoen. Genindlæs for at hente den seneste samling.");

            var requested = request.OwnedAvatarIds.ToHashSet(StringComparer.Ordinal);
            var avatars = AvatarEconomy.Avatars
                .Where(avatar => avatar.LegacyImportEligible
                    && (avatar.Id == AvatarEconomy.StarterId || requested.Contains(avatar.Id) && profile.Level >= avatar.Level))
                .ToArray();
            var maximumBalance = Math.Max(0, AvatarEconomy.InitialGold - avatars.Sum(avatar => avatar.Cost));
            profile.Gold = Math.Clamp(request.Gold, 0, Math.Min(profile.Gold, maximumBalance));
            foreach (var avatar in avatars)
                if (!profile.Avatars.Any(owned => owned.AvatarId == avatar.Id))
                    profile.Avatars.Add(new AvatarOwnership { UserSub = profile.UserSub, AvatarId = avatar.Id });
            profile.SelectedAvatarId = avatars.Any(avatar => avatar.Id == request.SelectedAvatarId)
                ? request.SelectedAvatarId!
                : AvatarEconomy.StarterId;
            return Mutation.Changed;
        }, ct);
    }

    [HttpPost("items/{id}/buy")]
    public Task<ActionResult<AvatarProfileDto>> BuyItem(string id, CancellationToken ct)
    {
        var item = AvatarEconomy.FindFurniture(id);
        if (item is null)
            return Task.FromResult<ActionResult<AvatarProfileDto>>(Failure(404, "item_not_found", "Møblet kan ikke købes her."));

        return ExecuteAsync((profile, _) =>
        {
            if (profile.Items.Any(owned => owned.ItemId == id)) return Mutation.Unchanged;
            if (profile.Level < item.Level)
                return Mutation.Fail(409, "level_required", $"Møblet kræver level {item.Level}.");
            // Badge progression is not yet account-backed. Do not authorize a
            // paid item using badge ranks supplied by a browser or demo catalog.
            if (item.RequiredBadge is not null)
                return Mutation.Fail(409, "badge_required", "Møblets badgekrav er ikke opfyldt på kontoen.");
            if (profile.Gold < item.Cost)
                return Mutation.Fail(409, "insufficient_gold", $"Du mangler {item.Cost - profile.Gold} guld.");

            profile.Gold -= item.Cost;
            profile.Items.Add(new AvatarItemOwnership { UserSub = profile.UserSub, ItemId = id });
            return Mutation.Changed;
        }, ct);
    }

    private async Task<ActionResult<AvatarProfileDto>> ExecuteAsync(
        Func<AvatarProfile, IReadOnlyList<AvatarUnlockProgressDto>, Mutation>? mutate,
        CancellationToken ct)
    {
        var subject = UserClaims.GetSub(User);
        if (subject is null || User.Identity?.IsAuthenticated != true)
            return Failure(401, "authentication_required", "Log ind for at gemme din avatar.");

        // This header binds an in-flight browser operation to the account that
        // started it. It is only a guard; the JWT remains the sole authority.
        // A token refresh/account switch must not apply an old action to the
        // newly authenticated account merely because its response is ignored.
        if (Request.Headers.TryGetValue("X-LUP-Avatar-Subject", out var expectedSubject)
            && (!Guid.TryParse(expectedSubject.ToString(), out var expected) || expected != subject.Value))
            return Failure(409, "account_changed", "Kontoen blev skiftet. Hent avatarsamlingen igen, før du fortsætter.");

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            db.ChangeTracker.Clear();
            try
            {
                var profile = await db.AvatarProfiles.Include(profile => profile.Avatars).Include(profile => profile.Items)
                    .SingleOrDefaultAsync(profile => profile.UserSub == subject.Value, ct);
                var created = profile is null;
                if (created)
                {
                    profile = NewProfile(subject.Value);
                    db.AvatarProfiles.Add(profile);
                }

                var progress = await AvatarRequirementEvaluator.EvaluateAsync(db, subject.Value, AvatarEconomy.Avatars, ct);
                var mutation = mutate?.Invoke(profile!, progress) ?? Mutation.Unchanged;
                if (mutation.Error is not null)
                    return Failure(mutation.Error.Status, mutation.Error.Code, mutation.Error.Detail);

                var closesImport = mutate is not null && profile!.LegacyImportAvailable;
                if (mutate is not null && (mutation.HasChanges || closesImport))
                {
                    profile!.LegacyImportAvailable = false;
                    profile.Revision++;
                }

                // SaveChanges uses one transaction for the wallet revision and
                // ownership inserts. An optimistic-concurrency failure rolls
                // back the entire purchase, including its gold deduction.
                if (created || mutation.HasChanges || closesImport)
                    await db.SaveChangesAsync(ct);

                return Ok(ToDto(profile!, progress));
            }
            catch (Exception error) when (IsRetryableConflict(error))
            {
                db.ChangeTracker.Clear();
                if (attempt + 1 == MaxAttempts)
                    return Failure(409, "avatar_conflict", "Samlingen blev ændret samtidig. Prøv igen.");
                await Task.Delay(15 * (attempt + 1), ct);
            }
        }

        return Failure(409, "avatar_conflict", "Samlingen blev ændret samtidig. Prøv igen.");
    }

    private static AvatarProfile NewProfile(Guid subject)
    {
        var profile = new AvatarProfile { UserSub = subject };
        profile.Avatars.Add(new AvatarOwnership { UserSub = subject, AvatarId = AvatarEconomy.StarterId });
        foreach (var id in AvatarEconomy.InitialItemIds)
            profile.Items.Add(new AvatarItemOwnership { UserSub = subject, ItemId = id });
        return profile;
    }

    private static AvatarProfileDto ToDto(AvatarProfile profile, IReadOnlyList<AvatarUnlockProgressDto> progress) => new(
        profile.SelectedAvatarId,
        profile.Avatars.Select(ownership => ownership.AvatarId).Order(StringComparer.Ordinal).ToArray(),
        profile.Gold,
        profile.Level,
        profile.Items.Select(ownership => ownership.ItemId).Order(StringComparer.Ordinal).ToArray(),
        profile.LegacyImportAvailable,
        profile.Revision,
        progress);

    private static bool IsRetryableConflict(Exception error)
    {
        if (error is DbUpdateConcurrencyException) return true;
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState is "23505" or "40001" or "40P01") return true;
            // The API does not depend on the SQLite provider. Its isolated test
            // host does; inspect only its public code without adding a runtime
            // database-provider dependency to production.
            if (current.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException"
                && current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current) is int code
                && (code is 5 or 6 || code == 19 && current.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal))) return true;
        }
        return false;
    }

    private static ObjectResult Failure(int status, string code, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = "Avatarhandlingen kunne ikke gennemføres", Detail = detail };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }

    private sealed record DomainError(int Status, string Code, string Detail);
    private sealed record Mutation(bool HasChanges, DomainError? Error)
    {
        public static Mutation Unchanged { get; } = new(false, null);
        public static Mutation Changed { get; } = new(true, null);
        public static Mutation Fail(int status, string code, string detail) => new(false, new(status, code, detail));
    }
}
