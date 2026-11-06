using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Learning;

public sealed class GroupSetsClient(HttpClient http)
{
    public async Task<GroupSetView[]> List(Guid holdId, CancellationToken ct, bool includeArchived = false)
    {
        using var response = await http.GetAsync($"holds/{holdId}/group-sets?includeArchived={includeArchived.ToString().ToLowerInvariant()}", ct);
        return await Read<GroupSetView[]>(response, ct);
    }
    public async Task<GroupSetView> Create(Guid holdId, string name, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"holds/{holdId}/group-sets", new SaveGroupSet(name), ct);
        return await Read<GroupSetView>(response, ct);
    }
    public async Task<GroupSetView> Update(Guid holdId, Guid id, string name, bool archived, int version, CancellationToken ct)
    {
        using var response = await http.PutAsJsonAsync($"holds/{holdId}/group-sets/{id}", new SaveGroupSet(name, archived, version), ct);
        return await Read<GroupSetView>(response, ct);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw new LearningException(response.StatusCode switch
            {
                System.Net.HttpStatusCode.Conflict => "Gruppesættet er ændret af en anden. Opdatér, og prøv igen.",
                System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden => "Du har ikke adgang til dette gruppesæt.",
                System.Net.HttpStatusCode.Unauthorized => "Log ind igen for at åbne grupperne.",
                _ => "Gruppesættet kunne ikke gemmes. Giv det et navn, og prøv igen."
            });
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new LearningException("Serveren returnerede et tomt gruppesæt.");
    }
}
