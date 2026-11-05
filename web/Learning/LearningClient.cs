using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components.Forms;

namespace Mercantec.Tech.Web.Learning;

public sealed class LearningClient(HttpClient http)
{
    public const long MaxFileBytes = 100 * 1024 * 1024;

    public Task<LearningHold[]> Holds(CancellationToken ct) => Get<LearningHold[]>("quests/holds", ct);
    public Task<LearningMember[]> Members(Guid holdId, CancellationToken ct) => Get<LearningMember[]>($"quests/members?holdId={holdId}", ct);
    public Task<QuestView[]> Quests(Guid holdId, CancellationToken ct) => Get<QuestView[]>($"quests?holdId={holdId}", ct);
    public Task<QuestView> CreateQuest(SaveQuest body, CancellationToken ct) => Send<QuestView>(HttpMethod.Post, "quests", body, ct);
    public Task<QuestView> UpdateQuest(Guid id, SaveQuest body, CancellationToken ct) => Send<QuestView>(HttpMethod.Put, $"quests/{id}", body, ct);
    public Task<LearningGroupView[]> Groups(Guid holdId, CancellationToken ct) => Get<LearningGroupView[]>($"groups?holdId={holdId}", ct);
    public Task<LearningGroupView> SaveGroup(SaveLearningGroup body, CancellationToken ct) => Send<LearningGroupView>(HttpMethod.Post, "groups", body, ct);
    public Task<LearningGroupView> UpdateGroup(Guid id, SaveLearningGroup body, CancellationToken ct) => Send<LearningGroupView>(HttpMethod.Put, $"groups/{id}", body, ct);
    public Task<SubmissionView[]> Submissions(Guid questId, CancellationToken ct) => Get<SubmissionView[]>($"submissions?questId={questId}", ct);
    public Task<SubmissionView> Submission(Guid id, CancellationToken ct) => Get<SubmissionView>($"submissions/{id}", ct);
    public Task<SubmissionView> Start(StartSubmission body, CancellationToken ct) => Send<SubmissionView>(HttpMethod.Post, "submissions", body, ct);
    public Task<SubmissionView> SaveDraft(Guid id, SaveSubmissionDraft body, CancellationToken ct) => Send<SubmissionView>(HttpMethod.Put, $"submissions/{id}/draft", body, ct);
    public Task<SubmissionView> Submit(Guid id, SubmitSubmission body, CancellationToken ct) => Send<SubmissionView>(HttpMethod.Post, $"submissions/{id}/submit", body, ct);
    public Task<SubmissionView> Review(Guid id, ReviewSubmission body, CancellationToken ct) => Send<SubmissionView>(HttpMethod.Post, $"submissions/{id}/feedback", body, ct);
    public Task<CourseMaterialView[]> Materials(Guid holdId, CancellationToken ct) => Get<CourseMaterialView[]>($"materials?holdId={holdId}", ct);
    public Task<CourseMaterialView> AddMaterial(CreateCourseMaterial body, CancellationToken ct) => Send<CourseMaterialView>(HttpMethod.Post, "materials", body, ct);
    public Task<CourseMaterialView> UpdateMaterial(Guid id, UpdateCourseMaterial body, CancellationToken ct) => Send<CourseMaterialView>(HttpMethod.Put, $"materials/{id}", body, ct);
    public Task<FilePreview> Preview(Guid id, CancellationToken ct) => Get<FilePreview>($"files/{id}/preview", ct);
    public Task<FileTextPreview> Text(Guid id, CancellationToken ct) => Get<FileTextPreview>($"files/{id}/text", ct);
    public Task<FileArchivePreview> Archive(Guid id, CancellationToken ct) => Get<FileArchivePreview>($"files/{id}/archive", ct);
    public Task<FileWebPreview> PrepareWebPreview(Guid id, CancellationToken ct) => Send<FileWebPreview>(HttpMethod.Post, $"files/{id}/web-preview", new { }, ct);

    public string FileUrl(string url) => new Uri(http.BaseAddress!, url).AbsoluteUri;

    public async Task<UploadedFile> Upload(IBrowserFile file, Guid holdId, CancellationToken ct)
    {
        if (file.Size > MaxFileBytes)
            throw new LearningException("Filen er større end den aktuelle grænse på 100 MiB.");
        await using var stream = file.OpenReadStream(MaxFileBytes, ct);
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", file.Name);
        content.Add(new StringContent(holdId.ToString()), "holdId");
        using var request = new HttpRequestMessage(HttpMethod.Post, "files") { Content = content };
        using var response = await http.SendAsync(request, ct);
        return await Read<UploadedFile>(response, ct);
    }

    private async Task<T> Get<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        return await Read<T>(response, ct);
    }

    private async Task<T> Send<T>(HttpMethod method, string url, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, ct);
        return await Read<T>(response, ct);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(ct); }
            catch (System.Text.Json.JsonException) { }
            var message = (int)response.StatusCode switch
            {
                401 => "Log ind igen for at åbne dine materialer og afleveringer.",
                403 => "Din konto har ikke adgang til denne handling.",
                404 => "Indholdet er ikke tilgængeligt for din konto.",
                409 => "Indholdet er ændret. Hent den seneste version, før du fortsætter.",
                413 => "Filen er for stor til serverens uploadgrænse.",
                503 => "Fillageret er ikke tilgængeligt lige nu.",
                _ => "Handlingen kunne ikke gemmes. Kontrollér felterne, og prøv igen."
            };
            message = error?.Error switch
            {
                "no_web_entrypoint" => "Arkivet indeholder ingen understøttet startside. Upload et komplet web-build med index.html.",
                "web_preview_disabled" or "web_preview_not_configured" => "Spilvisning kræver, at serverens særskilte spildomæne er konfigureret.",
                _ => message
            };
            throw new LearningException(message);
        }
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                ?? throw new LearningException("Serveren returnerede et tomt svar.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new LearningException("Serveren returnerede et uventet svar. Prøv igen.");
        }
    }

    private sealed record ApiError(string? Error);
}

public sealed class LearningException(string message) : Exception(message);
