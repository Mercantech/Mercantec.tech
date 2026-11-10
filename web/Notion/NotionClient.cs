using System.Net.Http.Json;
using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Notion;

public sealed class NotionClient(HttpClient http)
{
    public Task<NotionCollectionView[]> Collections(CancellationToken ct, bool publishedOnly = false) => Get<NotionCollectionView[]>($"notion/collections?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionCollectionView> CreateCollection(string title, Guid? holdId, CancellationToken ct, Guid? iconEmojiId = null) =>
        Post<NotionCollectionView>("notion/collections", new CreateNotionCollection(title, holdId, iconEmojiId), ct);
    public async Task SetCollectionIcon(Guid id, Guid? iconEmojiId, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync($"notion/collections/{id}/icon", new SetNotebookIcon(iconEmojiId), ct);
        if (!response.IsSuccessStatusCode) await Read<object>(response, ct);
    }
    public Task<NotionPageSummary[]> CollectionPages(Guid collectionId, CancellationToken ct, bool publishedOnly = false) => Get<NotionPageSummary[]>($"notion/pages?collectionId={collectionId}&publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionHold[]> Holds(CancellationToken ct) => Get<NotionHold[]>("notion/holds", ct);
    public Task<NotionPageSummary[]> Pages(Guid holdId, CancellationToken ct, bool publishedOnly = false, bool legacyOnly = false) =>
        Get<NotionPageSummary[]>($"notion/pages?holdId={holdId}&publishedOnly={publishedOnly.ToString().ToLowerInvariant()}&legacyOnly={legacyOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionGraph> Graph(Guid collectionId, CancellationToken ct, bool publishedOnly = false) => Get<NotionGraph>($"notion/graph/{collectionId}?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionGraph> HoldGraph(Guid holdId, CancellationToken ct, bool publishedOnly = false) => Get<NotionGraph>($"notion/holds/{holdId}/graph?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionPageContext> Context(Guid id, CancellationToken ct, bool includeResources = true) => Get<NotionPageContext>($"notion/pages/{id}/context?includeResources={includeResources.ToString().ToLowerInvariant()}", ct);
    public Task<NotionPageContext> PublishedContext(Guid id, CancellationToken ct, bool includeResources = true) => Get<NotionPageContext>($"notion/pages/{id}/published-context?includeResources={includeResources.ToString().ToLowerInvariant()}", ct);
    public Task<NotionEditorResources> EditorResources(Guid holdId, CancellationToken ct, bool publishedOnly = false) => Get<NotionEditorResources>($"notion/holds/{holdId}/editor-resources?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionNavigationContext> Navigation(Guid holdId, CancellationToken ct, bool publishedOnly = false) => Get<NotionNavigationContext>($"notion/holds/{holdId}/navigation?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public Task<NotionGroupView[]> Groups(Guid holdId, CancellationToken ct) => Get<NotionGroupView[]>($"notion/holds/{holdId}/groups", ct);
    public Task<NotionHoldMember[]> Members(Guid holdId, CancellationToken ct) => Get<NotionHoldMember[]>($"notion/holds/{holdId}/members", ct);
    public Task<NotionStudentProfile> Student(Guid holdId, Guid memberId, CancellationToken ct) => Get<NotionStudentProfile>($"notion/holds/{holdId}/members/{memberId}", ct);
    public Task<NotionPageSummary> Create(Guid? holdId, string title, Guid? parentPageId, CancellationToken ct, Guid? collectionId = null, bool atRoot = false) =>
        Post<NotionPageSummary>("notion/pages", new CreateNotionPage(holdId, title, parentPageId, collectionId, atRoot), ct);
    public Task<NotionCollectionView> Collection(Guid id, CancellationToken ct, bool publishedOnly = false) =>
        Get<NotionCollectionView>($"notion/collections/{id}?publishedOnly={publishedOnly.ToString().ToLowerInvariant()}", ct);
    public async Task MovePage(Guid id, MoveNotebookPage move, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync($"notion/pages/{id}/placement", move, ct);
        await Ensure(response, ct);
    }
    public Task<NotebookPageDeletionPreview> DeletePreview(Guid id, CancellationToken ct) =>
        Get<NotebookPageDeletionPreview>($"notion/pages/{id}/delete-preview", ct);
    public async Task DeletePage(Guid id, int count, CancellationToken ct)
    {
        using var response = await http.DeleteAsync($"notion/pages/{id}?expectedDescendantCount={count}", ct);
        await Ensure(response, ct);
    }
    public Task<TrashedNotebookPage[]> Trash(Guid id, bool isCollection, CancellationToken ct) =>
        Get<TrashedNotebookPage[]>($"notion/{(isCollection ? "collections" : "holds")}/{id}/trash", ct);
    public async Task RestorePage(Guid id, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync($"notion/pages/{id}/restore", new { }, ct);
        await Ensure(response, ct);
    }
    public Task<NotionPageSummary> Publish(Guid id, long expectedRevision, string expectedFingerprint, Guid[] assetIds, CancellationToken ct,
        DrawingPublicationRequest[]? drawings = null) => Post<NotionPageSummary>($"notion/pages/{id}/publish", new { expectedRevision, expectedFingerprint, assetIds, drawings }, ct);
    public Task<NotionPageSummary> Unpublish(Guid id, CancellationToken ct) => Post<NotionPageSummary>($"notion/pages/{id}/unpublish", new {}, ct);
    public Task<NotionGroupView> CreateGroup(Guid holdId, string name, Guid[] membershipIds, CancellationToken ct, Guid? groupSetId = null) => Post<NotionGroupView>($"notion/holds/{holdId}/groups", new SaveNotionGroup(name, membershipIds, GroupSetId: groupSetId), ct);
    public Task<NotionGroupView[]> CreateGroupBatch(Guid holdId, SaveNotionGroup[] groups, CancellationToken ct, Guid? groupSetId = null, Guid? requestId = null) => Post<NotionGroupView[]>($"notion/holds/{holdId}/groups/batch", new SaveNotionGroupBatch(groups, groupSetId, requestId), ct);

    public async Task<NotionGroupView> UpdateGroup(Guid holdId, Guid id, string name, Guid[] membershipIds, int version, CancellationToken ct, Guid? groupSetId = null)
    {
        using var response = await http.PutAsJsonAsync($"notion/holds/{holdId}/groups/{id}", new SaveNotionGroup(name, membershipIds, version, groupSetId), ct);
        return await Read<NotionGroupView>(response, ct);
    }
    public async Task<NotionPageSummary> Rename(Guid id, string title, CancellationToken ct)
    {
        using var response = await http.PatchAsJsonAsync($"notion/pages/{id}", new { title }, ct);
        return await Read<NotionPageSummary>(response, ct);
    }
    private async Task<T> Get<T>(string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);
        return await Read<T>(response, ct);
    }
    private async Task<T> Post<T>(string path, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, ct);
        return await Read<T>(response, ct);
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await Ensure(response, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new NotionException("Dokumenttjenesten gav et tomt svar.");
        ct.ThrowIfCancellationRequested();
        return value;
    }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            string? code = null;
            try { code = (await response.Content.ReadFromJsonAsync<ApiError>(ct))?.Error; } catch (System.Text.Json.JsonException) { }
            throw new NotionException(code switch
            {
                "invalid_group" => "Giv gruppen et navn, og vælg elever fra dette hold.",
                "invalid_group_set" => "Vælg et aktivt gruppesæt fra dette hold. Arkiverede sæt skal gendannes først.",
                "group_set_in_use" => "Gruppen har afleveringer og kan ikke flyttes til et andet gruppesæt.",
                "invalid_group_batch" => "Giv grupperne forskellige navne, og fordel hver elev én gang i denne runde (højst 250 elever og 100 grupper).",
                "batch_request_changed" => "Denne runde er allerede gemt med et andet indhold. Start en ny runde for at gemme en ny fordeling.",
                "group_changed" => "Gruppen er ændret af en anden. Opdatér listen, og prøv igen.",
                "draft_changed" or "stale_draft" or "publication_conflict" => "Kladden er ændret. Vent på lagring, og prøv at udgive igen.",
                "invalid_assets" => "En vedhæftning er ikke tilgængelig på denne side. Fjern den, eller upload den igen.",
                "invalid_icon" => "Ikonet er ikke længere tilgængeligt. Vælg et andet.",
                "invalid_parent" or "parent_cycle" or "invalid_page_position" => "Vælg en anden placering. En side kan ikke flyttes under sig selv eller en underside.",
                "page_tree_changed" => "Undersiderne er ændret. Åbn slettehandlingen igen, og kontrollér, hvad der bliver slettet.",
                "trash_expired" => "Fristen på 30 dage er udløbet.",
                "drawing_changed" => "Tegningen er ændret. Vent på lagring, og prøv at udgive igen.",
                "drawing_preview_required" => "Åbn tegningen, og vent på, at forhåndsvisningen er gemt, før du udgiver.",
                "drawing_service_unavailable" => "Tegningerne kunne ikke gemmes lige nu. Prøv igen.",
                _ => (int)response.StatusCode switch
                {
                    401 => "Log ind igen for at åbne indholdet.",
                    403 => "Du har ikke adgang til denne handling på holdet.",
                    404 => "Indholdet er ikke tilgængeligt for din konto. Siden kan være en kladde eller trukket tilbage.",
                    400 => "Kontrollér de indtastede oplysninger, og prøv igen.",
                    409 => "Indholdet er ændret. Opdatér, og prøv igen.",
                    503 => "Dokumenttjenesten er ikke tilgængelig lige nu. Prøv igen om lidt.",
                    _ => "Indholdet kunne ikke hentes. Kontrollér forbindelsen, og prøv igen."
                }
            }, code);
        }
    }
    private sealed record ApiError(string? Error);
}
public sealed class NotionException(string message, string? errorCode = null) : Exception(message)
{
    public string? ErrorCode { get; } = errorCode;
}
