using System.Text.Json;
using Bunit;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Emojis;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiBackgroundEditorTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO6YbNgAAAAASUVORK5CYII=";
    private static IBrowserFile Image(string name) => new ExtensionEmojiDraft(name, "image/png", Png).ToBrowserFile()!;
    private static readonly string Result = JsonSerializer.Serialize(new { contentType = "image/png", base64 = Png });

    [Fact]
    public async Task ProcessedImageCanBeReplacedWithTheExactOriginalWithoutAnotherInference()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var original = Image("min_figur");
        var changed = new List<IBrowserFile>();
        var busy = new List<bool>();
        var component = context.Render<EmojiBackgroundEditor>(p => p
            .Add(x => x.File, original)
            .Add(x => x.FileChanged, file => changed.Add(file))
            .Add(x => x.BusyChanged, value => busy.Add(value)));
        runtime.Module.Result.SetResult(Result);

        component.Find("button").Click();

        component.WaitForAssertion(() => Assert.Single(changed));
        Assert.NotSame(original, changed[0]);
        Assert.Equal("image/png", changed[0].ContentType);
        component.Render(p => p.Add(x => x.File, changed[0]));
        Assert.Equal("Brug original", component.Find("button").TextContent.Trim());
        component.Find("button").Click();
        Assert.Same(original, changed[1]);
        Assert.Equal([true, false], busy);
        Assert.Equal(1, runtime.Module.Calls);
        Assert.Equal("Fjern baggrund", component.Find("button").TextContent.Trim());
    }

    [Fact]
    public async Task ChoosingAnotherFileCancelsWorkAndLateResultsCannotOverwriteIt()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var busy = new List<bool>();
        var component = context.Render<EmojiBackgroundEditor>(p => p
            .Add(x => x.File, Image("foerste"))
            .Add(x => x.FileChanged, file => changed.Add(file))
            .Add(x => x.BusyChanged, value => busy.Add(value)));
        var pending = component.Find("button").ClickAsync(new MouseEventArgs());
        component.WaitForAssertion(() => Assert.Equal(1, runtime.Module.Calls));

        component.Render(p => p.Add(x => x.File, Image("anden")));
        Assert.Single(runtime.Module.Cancelled);
        runtime.Module.Result.SetResult(Result);
        await pending;

        Assert.Empty(changed);
        Assert.Equal([true, false], busy);
        Assert.Equal("Fjern baggrund", component.Find("button").TextContent.Trim());
        Assert.DoesNotContain("Baggrunden er fjernet", component.Markup);
    }

    [Fact]
    public async Task StopDiscardsLateOutputAndKeepsOriginalAvailableForUpload()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var component = context.Render<EmojiBackgroundEditor>(p => p
            .Add(x => x.File, Image("original"))
            .Add(x => x.FileChanged, file => changed.Add(file)));
        var pending = component.Find("button").ClickAsync(new MouseEventArgs());
        component.WaitForAssertion(() => Assert.Equal("Stop", component.Find("button").TextContent.Trim()));

        component.Find("button").Click();
        runtime.Module.Result.SetResult(Result);
        await pending;

        Assert.Single(runtime.Module.Cancelled);
        Assert.Empty(changed);
        Assert.Contains("Behandlingen er stoppet", component.Markup);
        Assert.False(component.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public async Task RuntimeFailureKeepsTheDraftAndDoesNotReplaceTheImage()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var component = context.Render<EmojiBackgroundEditor>(p => p
            .Add(x => x.File, Image("original"))
            .Add(x => x.FileChanged, file => changed.Add(file)));
        var source = component.Find("img").GetAttribute("src");
        runtime.Module.Result.SetException(new JSException("Inference failed"));

        component.Find("button").Click();

        Assert.Empty(changed);
        Assert.Equal(source, component.Find("img").GetAttribute("src"));
        Assert.Contains("Originalbilledet er bevaret", component.Find("[role=alert]").TextContent);
        Assert.False(component.Find("button").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("image/gif", "R0lGODlh")]
    [InlineData("image/png", "not-base64!")]
    public async Task InvalidResultsCannotReplaceTheUploadDraft(string contentType, string base64)
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var component = context.Render<EmojiBackgroundEditor>(p => p
            .Add(x => x.File, Image("original"))
            .Add(x => x.FileChanged, file => changed.Add(file)));
        runtime.Module.Result.SetResult(JsonSerializer.Serialize(new { contentType, base64 }));

        component.Find("button").Click();

        Assert.Empty(changed);
        Assert.Contains("Originalbilledet er bevaret", component.Find("[role=alert]").TextContent);
    }

    [Fact]
    public async Task DisposingCancelsInferenceAndLateOutputCannotNotifyTheParent()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var busy = new List<bool>();
        var component = context.Render<EmojiBackgroundEditor>(p => p
            .Add(x => x.File, Image("original"))
            .Add(x => x.FileChanged, file => changed.Add(file))
            .Add(x => x.BusyChanged, value => busy.Add(value)));
        var pending = component.Find("button").ClickAsync(new MouseEventArgs());
        component.WaitForAssertion(() => Assert.Equal(1, runtime.Module.Calls));

        await component.Instance.DisposeAsync();
        runtime.Module.Result.SetResult(Result);
        await pending;

        Assert.Single(runtime.Module.Cancelled);
        Assert.Empty(changed);
        Assert.Equal([true], busy);
    }

    [Fact]
    public async Task CroppingBlocksUploadUntilAppliedAndCanBeFollowedByBackgroundRemoval()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var original = Image("original");
        var changed = new List<IBrowserFile>();
        var busy = new List<bool>();
        var component = context.Render<EmojiBackgroundEditor>(p => p.Add(x => x.File, original)
            .Add(x => x.FileChanged, file => changed.Add(file)).Add(x => x.BusyChanged, state => busy.Add(state)));

        Button(component, "Beskær og zoom").Click();
        component.WaitForAssertion(() => Assert.False(Button(component, "Brug beskæring").HasAttribute("disabled")));
        Assert.Equal([true], busy);
        runtime.Crop.Result.SetResult(JsonSerializer.Serialize(new { changed = true, contentType = "image/png", base64 = Png }));
        Button(component, "Brug beskæring").Click();
        component.WaitForAssertion(() => Assert.Single(changed));
        Assert.Equal([true, false], busy);
        Assert.Contains(17, runtime.Crop.Destroyed);
        component.Render(p => p.Add(x => x.File, changed[0]));
        runtime.Module.Result.SetResult(Result);
        Button(component, "Fjern baggrund").Click();
        component.WaitForAssertion(() => Assert.Equal(2, changed.Count));
        Assert.Equal(1, runtime.Module.Calls);
        Button(component, "Brug original").Click();
        Assert.Same(original, changed[2]);
    }

    [Fact]
    public async Task CancellingCropKeepsTheCurrentImageAndReleasesParentBusyState()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var busy = new List<bool>();
        var component = context.Render<EmojiBackgroundEditor>(p => p.Add(x => x.File, Image("original"))
            .Add(x => x.FileChanged, file => changed.Add(file)).Add(x => x.BusyChanged, state => busy.Add(state)));
        var preview = component.Find("img").GetAttribute("src");
        Button(component, "Beskær og zoom").Click();
        component.WaitForAssertion(() => Assert.Equal(1, runtime.Crop.Mounts));
        Button(component, "Annuller beskæring").Click();
        Assert.Empty(changed);
        Assert.Equal(preview, component.Find("img").GetAttribute("src"));
        Assert.Equal([true, false], busy);
        Assert.Single(runtime.Crop.Destroyed);
    }

    [Fact]
    public async Task ANewFileDiscardsAnInFlightCropAndLateBytesCannotReplaceIt()
    {
        await using var context = new BunitContext();
        var runtime = new BackgroundRuntime();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        var changed = new List<IBrowserFile>();
        var component = context.Render<EmojiBackgroundEditor>(p => p.Add(x => x.File, Image("original"))
            .Add(x => x.FileChanged, file => changed.Add(file)));
        Button(component, "Beskær og zoom").Click();
        component.WaitForAssertion(() => Assert.False(Button(component, "Brug beskæring").HasAttribute("disabled")));
        var pending = Button(component, "Brug beskæring").ClickAsync(new MouseEventArgs());
        component.Render(p => p.Add(x => x.File, Image("anden")));
        runtime.Crop.Result.SetResult(JsonSerializer.Serialize(new { changed = true, contentType = "image/png", base64 = Png }));
        await pending;
        Assert.Empty(changed);
        Assert.Empty(component.FindAll(".emoji-crop-panel"));
        Assert.Single(runtime.Crop.Destroyed);
    }

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<EmojiBackgroundEditor> component, string text) =>
        component.FindAll("button").Single(button => button.TextContent.Trim() == text);

    private sealed class BackgroundRuntime : IJSRuntime
    {
        public BackgroundModule Module { get; } = new();
        public CropModule Crop { get; } = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Assert.Equal("import", identifier);
            if (Equals(args![0], "/js/emoji-crop-adapter.js")) return ValueTask.FromResult((TValue)(object)Crop);
            Assert.Equal("/js/emoji-background.js", args[0]);
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }

    private sealed class CropModule : IJSObjectReference
    {
        public TaskCompletionSource<string> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Destroyed { get; } = [];
        public int Mounts { get; private set; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "mount") { Mounts++; return (TValue)(object)17; }
            if (identifier == "destroy") { Destroyed.Add((int)args![0]!); return default!; }
            Assert.Equal("result", identifier);
            return JsonSerializer.Deserialize<TValue>(await Result.Task, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
    }

    private sealed class BackgroundModule : IJSObjectReference
    {
        public TaskCompletionSource<string> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Cancelled { get; } = [];
        public int Calls { get; private set; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "cancel") { Cancelled.Add((string)args![0]!); return default!; }
            Assert.Equal("removeBackground", identifier);
            Assert.StartsWith("data:image/png;base64,", (string)args![0]!);
            Calls++;
            return JsonSerializer.Deserialize<TValue>(await Result.Task, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
    }
}
