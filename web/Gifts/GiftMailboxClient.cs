using System.Net.Http.Json;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Activity;

namespace Mercantec.Tech.Web.Gifts;

public sealed class GiftMailboxClient(HttpClient http, AuthService auth, ActionCenterChanges? actionChanges = null, ViewAsStudentContext? viewAs = null)
{
    private string? _registeredSubject;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    public List<GiftMessage> Received { get; private set; } = [];
    public List<GiftMessage> Sent { get; private set; } = [];
    public List<GiftRecipient> Recipients { get; private set; } = [];
    public int Unread { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;

    public async Task RefreshAsync(bool recipients = false, CancellationToken ct = default)
    {
        if (!auth.IsAuthenticated || viewAs?.IsActive == true) { Clear(); return; }
        var subject = auth.Subject;
        await _refresh.WaitAsync(ct);
        try
        {
            if (subject != auth.Subject || !auth.IsAuthenticated || viewAs?.IsActive == true) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            if (_registeredSubject != subject)
            {
                using var registration = await http.PostAsync("gifts/profile", null, timeout.Token);
                await RequireSuccess(registration);
                _registeredSubject = subject;
            }
            var mailbox = await http.GetFromJsonAsync<GiftMailbox>("gifts", timeout.Token);
            var people = recipients ? await http.GetFromJsonAsync<List<GiftRecipient>>("gifts/recipients", timeout.Token) : null;
            if (subject != auth.Subject || !auth.IsAuthenticated || viewAs?.IsActive == true) return;
            Received = mailbox?.Received ?? [];
            Sent = mailbox?.Sent ?? [];
            Unread = mailbox?.Unread ?? 0;
            if (people is not null) Recipients = people;
            Error = null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { Error = "Gaver kunne ikke hentes lige nu. Prøv igen om lidt."; }
        catch (System.Text.Json.JsonException) { Error = "Gaveforbindelsen gav et uventet svar. Prøv igen om lidt."; }
        catch (HttpRequestException) { Error = "Gaveforbindelsen er ikke tilgængelig. Prøv igen, når LUPs API er startet."; }
        catch (GiftException ex) { Error = ex.Message; }
        finally { _refresh.Release(); Changed?.Invoke(); }
    }

    public async Task<GiftMessage> SendAsync(GiftDraft draft, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.PostAsJsonAsync("gifts", draft, timeout.Token);
        await RequireSuccess(response);
        var gift = await ReadGift(response, timeout.Token);
        await RefreshAsync(ct: ct);
        return gift;
    }

    public async Task<GiftMessage> GetAsync(Guid id, CancellationToken ct = default)
    {
        var subject = OwnAccount();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.GetAsync($"gifts/{id:D}", timeout.Token);
        await RequireSuccess(response, timeout.Token);
        var gift = await ReadGift(response, timeout.Token);
        CheckAccount(subject);
        if (gift.Id != id) throw new GiftException("Gaven kunne ikke bekræftes. Prøv igen.");
        return gift;
    }

    public async Task<GiftMessage> OpenAsync(Guid id, CancellationToken ct)
    {
        var subject = OwnAccount();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.PostAsync($"gifts/{id}/open", null, timeout.Token);
        await RequireSuccess(response, timeout.Token);
        var gift = await ReadGift(response, timeout.Token);
        CheckAccount(subject);
        if (gift.Id != id || gift.OpenedAtUtc is null) throw new GiftException("Gaven kunne ikke bekræftes som åbnet. Prøv igen.");
        actionChanges?.Notify();
        await RefreshAsync(ct: ct);
        CheckAccount(subject);
        return gift;
    }

    private string OwnAccount()
    {
        if (viewAs?.IsActive == true) throw new GiftException("Afslut elevvisningen for at åbne dine egne gaver.");
        var subject = auth.Subject;
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(subject)) throw new GiftException("Log ind for at hente dine gaver.");
        return subject;
    }

    private void CheckAccount(string subject)
    {
        if (!auth.IsAuthenticated || auth.Subject != subject || viewAs?.IsActive == true)
            throw new GiftException("Din konto eller visning er ændret. Åbn gaven igen fra din egen konto.");
    }

    private static async Task<GiftMessage> ReadGift(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadFromJsonAsync<GiftMessage>(ct) ?? throw new GiftException("Gaven kunne ikke bekræftes. Prøv igen."); }
        catch (System.Text.Json.JsonException) { throw new GiftException("Gaveforbindelsen gav et uventet svar. Prøv igen om lidt."); }
    }

    public void Clear()
    {
        _registeredSubject = null; Received = []; Sent = []; Recipients = []; Unread = 0; Error = null;
        Changed?.Invoke();
    }
    private static async Task RequireSuccess(HttpResponseMessage response, CancellationToken ct = default)
    {
        if (response.IsSuccessStatusCode) return;
        var code = "";
        try { code = (await response.Content.ReadFromJsonAsync<ApiError>(ct))?.Error ?? ""; }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ObjectDisposedException) { }
        throw new GiftException(code switch
        {
            "recipient_unavailable" => "Modtageren er ikke længere tilgængelig. Vælg en anden.",
            "request_conflict" => "Gaven er allerede registreret med andre valg. Luk og åbn gavevinduet igen.",
            _ when response.StatusCode == System.Net.HttpStatusCode.NotFound => "Gaven er ikke tilgængelig for din konto.",
            _ => response.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "Log ind igen for at hente dine gaver." : "Gaven kunne ikke gemmes. Prøv igen om lidt."
        });
    }
    private sealed record ApiError(string? Error);
    private sealed record GiftMailbox(List<GiftMessage> Received, List<GiftMessage> Sent, int Unread);
}
public sealed record GiftRecipient(Guid UserSub, string DisplayName, bool IsTeacher);
public sealed record GiftMessage(Guid Id, string SenderName, string RecipientName, string Kind, string Wrapping, string Message, DateTime CreatedAtUtc, DateTime? OpenedAtUtc);
public sealed record GiftDraft(Guid Id, Guid RecipientSub, string Kind, string Wrapping, string Message);
public sealed class GiftException(string message) : Exception(message);
