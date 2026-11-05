using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Calendar;

public sealed class TeacherCalendarClient(HttpClient http)
{
    public async Task<TeacherCalendarView> MineAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var url =
            $"calendar/mine?from={Uri.EscapeDataString(from.UtcDateTime.ToString("o"))}&to={Uri.EscapeDataString(to.UtcDateTime.ToString("o"))}";
        using var response = await http.GetAsync(url, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(timeout.Token); } catch (System.Text.Json.JsonException) { }
            throw new TeacherCalendarException(error?.Error switch
            {
                "missing_email" => "Din konto mangler e-mail til kalenderkobling.",
                "mailbox_not_found" => "Outlook-postkassen blev ikke fundet for din konto.",
                "graph_unavailable" => "Azure-kalenderen kunne ikke hentes lige nu.",
                "invalid_range" or "range_too_large" => "Ugyldigt datointerval for kalenderen.",
                _ => (int)response.StatusCode switch
                {
                    401 => "Log ind igen for at se din kalender.",
                    403 => "Kun undervisere kan se Outlook-kalenderen her.",
                    _ => "Kalenderen kunne ikke hentes. Prøv igen om lidt."
                }
            });
        }

        return await response.Content.ReadFromJsonAsync<TeacherCalendarView>(timeout.Token)
            ?? throw new TeacherCalendarException("Tomt svar fra kalender-API.");
    }

    private sealed record ApiError(string? Error, string? Message);
}

public sealed class TeacherCalendarException(string message) : Exception(message);
