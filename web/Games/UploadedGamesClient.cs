using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Microsoft.AspNetCore.Components.Forms;

namespace Mercantec.Tech.Web.Games;

public sealed class UploadedGamesClient(HttpClient http, AuthService auth)
{
    public const long MaxBytes = 100 * 1024 * 1024;
    public async Task<UploadedGameView[]> ListAsync(CancellationToken ct = default)
    {
        var subject = Subject();
        using var response = await http.GetAsync("me/games", ct);
        var games = await Read<UploadedGameView[]>(response, ct);
        CheckAccount(subject);
        return games;
    }

    public async Task<UploadedGameView> UploadAsync(IBrowserFile file, string title, Guid requestId, CancellationToken ct)
    {
        var subject = Subject();
        if (file.Size > MaxBytes) throw new UploadedGameException("Spillet må højst fylde 100 MiB.");
        await using var stream = file.OpenReadStream(MaxBytes, ct);
        using var form = new MultipartFormDataContent();
        using var body = new StreamContent(stream);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(body, "file", file.Name); form.Add(new StringContent(title), "title");
        form.Add(new StringContent(requestId.ToString("D")), "requestId");
        using var request = new HttpRequestMessage(HttpMethod.Post, "me/games") { Content = form };
        request.Headers.Add("X-LUP-Game-Subject", subject);
        using var response = await http.SendAsync(request, ct);
        var game = await Read<UploadedGameView>(response, ct);
        CheckAccount(subject);
        return game;
    }

    private string Subject() => auth.IsAuthenticated && !string.IsNullOrEmpty(auth.Subject)
        ? auth.Subject : throw new UploadedGameException("Log ind for at uploade dit spil.");
    private void CheckAccount(string subject)
    {
        if (!auth.IsAuthenticated || auth.Subject != subject) throw new UploadedGameException("Kontoen blev skiftet. Hent dine spil igen.");
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(ct); } catch (System.Text.Json.JsonException) { }
            throw new UploadedGameException(error?.Error switch
            {
                "account_changed" => "Kontoen blev skiftet. Åbn siden igen fra din egen konto.",
                "unsupported_game_format" => "Vælg et browser-spil som HTML-fil eller ZIP med en startside.",
                "no_web_entrypoint" => "ZIP-filen mangler en startside. Eksportér et komplet web-build med index.html.",
                "unsafe_archive_path" or "duplicate_archive_path" => "ZIP-filen har en ugyldig mappestruktur. Eksportér spillet igen.",
                "file_too_large" or "expanded_build_too_large" => "Spillet er for stort. Prøv en mindre eksport.",
                "request_conflict" => "Denne upload er allerede gemt med andre valg. Vælg filen igen for at starte en ny upload.",
                "object_storage_disabled" => "Spilupload er ikke tilgængelig lige nu. Prøv igen senere.",
                _ => "Spillet kunne ikke gemmes. Dine valg er bevaret, så du kan prøve igen."
            });
        }
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new UploadedGameException("Serveren gav et tomt svar.");
    }
    private sealed record ApiError(string? Error);
}

public sealed class UploadedGameException(string message) : Exception(message);
