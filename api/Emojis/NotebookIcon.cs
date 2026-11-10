using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Emojis;

internal static class NotebookIcon
{
    public static async Task<bool> IsAvailableAsync(AppDbContext db, Guid? iconId, CancellationToken ct)
    {
        if (iconId is not Guid id) return true;
        if (id == Guid.Empty) return false;
        if (await db.CustomEmojis.AnyAsync(e => e.Id == id, ct)) return true;
        var character = CharacterEmojiCatalog.All.FirstOrDefault(e => e.Id == id);
        return character is not null && !await db.CustomEmojis.AnyAsync(e => e.Name == character.Name, ct);
    }
}
