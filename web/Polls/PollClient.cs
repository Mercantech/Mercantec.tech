using System.Net.Http.Json;
using Mercantec.Tech.Web.Activity;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Polls;

public sealed class PollClient(HttpClient http, ActionCenterChanges? actionChanges = null)
{
    public Task<List<PollHold>> Holds(CancellationToken ct = default) => Get<List<PollHold>>("polls/holds", ct);
    public Task<List<PollView>> List(CancellationToken ct = default) => Get<List<PollView>>("polls", ct);
    public Task<List<PollView>> ForHold(Guid holdId, CancellationToken ct = default) => Get<List<PollView>>($"polls?holdId={holdId:D}", ct);
    public Task<PollView> Get(Guid id, CancellationToken ct = default) => Get<PollView>($"polls/{id}", ct);
    public Task<PollView> Create(CreatePoll draft, CancellationToken ct = default) => Post("polls", draft, ct);
    public Task<PollView> State(Guid id, string state, CancellationToken ct = default) => Post($"polls/{id}/state", new PollState(state), ct);
    public Task<PollView> Answer(Guid id, PollAnswer answer, CancellationToken ct = default) => Post($"polls/{id}/answer", answer, ct);

    private async Task<T> Get<T>(string path, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.GetAsync(path, timeout.Token);
        return await Read<T>(response, timeout.Token);
    }
    private async Task<PollView> Post<T>(string path, T body, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.PostAsJsonAsync(path, body, timeout.Token);
        var result = await Read<PollView>(response, timeout.Token);
        actionChanges?.Notify();
        return result;
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(ct); } catch (System.Text.Json.JsonException) { }
            throw new PollException(error?.Error switch
            {
                "poll_closed" => "Afstemningen er lukket. Dit svar blev ikke ændret.",
                "invalid_options" => "Skriv 2–6 forskellige svarmuligheder på højst 100 tegn.",
                "invalid_poll" => "Kontrollér spørgsmål, hold og indstillinger.",
                "invalid_answer" => "Vælg et svar, eller skriv højst 280 tegn.",
                _ => (int)response.StatusCode switch
                {
                    401 => "Log ind igen for at bruge afstemninger.",
                    403 => "Din konto har ikke adgang til at styre afstemningen.",
                    404 => "Afstemningen eller holdet er ikke tilgængeligt for din konto.",
                    _ => "Afstemningen kunne ikke hentes. Prøv igen om lidt."
                }
            });
        }
        try { return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new PollException("Svaret fra LUP var tomt. Prøv igen."); }
        catch (System.Text.Json.JsonException) { throw new PollException("Afstemningsforbindelsen gav et uventet svar. Prøv igen."); }
    }
    private sealed record ApiError(string? Error);
}
public sealed class PollException(string message) : Exception(message);

public static class PollDesign
{
    public static readonly string[] Kinds = ["mood", "choice", "scale", "text"];
    public static readonly string[] MoodNames = ["Glad", "Klar", "Midt imellem", "Træt", "Presset"];
    public static string Label(string kind) => kind switch { "mood" => "Stemningstjek", "scale" => "Skala 1–5", "text" => "Kort svar", _ => "Fælles valg" };
    public static string Icon(string kind) => kind switch { "mood" => "heart", "text" => "message", "scale" => "sliders", _ => "chart" };
    public static string StateLabel(string state) => state switch { "open" => "Åben", "closed" => "Afsluttet", _ => "Kladde" };
    public static string Question(string kind) => kind switch { "mood" => "Hvordan er du landet i dag?", "scale" => "Hvor godt er du med lige nu?", "text" => "Hvad vil du gerne forstå bedre?", _ => "Hvad skal vi bruge mere tid på?" };
    public static string Color(int index) => new[] { "#f2ca69", "#91cfa4", "#a7c8e9", "#b6a6d5", "#eaa6a0", "#88c8c6" }[Math.Abs(index) % 6];
}
