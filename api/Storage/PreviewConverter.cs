using System.Diagnostics;
namespace Mercantec.Tech.Api.Storage;

/// <summary>Only sees a transient spool, without network or storage/database credentials.</summary>
public sealed class PreviewConverter(IConfiguration configuration, ILogger<PreviewConverter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var spool = configuration["PreviewWorker:SpoolPath"] ?? "/spool";
        Directory.CreateDirectory(spool);
        while (!ct.IsCancellationRequested)
        {
            foreach (var directory in Directory.EnumerateDirectories(spool))
            {
                if (!File.Exists(Path.Combine(directory, "ready")) || File.Exists(Path.Combine(directory, "done"))
                    || File.Exists(Path.Combine(directory, "error"))) continue;
                var marker = Path.Combine(directory, "processing");
                if (File.Exists(marker) && File.GetLastWriteTimeUtc(marker) > DateTime.UtcNow.AddMinutes(-15)) continue;
                try
                {
                    if (File.Exists(marker)) File.Delete(marker);
                    await using (var claim = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                    var extractText = File.Exists(Path.Combine(directory, "extract-text"));
                    var input = Directory.EnumerateFiles(directory, "input.*").Where(path => CanProcess(path, extractText)).Single();
                    if (!CanProcess(input, extractText)) throw new IOException("Unsupported conversion input");
                    if (extractText) await ExtractTextAsync(input, directory, ct);
                    else await ConvertAsync(input, directory, ct);
                    await File.WriteAllTextAsync(Path.Combine(directory, "done"), "", ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception error)
                {
                    logger.LogWarning(error, "Conversion failed");
                    if (Directory.Exists(directory))
                        await File.WriteAllTextAsync(Path.Combine(directory, "error"), "conversion_failed", ct);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    public static bool CanProcess(string input, bool extractText) => extractText
        ? new[] { ".pdf", ".doc", ".docx", ".odt", ".rtf" }.Contains(Path.GetExtension(input).ToLowerInvariant())
        : PreviewWorker.CanConvert(input);

    private static async Task ExtractTextAsync(string input, string directory, CancellationToken ct)
    {
        if (!CanProcess(input, true)) throw new IOException("Unsupported text extraction input");
        var pdf = input;
        if (!Path.GetExtension(input).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            await ConvertAsync(input, directory, ct);
            pdf = Path.Combine(directory, "input.pdf");
        }
        if (!File.Exists(pdf)) throw new IOException("Document converter produced no PDF");
        // Poppler reads a local PDF in the same networkless, credential-free conversion container.
        // Keep form-feed page delimiters so source excerpts can cite their original PDF page.
        await RunAsync("pdftotext", ["-enc", "UTF-8", "-layout", pdf, Path.Combine(directory, "text.txt")],
            directory, TimeSpan.FromMinutes(1), ct);
    }

    private static async Task ConvertAsync(string input, string directory, CancellationToken ct)
    {
            var video = PreviewWorker.IsVideo(input);
            var output = Path.Combine(directory, video ? "preview.mp4" : "input.pdf");
            if (video)
            {
                await RunAsync("ffmpeg", ["-nostdin", "-v", "error", "-protocol_whitelist", "file,pipe",
                    "-i", input, "-map", "0:v:0", "-map", "0:a:0?", "-c:v", "libx264", "-preset", "fast",
                    "-crf", "24", "-pix_fmt", "yuv420p", "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",
                    "-c:a", "aac", "-movflags", "+faststart", "-y", output], directory, TimeSpan.FromMinutes(10), ct);
            }
            else
            {
                var profile = Path.Combine(directory, "profile");
                Directory.CreateDirectory(Path.Combine(profile, "user"));
                // Highest macro security and no automatic external-link updates for untrusted documents.
                await File.WriteAllTextAsync(Path.Combine(profile, "user", "registrymodifications.xcu"),
                    """<?xml version="1.0" encoding="UTF-8"?><oor:items xmlns:oor="http://openoffice.org/2001/registry"><item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item><item oor:path="/org.openoffice.Office.Writer/Content/Update"><prop oor:name="Link" oor:op="fuse"><value>2</value></prop></item></oor:items>""", ct);
                await RunAsync("libreoffice", ["-env:UserInstallation=" + new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri,
                    "--headless", "--nologo", "--nodefault", "--norestore", "--convert-to", "pdf", "--outdir", directory, input],
                    directory, TimeSpan.FromMinutes(2), ct);
            }

    }

    private static async Task RunAsync(string executable, string[] arguments, string directory, TimeSpan timeout, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardError = true, RedirectStandardOutput = true
        };
        start.Environment.Clear();
        start.Environment["PATH"] = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin";
        start.Environment["HOME"] = directory;
        start.Environment["TMPDIR"] = directory;
        start.Environment["LANG"] = "C.UTF-8";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Converter could not start");
        // Drain output without retaining untrusted, arbitrarily long converter diagnostics.
        var output = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, ct);
        var error = process.StandardError.BaseStream.CopyToAsync(Stream.Null, ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(output, error);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        if (process.ExitCode != 0) throw new IOException("Converter failed");
    }
}
