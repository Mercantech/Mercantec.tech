using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Notebooks;

public sealed class NotebookClient(HttpClient http)
{
    public async Task Register(CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("notebooks/profile", new { }, ct);
        await Ensure(response, ct);
    }
    public Task<NotebookInfo> Mine(CancellationToken ct) => Post<NotebookInfo>("notebooks/mine", new { }, ct);
    public async Task SetIcon(Guid notebook, Guid? iconEmojiId, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync($"notebooks/{notebook}/icon", new SetNotebookIcon(iconEmojiId), ct);
        await Ensure(response, ct);
    }
    public Task<DateOnly> Today(CancellationToken ct) => Get<DateOnly>("notebooks/today", ct);
    public Task<NotebookPageList> Pages(Guid notebook, string search, string tag, string kind, int offset, CancellationToken ct) => Get<NotebookPageList>($"notebooks/{notebook}/pages?search={E(search)}&tag={E(tag)}&kind={E(kind)}&offset={offset}&limit=100", ct);
    public Task<NotebookPageSummary[]> Shared(CancellationToken ct) => Get<NotebookPageSummary[]>("notebooks/shared", ct);
    public Task<NotebookDocumentContext> Context(Guid page, CancellationToken ct, long? knownRevision = null) => Get<NotebookDocumentContext>($"notebooks/pages/{page}/context" + (knownRevision is long revision ? $"?knownRevision={revision}" : ""), ct);
    public Task<NotebookPageSummary> Create(Guid notebook, CreateNotebookPage body, CancellationToken ct) => Post<NotebookPageSummary>($"notebooks/{notebook}/pages", body, ct);
    public Task<NotebookPageSummary> Update(Guid page, UpdateNotebookPage body, CancellationToken ct) => Patch<NotebookPageSummary>($"notebooks/pages/{page}", body, ct);
    public Task<NotionPageLink[]> Tree(Guid notebook, CancellationToken ct) => Get<NotionPageLink[]>($"notebooks/{notebook}/tree", ct);
    public async Task MovePage(Guid id, MoveNotebookPage move, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync($"notebooks/pages/{id}/placement", move, ct);
        await Ensure(response, ct);
    }
    public Task<NotebookPageDeletionPreview> DeletePreview(Guid id, CancellationToken ct) =>
        Get<NotebookPageDeletionPreview>($"notebooks/pages/{id}/delete-preview", ct);
    public async Task DeletePage(Guid id, int count, CancellationToken ct)
    {
        using var response = await http.DeleteAsync($"notebooks/pages/{id}?expectedDescendantCount={count}", ct);
        await Ensure(response, ct);
    }
    public Task<TrashedNotebookPage[]> Trash(Guid notebook, CancellationToken ct) =>
        Get<TrashedNotebookPage[]>($"notebooks/{notebook}/trash", ct);
    public async Task RestorePage(Guid id, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"notebooks/pages/{id}/restore", new { }, ct);
        await Ensure(response, ct);
    }
    public Task<NotebookPageSummary[]> Backlinks(Guid page, CancellationToken ct) => Get<NotebookPageSummary[]>($"notebooks/pages/{page}/backlinks", ct);
    public Task<NotebookGraph> Graph(Guid notebook, CancellationToken ct) => Get<NotebookGraph>($"notebooks/{notebook}/graph", ct);
    public Task<NotebookCommentView[]> Comments(Guid page, CancellationToken ct) => Get<NotebookCommentView[]>($"notebooks/pages/{page}/comments", ct);
    public Task<NotebookCommentView> Comment(Guid page, string text, CancellationToken ct) => Post<NotebookCommentView>($"notebooks/pages/{page}/comments", new AddNotebookComment(text), ct);
    public Task<NotebookShareView[]> Shares(Guid notebook, CancellationToken ct) => Get<NotebookShareView[]>($"notebooks/{notebook}/shares", ct);
    public Task<NotebookShareView> Share(Guid notebook, GrantNotebookShare body, CancellationToken ct) => Post<NotebookShareView>($"notebooks/{notebook}/shares", body, ct);
    public async Task Revoke(Guid notebook, Guid id, CancellationToken ct)
    {
        using var response = await http.DeleteAsync($"notebooks/{notebook}/shares/{id}", ct);
        await Ensure(response, ct);
    }
    public Task<NotebookUser[]> Users(string query, CancellationToken ct) => Get<NotebookUser[]>($"notebooks/users?q={E(query)}", ct);
    public Task<TeacherNotebookView[]> Teacher(string search, Guid? holdId, CancellationToken ct) => Get<TeacherNotebookView[]>($"notebooks/teacher?search={E(search)}&holdId={holdId}", ct);
    public Task<NotebookTemplateView[]> Templates(CancellationToken ct) => Get<NotebookTemplateView[]>("notebooks/templates", ct);
    public Task<NotebookTemplateView> CreateTemplate(CreateNotebookTemplate body, CancellationToken ct) => Post<NotebookTemplateView>("notebooks/templates", body, ct);
    public Task<NotebookTemplateView> UpdateTemplate(Guid id, UpdateNotebookTemplate body, CancellationToken ct) => Patch<NotebookTemplateView>($"notebooks/templates/{id}", body, ct);
    public Task<NotebookDocumentContext> TemplateContext(Guid id, CancellationToken ct) => Get<NotebookDocumentContext>($"notebooks/templates/{id}/context", ct);
    public Task<NotebookTemplateView> Activate(Guid id, ActivateNotebookTemplate body, CancellationToken ct) => Post<NotebookTemplateView>($"notebooks/templates/{id}/activate", body, ct);
    public Task<NotebookTemplateView> Archive(Guid id, CancellationToken ct) => Post<NotebookTemplateView>($"notebooks/templates/{id}/archive", new { }, ct);
    public Task<NotebookPageSummary> Entry(EnsureJournalEntry body, CancellationToken ct) => Post<NotebookPageSummary>("notebooks/entries", body, ct);
    private static string E(string value) => Uri.EscapeDataString(value);
    private async Task<T> Get<T>(string path, CancellationToken ct) { using var r = await http.GetAsync(path, ct); return await Read<T>(r, ct); }
    private async Task<T> Post<T>(string path, object body, CancellationToken ct) { using var r = await http.PostAsJsonAsync(path, body, ct); return await Read<T>(r, ct); }
    private async Task<T> Patch<T>(string path, object body, CancellationToken ct) { using var r = await http.PatchAsJsonAsync(path, body, ct); return await Read<T>(r, ct); }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await Ensure(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new NotebookException("Notesbogen gav et tomt svar.", response.StatusCode);
    }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? code = null;
        try { code = (await response.Content.ReadFromJsonAsync<ErrorBody>(ct))?.Error; } catch (System.Text.Json.JsonException) { }
        throw new NotebookException(code switch
        {
            "template_changed" or "draft_changed" or "stale_draft" => "Skabelonen er ændret. Vent på lagring, opdatér og prøv igen.",
            "invalid_parent" or "parent_cycle" or "invalid_page_position" => "Vælg en anden placering. En side kan ikke flyttes under sig selv eller en underside.",
            "page_tree_changed" => "Undersiderne er ændret. Åbn slettehandlingen igen, og kontrollér, hvad der bliver slettet.",
            "trash_expired" => "Fristen på 30 dage er udløbet.",
            "journal_in_trash" => "Der ligger allerede et indlæg for perioden i papirkurven. Du kan gendanne det derfra.",
            "invalid_icon" => "Ikonet er ikke længere tilgængeligt. Vælg et andet.",
            "recipient_unavailable" => "Personen er ikke tilgængelig i LUP. Søg og vælg en modtager igen.",
            _ => response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Log ind igen for at åbne notesbogen.",
                HttpStatusCode.Forbidden or HttpStatusCode.NotFound => "Indholdet er ikke tilgængeligt for din konto. En deling kan være trukket tilbage.",
                HttpStatusCode.Conflict => "Indholdet er ændret. Opdatér og prøv igen.",
                HttpStatusCode.BadRequest => "Kontrollér de indtastede oplysninger og prøv igen.",
                _ => "Notesbogen kunne ikke hentes. Kontrollér forbindelsen og prøv igen."
            }
        }, response.StatusCode, code);
    }
    private sealed record ErrorBody(string? Error);
}

public sealed class NotebookException(string message, HttpStatusCode status = HttpStatusCode.BadRequest, string? errorCode = null) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string? ErrorCode { get; } = errorCode;
}
