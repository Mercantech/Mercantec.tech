using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Learning;

public sealed class TeachingCoursesClient(HttpClient http)
{
    public Task<TeachingCourseWorkspace> List(Guid holdId, bool publishedOnly, CancellationToken ct) =>
        Get<TeachingCourseWorkspace>($"holds/{holdId}/teaching-courses?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<TeachingCourseResourceView[]> Catalog(Guid holdId, CancellationToken ct) => Get<TeachingCourseResourceView[]>($"holds/{holdId}/teaching-courses/catalog", ct);
    public Task<TeachingCourseView> Create(Guid holdId, SaveTeachingCourse body, CancellationToken ct) => Send<TeachingCourseView>(HttpMethod.Post, $"holds/{holdId}/teaching-courses", body, ct);
    public Task<TeachingCourseView> Update(Guid holdId, Guid id, SaveTeachingCourse body, CancellationToken ct) => Send<TeachingCourseView>(HttpMethod.Put, $"holds/{holdId}/teaching-courses/{id}", body, ct);
    public Task<TeachingCourseView> SaveResources(Guid holdId, Guid id, SaveTeachingCourseResources body, CancellationToken ct) => Send<TeachingCourseView>(HttpMethod.Put, $"holds/{holdId}/teaching-courses/{id}/resources", body, ct);
    public Task<TeachingCoursePageCreated> CreatePage(Guid holdId, Guid id, CreateTeachingCoursePage body, CancellationToken ct) => Send<TeachingCoursePageCreated>(HttpMethod.Post, $"holds/{holdId}/teaching-courses/{id}/pages", body, ct);
    private async Task<T> Get<T>(string url, CancellationToken ct)
    { using var response = await http.GetAsync(url, ct); return await Read<T>(response, ct); }
    private async Task<T> Send<T>(HttpMethod method, string url, object body, CancellationToken ct)
    { using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) }; using var response = await http.SendAsync(request, ct); return await Read<T>(response, ct); }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? code = null;
            try { code = (await response.Content.ReadFromJsonAsync<ApiError>(ct))?.Error; } catch (System.Text.Json.JsonException) { }
            throw new LearningException(code switch
            {
                "teaching_course_changed" => "Forløbet er ændret af en anden. Opdatér, før du gemmer igen.",
                "resource_already_assigned" => "Noget af indholdet er allerede knyttet til et andet forløb. Opdatér listen, og vælg igen.",
                "teaching_course_archived" => "Gendan forløbet fra arkivet, før du ændrer indholdet.",
                "invalid_teaching_course" => "Angiv en titel og en gyldig periode. Slutdato må ikke være før startdato.",
                "invalid_course_resources" => "Vælg indhold fra det samme hold. Opdatér listen, og prøv igen.",
                _ => response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Log ind igen for at åbne forløbet.",
                    System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden => "Forløbet er ikke tilgængeligt for din konto.",
                    _ => "Forløbet kunne ikke gemmes. Kontrollér oplysningerne, og prøv igen."
                }
            });
        }
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new LearningException("Serveren returnerede et tomt forløb.");
    }
    private sealed record ApiError(string? Error);
}
