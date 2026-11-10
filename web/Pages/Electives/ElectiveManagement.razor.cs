using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Electives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Electives;

public partial class ElectiveManagement
{
    [Parameter, EditorRequired] public ElectiveView[] Items { get; set; } = [];
    [Parameter] public ElectiveView? InitialTemplate { get; set; }
    [Parameter] public EventCallback<ElectiveView> OnSaved { get; set; }
    [Parameter] public EventCallback OnRefresh { get; set; }

    private static readonly string[] OfferingStatuses = ["draft", "open", "closed", "archived"];
    private readonly CancellationTokenSource _lifetime = new();
    private Draft? _draft;
    private ElectiveView? _roster;
    private ElectiveParticipant[] _participants = [];
    private ElementReference _titleInput, _rosterHeading;
    private Guid? _lastTemplate;
    private string _filter = "", _search = "";
    private string? _error;
    private bool _busy, _dirty, _focus;

    private IEnumerable<ElectiveView> Templates => Items.Where(e => e.Status == "template").OrderBy(e => e.Title);

    private IEnumerable<ElectiveView> FilteredItems => Items
        .Where(e => (_filter.Length == 0 || e.Status == _filter)
            && e.Title.Contains(_search.Trim(), StringComparison.CurrentCultureIgnoreCase))
        .OrderBy(e => e.Status switch { "draft" => 0, "open" or "closed" => 1, "template" => 2, _ => 3 })
        .ThenByDescending(e => e.StartsOn).ThenBy(e => e.Title);

    protected override void OnParametersSet()
    {
        if (InitialTemplate?.Id == _lastTemplate)
            return;

        _lastTemplate = InitialTemplate?.Id;
        if (InitialTemplate is { } template && !_dirty)
        {
            _draft = new();
            _draft.ApplyTemplate(template);
            _focus = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focus)
            return;

        _focus = false;
        if (_draft is not null)
            await _titleInput.FocusAsync();
        else if (_roster is not null)
            await _rosterHeading.FocusAsync();
    }

    private async Task CheckNavigation(LocationChangingContext context)
    {
        if (!await CanDiscardDraft())
            context.PreventNavigation();
    }

    private ValueTask<bool> CanDiscardDraft() => _dirty
        ? Js.InvokeAsync<bool>("confirm", "Vil du kassere dine ændringer til valgfaget?")
        : ValueTask.FromResult(true);

    private void MarkDirty() => _dirty = true;

    private async Task Refresh()
    {
        if (await CanDiscardDraft())
        {
            _dirty = false;
            await OnRefresh.InvokeAsync();
        }
    }

    private async Task OpenEditor(Draft draft)
    {
        if (!await CanDiscardDraft())
            return;

        CloseRoster();
        _draft = draft;
        _dirty = false;
        _focus = true;
        _error = null;
    }

    private Task NewTemplate() => OpenEditor(new() { Status = "template" });

    private async Task CloseEditor()
    {
        if (await CanDiscardDraft())
        {
            _draft = null;
            _dirty = false;
            _error = null;
        }
    }

    private void CloseRoster()
    {
        _roster = null;
        _participants = [];
        _error = null;
    }

    private void PrefillTemplate(ChangeEventArgs args)
    {
        if (_draft is not { Id: null } draft)
            return;

        if (Guid.TryParse(args.Value?.ToString(), out var id) && Templates.FirstOrDefault(e => e.Id == id) is { } template)
            draft.ApplyTemplate(template);
        else
            draft.TemplateId = null;
        _dirty = true;
    }

    private Task Save()
    {
        if (_draft is not { } draft)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            _error = "Skriv en titel på valgfaget.";
            return Task.CompletedTask;
        }
        if (draft.EndsOn < draft.StartsOn || draft.RegistrationClosesOn < draft.RegistrationOpensOn)
        {
            _error = "En slutdato kan tidligst ligge på startdatoen.";
            return Task.CompletedTask;
        }

        return Run(async ct =>
        {
            var saved = await Client.Save(draft.Id, draft.ToRequest(), ct);
            if (!ct.IsCancellationRequested)
            {
                _draft = null;
                _dirty = false;
                await OnSaved.InvokeAsync(saved);
            }
        }, "Valgfaget kunne ikke gemmes. Prøv igen.");
    }

    private async Task ShowParticipants(ElectiveView item)
    {
        if (_busy || !await CanDiscardDraft())
            return;

        _draft = null;
        _dirty = false;
        _roster = item;
        _participants = [];
        _focus = true;
        await Run(async ct =>
        {
            var participants = await Client.Participants(item.Id, ct);
            if (!ct.IsCancellationRequested)
                _participants = participants;
        }, "Deltagerlisten kunne ikke hentes. Prøv igen.");
    }

    private async Task Run(Func<CancellationToken, Task> action, string fallback)
    {
        if (_busy)
            return;

        var ct = _lifetime.Token;
        _busy = true;
        _error = null;
        try { await action(ct); }
        catch (Exception ex) when (ex is ElectiveException or HttpRequestException or OperationCanceledException)
        {
            if (!ct.IsCancellationRequested)
                _error = ex is ElectiveException ? ex.Message : fallback;
        }
        finally { _busy = false; }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private sealed class Draft
    {
        public Guid? Id { get; init; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Schedule { get; set; } = "";
        public string? SourceUrl { get; set; }
        public Guid? TemplateId { get; set; }
        public DateOnly? StartsOn { get; set; }
        public DateOnly? EndsOn { get; set; }
        public DateOnly? RegistrationOpensOn { get; set; }
        public DateOnly? RegistrationClosesOn { get; set; }
        public int? Capacity { get; set; }
        public int ReservedSeats { get; set; }
        public string Status { get; set; } = "draft";
        public int Version { get; init; }

        public static Draft ForEdit(ElectiveView item) => new()
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Schedule = item.Schedule,
            SourceUrl = item.SourceUrl,
            TemplateId = item.TemplateId,
            StartsOn = item.StartsOn,
            EndsOn = item.EndsOn,
            RegistrationOpensOn = item.RegistrationOpensOn,
            RegistrationClosesOn = item.RegistrationClosesOn,
            Capacity = item.Capacity,
            ReservedSeats = item.ReservedSeats,
            Status = item.Status,
            Version = item.Version
        };

        public void ApplyTemplate(ElectiveView template)
        {
            Title = template.Title;
            Description = template.Description;
            Schedule = template.Schedule;
            SourceUrl = template.SourceUrl;
            TemplateId = template.Id;
        }

        public SaveElective ToRequest() => new(Title, Description, Schedule, StartsOn, EndsOn,
            RegistrationOpensOn, RegistrationClosesOn, Capacity, ReservedSeats, Status, SourceUrl, TemplateId, Version);
    }
}
