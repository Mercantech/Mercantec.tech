using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Mercantec.Tech.Web.Emojis;

public sealed record CustomEmojiItem(Guid Id, string Name, string ContentType, long SizeBytes, DateTimeOffset UpdatedAtUtc, string ContentUrl,
    bool IsStandard = false, string Category = "custom", string[]? Keywords = null);

public sealed class EmojiClient(HttpClient http)
{
    public const int MaxBytes = 256 * 1024;
    private IReadOnlyList<CustomEmojiItem>? _cache;

    public void Invalidate() => _cache = null;

    public async Task<IReadOnlyList<CustomEmojiItem>> List(bool refresh = false, CancellationToken ct = default)
    {
        if (!refresh && _cache is not null) return _cache;
        using var response = await Send(() => http.GetAsync("emojis", ct), ct);
        var items = await response.Content.ReadFromJsonAsync<List<CustomEmojiItem>>(ct) ?? [];
        _cache = items;
        return items;
    }

    public async Task<CustomEmojiItem> Upload(string name, Stream content, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(name), "name");
        var file = new StreamContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        using var response = await Send(() => http.PostAsync("emojis", form, ct), ct);
        Invalidate();
        return await response.Content.ReadFromJsonAsync<CustomEmojiItem>(ct)
            ?? throw new EmojiException("Emoji-svaret var tomt. Prøv igen.");
    }

    public async Task<CustomEmojiItem> Rename(Guid id, string name, CancellationToken ct = default)
    {
        using var response = await Send(() => http.PatchAsJsonAsync($"emojis/{id}", new { name }, ct), ct);
        Invalidate();
        return await response.Content.ReadFromJsonAsync<CustomEmojiItem>(ct)
            ?? throw new EmojiException("Emoji-svaret var tomt. Prøv igen.");
    }

    public async Task Delete(Guid id, CancellationToken ct = default)
    {
        using var response = await Send(() => http.DeleteAsync($"emojis/{id}", ct), ct);
        Invalidate();
    }

    private static async Task<HttpResponseMessage> Send(Func<Task<HttpResponseMessage>> call, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        HttpResponseMessage response;
        try { response = await call(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new EmojiException("Emoji-biblioteket svarede ikke. Prøv igen."); }
        catch (HttpRequestException) { throw new EmojiException("Emoji-biblioteket kunne ikke nås. Prøv igen."); }
        if (response.IsSuccessStatusCode) return response;
        string? code = null;
        try { code = (await response.Content.ReadFromJsonAsync<ApiError>(ct))?.Error; } catch (System.Text.Json.JsonException) { }
        response.Dispose();
        throw new EmojiException(code switch
        {
            "invalid_name" => "Navnet skal være 2–32 tegn: små bogstaver, tal og _.",
            "name_taken" => "Navnet er allerede brugt. Vælg et andet.",
            "unsupported_file_type" => "Brug et PNG-, JPEG-, GIF- eller WebP-billede.",
            "invalid_file_signature" => "Filen matcher ikke billedtypen.",
            "invalid_filename" => "Filnavnet kan ikke bruges.",
            "file_too_large" => "Billedet må højst være 256 KB.",
            "missing_file" or "incomplete_upload" => "Vælg et billede, og prøv igen.",
            "emoji_limit" => "Biblioteket er fyldt. Slet en emoji, før du uploader en ny.",
            "object_storage_disabled" => "Fillageret er ikke klar. Prøv igen senere.",
            _ => (int)response.StatusCode switch
            {
                401 => "Log ind igen for at bruge emojis.",
                403 => "Kun undervisere kan ændre emojis.",
                404 => "Emojien findes ikke længere.",
                _ => "Emoji-biblioteket kunne ikke opdateres. Prøv igen."
            }
        });
    }

    private sealed record ApiError(string? Error);
}

public sealed class EmojiException(string message) : Exception(message);

public static class EmojiInsert
{
    public static string Append(string? current, string name, int maxLength)
    {
        var token = $":{name}:";
        var next = string.IsNullOrWhiteSpace(current) ? token : current.TrimEnd() + " " + token;
        return next.Length <= maxLength ? next : current ?? "";
    }
}
