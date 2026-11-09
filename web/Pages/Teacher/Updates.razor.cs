using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Updates;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.Teacher;

public partial class Updates : IDisposable
{
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private AuthRedirect Redirect { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private PlatformUpdatesClient Client { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private bool CanRead => Auth.IsInitialized && Redirect.ShowsTeacherChrome;
    private PlatformUpdate[] _entries = [];
    private readonly Dictionary<string, string> _images = [];
    private readonly HashSet<string> _retrying = [];
    private CancellationTokenSource? _request;
    private int _generation;
    private bool _loading;
    private string? _error;
    private string? _account;
    private UpdateImage? _zoom;
    private string? _zoomTitle;
    private bool _openZoom;
    private ElementReference _dialog;
    private IJSObjectReference? _module;
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        Auth.AuthStateChanged += OnAccountChanged;
        ViewAs.Changed += OnAccountChanged;
        if (CanRead) await Load();
    }

    private void OnAccountChanged() => _ = InvokeAsync(async () =>
    {
        if (_disposed) return;
        if (!CanRead || _account != Auth.Subject) await Load();
        StateHasChanged();
    });

    private async Task Load()
    {
        _request?.Cancel();
        _request?.Dispose();
        _request = new CancellationTokenSource();
        var ct = _request.Token;
        var generation = ++_generation;
        _entries = [];
        _images.Clear();
        _retrying.Clear();
        _error = null;
        _loading = false;
        _account = CanRead ? Auth.Subject : null;
        await CloseZoom();
        if (!CanRead || _disposed) return;
        _loading = true;
        try
        {
            var entries = await Client.Get(ct);
            var images = entries.Where(entry => entry.After is not null)
                .SelectMany(entry => new[] { entry.Before, entry.After })
                .OfType<UpdateImage>()
                .DistinctBy(image => image.Id).ToArray();
            using var slots = new SemaphoreSlim(4);
            var results = await Task.WhenAll(images.Select(async image =>
            {
                await slots.WaitAsync(ct);
                try { return (image.Id, Source: await FetchImage(image.Id, ct)); }
                finally { slots.Release(); }
            }));
            if (!IsCurrent(generation)) return;
            _entries = entries.OrderByDescending(e => e.Date).ToArray();
            foreach (var (id, source) in results) if (source is not null) _images[id] = source;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is PlatformUpdatesException or HttpRequestException)
        {
            if (IsCurrent(generation)) _error = ex is PlatformUpdatesException ? ex.Message : "Opdateringerne kunne ikke hentes. Prøv igen.";
        }
        finally { if (IsCurrent(generation)) _loading = false; }
    }

    private async Task<string?> FetchImage(string id, CancellationToken ct)
    {
        try { return await Client.Image(id, ct); }
        catch (Exception ex) when (ex is PlatformUpdatesException or HttpRequestException) { return null; }
    }

    private bool IsCurrent(int generation) => !_disposed && CanRead && generation == _generation && _account == Auth.Subject;

    private async Task RetryImage(string id)
    {
        if (!CanRead || _request is null || !_retrying.Add(id)) return;
        var generation = _generation;
        try
        {
            var source = await FetchImage(id, _request.Token);
            if (IsCurrent(generation) && source is not null) _images[id] = source;
        }
        catch (OperationCanceledException) { }
        finally { if (IsCurrent(generation)) _retrying.Remove(id); }
    }

    private void Zoom(UpdateImage image, string title, string label)
    {
        if (!CanRead || !_images.ContainsKey(image.Id)) return;
        _zoom = image;
        _zoomTitle = $"{title} · {label}";
        _openZoom = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_openZoom || !CanRead || _zoom is null) return;
        _openZoom = false;
        _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "./js/platform-updates.js");
        if (_module is not null && CanRead && _zoom is not null) await _module.InvokeVoidAsync("open", _dialog);
    }

    private async Task CloseZoom()
    {
        var wasOpen = _zoom is not null;
        ClearZoom();
        if (wasOpen && _module is not null) await _module.InvokeVoidAsync("close", _dialog);
    }

    private void ClearZoom() { _zoom = null; _zoomTitle = null; _openZoom = false; }

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= OnAccountChanged;
        ViewAs.Changed -= OnAccountChanged;
        _request?.Cancel();
        _request?.Dispose();
        ClearZoom();
        _images.Clear();
        if (_module is not null) _ = _module.DisposeAsync();
    }
}
