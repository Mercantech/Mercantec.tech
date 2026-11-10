using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Electives;

public sealed class ElectiveClient(HttpClient http)
{
    public Task<ElectiveWorkspace> List(bool publishedOnly, CancellationToken ct) =>
        Send<ElectiveWorkspace>(new(HttpMethod.Get, publishedOnly ? "electives?publishedOnly=true" : "electives"), ct);

    public Task<ElectiveView> Save(Guid? id, SaveElective body, CancellationToken ct) =>
        Send<ElectiveView>(new(id is null ? HttpMethod.Post : HttpMethod.Put, id is null ? "electives" : $"electives/{id:D}")
        {
            Content = JsonContent.Create(body)
        }, ct);

    public Task<ElectiveView> Enroll(Guid id, bool hasEmployerAgreement, CancellationToken ct) =>
        Send<ElectiveView>(new(HttpMethod.Put, $"electives/{id:D}/enrollment")
        {
            Content = JsonContent.Create(new EnrollInElective(hasEmployerAgreement))
        }, ct);

    public Task<ElectiveView> Withdraw(Guid id, CancellationToken ct) =>
        Send<ElectiveView>(new(HttpMethod.Delete, $"electives/{id:D}/enrollment"), ct);

    public Task<ElectiveParticipant[]> Participants(Guid id, CancellationToken ct) =>
        Send<ElectiveParticipant[]>(new(HttpMethod.Get, $"electives/{id:D}/participants"), ct);

    private async Task<T> Send<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                ApiError? error = null;
                try { error = await response.Content.ReadFromJsonAsync<ApiError>(timeout.Token); }
                catch (Exception ex) when (ex is JsonException or NotSupportedException) { }
                throw new ElectiveException(error?.Error switch
                {
                    "elective_full" => "Holdet er fyldt.",
                    "elective_enrollment_closed" => "Tilmeldingen til holdet er lukket.",
                    "elective_withdrawal_closed" => "Afmeldingen til holdet er lukket. Kontakt underviseren.",
                    "elective_employer_agreement_required" => "Bekræft aftalen med din mester eller arbejdsgiver for at tilmelde dig.",
                    "elective_changed" => "Holdet blev ændret af en anden underviser. Luk redigeringen, opdatér oversigten, og prøv igen.",
                    "elective_capacity_below_enrollment" => "Der skal være plads til alle tilmeldte og reserverede pladser.",
                    "elective_template_in_use" => "Faget bruges på et hold og skal blive i fagkataloget.",
                    "elective_has_enrollments" => "Holdet har tilmeldte og skal forblive synligt. Luk eller arkivér holdet.",
                    "invalid_elective_template" => "Det valgte fag er ikke længere i fagkataloget.",
                    "invalid_elective" => "Kontrollér titel, datoer, antal pladser og kildelink.",
                    _ => (int)response.StatusCode switch
                    {
                        401 => "Log ind igen for at bruge valgfag.",
                        403 => "Din konto har ikke adgang til denne handling.",
                        404 => "Valgfaget er ikke længere tilgængeligt.",
                        _ => "LUP kunne ikke gennemføre handlingen. Prøv igen."
                    }
                });
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(timeout.Token)
                    ?? throw new ElectiveException("Svaret fra LUP var tomt. Prøv igen.");
            }
            catch (JsonException)
            {
                throw new ElectiveException("LUP gav et uventet svar. Prøv igen.");
            }
        }
    }

    private sealed record ApiError(string? Error);
}

public sealed class ElectiveException(string message) : Exception(message);
