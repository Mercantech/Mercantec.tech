using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Games;

public sealed class WordleClient(HttpClient http)
{
    private const string Path = "games/wordle/";

    public async Task<WordleDay> GetDayAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<WordleDay>(Path + "daily", ct)
        ?? throw new HttpRequestException("Dagens Wordle mangler.");

    public async Task<WordleRound> GetRoundAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<WordleRound>(Path + "round", ct)
        ?? throw new HttpRequestException("Wordle-runden mangler.");

    public async Task<WordleRound> GuessAsync(WordleGuessRequest guess, string subject, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path + "guess")
        {
            Content = JsonContent.Create(guess)
        };
        request.Headers.Add("X-LUP-Wordle-Subject", subject);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity))
            response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WordleRound>(ct);
        if (result?.Error == "account_changed")
            throw new HttpRequestException("Kontoen blev skiftet.", null, HttpStatusCode.Unauthorized);

        return result ?? throw new HttpRequestException("Resultatet af gættet mangler.");
    }
}
