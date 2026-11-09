using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Gifts;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Avatars;

public static class AvatarRequirementEvaluator
{
    public static async Task<IReadOnlyList<AvatarUnlockProgressDto>> EvaluateAsync(
        AppDbContext db, Guid subject, IReadOnlyList<AvatarUnlockDefinition> avatars, CancellationToken ct)
    {
        var result = new List<AvatarUnlockProgressDto>();
        var counts = new Dictionary<(string Kind, string Target), int>();
        foreach (var avatar in avatars)
        foreach (var requirement in avatar.Requirements ?? [])
        {
            // A new or invalid requirement must not silently turn into a free
            // unlock when a deployment has not implemented its evaluator yet.
            var supported = requirement.RequiredCount > 0
                && requirement.Kind == AvatarRequirementKinds.SentGiftsDistinctRecipients
                && GiftThemes.IsKnown(requirement.Target);
            var current = 0;
            if (supported)
            {
                var key = (requirement.Kind, requirement.Target);
                if (!counts.TryGetValue(key, out current))
                {
                    current = await db.ChristmasGifts.AsNoTracking()
                        .Where(gift => gift.SenderSub == subject && gift.Theme == requirement.Target
                            && gift.RecipientSub != Guid.Empty && gift.RecipientSub != subject)
                        .Select(gift => gift.RecipientSub).Distinct().CountAsync(ct);
                    counts[key] = current;
                }
            }
            result.Add(new(avatar.Id, requirement.Id, current, requirement.RequiredCount,
                supported && current >= requirement.RequiredCount));
        }
        return result;
    }
}
