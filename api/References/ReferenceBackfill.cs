using Mercantec.Tech.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.References;

/// <summary>Silent historical indexing in small batches; public graph reads never write state.</summary>
public sealed class ReferenceBackfill(IServiceScopeFactory scopes, ILogger<ReferenceBackfill> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var notebook in new[] { false, true })
            {
                for (var offset = 0; !stoppingToken.IsCancellationRequested; offset += 40)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var ids = notebook
                        ? await db.NotebookDocuments.AsNoTracking().Where(d => d.NotebookId != null).OrderBy(d => d.Id).Skip(offset).Take(40).Select(d => d.Id).ToArrayAsync(stoppingToken)
                        : await db.NotionPages.AsNoTracking().OrderBy(d => d.Id).Skip(offset).Take(40).Select(d => d.Id).ToArrayAsync(stoppingToken);
                    if (ids.Length == 0) break;
                    foreach (var id in ids)
                    {
                        var kind = notebook ? "notebook-page" : "notion-page";
                        var required = notebook ? 1 : 2;
                        if (await db.ReferenceIndexStates.CountAsync(s => s.SourceKind == kind && s.SourceId == id.ToString("D")
                                && s.Fingerprint.StartsWith(ReferenceService.IndexFingerprintPrefix), stoppingToken) >= required) continue;
                        await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                        if (notebook)
                        {
                            await db.NotebookDocuments.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Revision, d => d.Revision), stoppingToken);
                            await new ReferenceService(db).BaselineNotebookAsync(id, stoppingToken);
                        }
                        else
                        {
                            await db.NotionPages.Where(d => d.Id == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.PublicationVersion, d => d.PublicationVersion), stoppingToken);
                            await new ReferenceService(db).BaselineNotionAsync(id, stoppingToken);
                        }
                        await transaction.CommitAsync(stoppingToken);
                        db.ChangeTracker.Clear();
                    }
                    if (ids.Length < 40) break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) { logger.LogError(ex, "Historical reference indexing failed; the next startup resumes missing sources."); }
    }
}
