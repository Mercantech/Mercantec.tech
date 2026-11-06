using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Learning;

/// <summary>Serializes first-draft creation and edits to the same quest's audience/mode.</summary>
public static class QuestWriteLock
{
    public static Task<int> AcquireAsync(AppDbContext db, Guid questId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A quest write lock requires a transaction.");
        // A self-update acquires the row's write lock on Postgres and SQLite without changing course data.
        return db.LearningQuests.Where(q => q.Id == questId)
            .ExecuteUpdateAsync(update => update.SetProperty(q => q.Published, q => q.Published), ct);
    }
}
