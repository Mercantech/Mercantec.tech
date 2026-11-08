using System.Text;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Ai.EditorRuns;

/// <summary>Private file bytes are staged by the storage worker; conversion runs in the networkless converter.</summary>
public sealed class EditorAiFileTextWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<EditorAiFileTextWorker> logger) : BackgroundService
{
    public static bool IsText(string name) => FileTypePolicy.Classify(name)?.Kind == "text";
    public static bool CanExtract(string name) => IsText(name) || new[] { ".pdf", ".doc", ".docx", ".odt", ".rtf" }.Contains(Path.GetExtension(name).ToLowerInvariant());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { if (await ProcessNextAsync(stoppingToken)) continue; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Teaching text extraction failed"); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        if (!storage.IsConfigured) return false;
        var pending = await db.EditorAiFileTexts.Where(x => x.State == "pending" || x.State == "processing")
            .OrderBy(x => x.State == "pending" ? 0 : 1).ThenBy(x => x.FileId).Take(20).ToArrayAsync(ct);
        var job = pending.FirstOrDefault(x => x.State == "pending" || x.UpdatedAtUtc < DateTimeOffset.UtcNow.AddMinutes(-5));
        if (job is null) return false;
        var now = DateTimeOffset.UtcNow;
        var claimed = await db.EditorAiFileTexts.Where(x => x.FileId == job.FileId && x.UpdatedAtUtc == job.UpdatedAtUtc)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.State, "processing").SetProperty(p => p.UpdatedAtUtc, now), ct);
        if (claimed == 0) return true;
        await db.Entry(job).ReloadAsync(ct);
        var asset = await db.FileAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.FileId, ct);
        var spool = configuration["PreviewWorker:SpoolPath"] ?? "/spool";
        string? directory = null;
        try
        {
            if (asset is null) return true;
            if (!CanExtract(asset.OriginalName)) { job.State = "unsupported"; job.Error = "Filtypen har ikke et læsbart tekstlag."; }
            else if (asset.SizeBytes > EditorAiText.MaxFileBytes) { job.State = "unsupported"; job.Error = "Filen er større end grænsen for tekstudtræk (32 MiB)."; }
            else if (IsText(asset.OriginalName))
            {
                await using var source = await storage.GetAsync(asset.Key, ct) ?? throw new IOException("Original missing");
                job.Text = await ReadTextAsync(source.Content, ct);
                job.State = job.Text.Trim().Length > 0 ? "ready" : "unsupported";
                if (job.State == "unsupported") job.Error = "Filen indeholder ingen læsbar tekst.";
            }
            else
            {
                directory = Path.Combine(spool, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                var input = Path.Combine(directory, "input" + Path.GetExtension(asset.OriginalName).ToLowerInvariant());
                await using (var source = await storage.GetAsync(asset.Key, ct) ?? throw new IOException("Original missing"))
                await using (var target = new FileStream(input, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920]; var copied = 0L; int read;
                    while ((read = await source.Content.ReadAsync(buffer, ct)) > 0)
                    {
                        copied += read;
                        if (copied > EditorAiText.MaxFileBytes) throw new InvalidDataException("file_too_large");
                        await target.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                }
                await File.WriteAllTextAsync(Path.Combine(directory, "extract-text"), "text-v1", ct);
                await File.WriteAllTextAsync(Path.Combine(directory, "ready"), "", ct);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromMinutes(3));
                while (!File.Exists(Path.Combine(directory, "done")))
                {
                    if (File.Exists(Path.Combine(directory, "error"))) throw new IOException("Text converter failed");
                    await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
                }
                await using var text = File.OpenRead(Path.Combine(directory, "text.txt"));
                job.Text = await ReadTextAsync(text, ct);
                job.State = job.Text.Trim().Length > 0 ? "ready" : "unsupported";
                if (job.State == "unsupported") job.Error = "Dokumentet har intet læsbart tekstlag. Scanninger kræver OCR og er ikke læst.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogWarning(error, "Teaching text extraction failed for file {FileId}", job.FileId);
            job.State = "failed"; job.Text = ""; job.Error = "Teksten kunne ikke udtrækkes. Materialets beskrivelse kan stadig bruges.";
        }
        finally { if (directory is not null) PreviewSpool.TryDeleteJob(spool, directory, logger); }
        job.UpdatedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
    }
    public static async Task<string> ReadTextAsync(Stream source, CancellationToken ct)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, true, 8192, leaveOpen: true);
        var result = new StringBuilder(); var buffer = new char[8192]; int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (result.Length + read > EditorAiText.MaxExtractedChars) throw new InvalidDataException("extracted_text_too_large");
            result.Append(buffer, 0, read);
        }
        return result.ToString();
    }
}
