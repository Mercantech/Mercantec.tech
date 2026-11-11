using System.Net;
using System.Net.Http.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.ProfileNames;
using Microsoft.Extensions.DependencyInjection;

namespace Mercantec.Tech.Web.Tests;

public sealed class ProfileNameStateTests
{
    [Fact]
    public async Task Concurrent_readers_share_the_current_account_load()
    {
        using var api = new ProfileApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        using var state = NewState(context);
        var gate = api.NextRead = NewGate();
        var first = state.EnsureAsync();
        var second = state.EnsureAsync();
        try
        {
            Assert.True(state.IsLoading);
            Assert.Same(first, second);
            Assert.Equal(1, api.Reads);
        }
        finally { gate.TrySetResult(); }

        await Task.WhenAll(first, second);
        await state.EnsureAsync();
        Assert.True(state.IsLoaded);
        Assert.Equal(api.Name, state.DisplayName);
        Assert.Equal(1, api.Reads);
    }

    [Fact]
    public async Task Save_is_bound_to_the_subject_and_only_publishes_a_confirmed_name()
    {
        using var api = new ProfileApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        using var state = NewState(context);
        await state.EnsureAsync();
        var previous = state.DisplayName;
        var gate = api.NextWrite = NewGate();
        var save = state.SaveAsync("  Ny signatur  ");
        try
        {
            Assert.True(state.IsSaving);
            Assert.Equal(previous, state.DisplayName);
            Assert.False(await state.SaveAsync("Et sideløbende navn"));
            Assert.Equal(1, api.Writes);
        }
        finally { gate.TrySetResult(); }

        Assert.True(await save);
        Assert.Equal("Ny signatur", state.DisplayName);
        Assert.Equal(LearningUiTestContext.AccountId.ToString("D"), api.ExpectedSubject);
        Assert.True(await state.SaveAsync(null));
        Assert.Null(state.DisplayName);
    }

    [Fact]
    public async Task Read_errors_can_be_retried_and_a_failed_save_keeps_the_existing_name()
    {
        using var api = new ProfileApi { ReadFailure = HttpStatusCode.ServiceUnavailable };
        using var context = await LearningUiTestContext.CreateAsync(api);
        using var state = NewState(context);
        await state.EnsureAsync();
        Assert.False(state.IsLoaded);
        Assert.NotNull(state.LoadError);
        Assert.False(await state.SaveAsync("Må vente på indlæsning"));
        Assert.Equal(0, api.Writes);

        api.ReadFailure = null;
        await state.ReloadAsync();
        Assert.True(state.IsLoaded);
        Assert.Null(state.LoadError);
        api.WriteFailure = HttpStatusCode.ServiceUnavailable;
        Assert.False(await state.SaveAsync("Må ikke vises endnu"));
        Assert.Equal(api.Name, state.DisplayName);
        Assert.NotNull(state.SaveError);
        Assert.False(state.IsSaving);
    }

    [Fact]
    public async Task Teacher_preview_cancels_the_old_load_and_ignores_its_late_response()
    {
        using var api = new ProfileApi();
        using var context = await LearningUiTestContext.CreateAsync(api, teacher: true);
        using var state = NewState(context);
        await state.EnsureAsync();
        var gate = api.NextRead = NewGate();
        var previous = state.ReloadAsync();
        var oldToken = api.LastReadToken;
        try
        {
            context.ViewAs.Enter("pupil", "Valgt elev", authSub: Guid.NewGuid());
            Assert.True(oldToken.IsCancellationRequested);
            Assert.False(state.CanEdit);
            Assert.Null(state.DisplayName);
            await state.EnsureAsync();
            Assert.False(await state.SaveAsync("Må ikke gemmes i preview"));
            Assert.Equal(2, api.Reads);
            Assert.Equal(0, api.Writes);

            api.Name = "Genindlæst navn";
            context.ViewAs.Exit();
            await state.EnsureAsync();
            Assert.Equal("Genindlæst navn", state.DisplayName);
        }
        finally { gate.TrySetResult(); }

        await previous;
        Assert.Equal("Genindlæst navn", state.DisplayName);
        Assert.Equal(3, api.Reads);
    }

    [Fact]
    public async Task Logout_clears_the_name_and_a_pending_save_cannot_restore_it()
    {
        using var api = new ProfileApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        using var state = NewState(context);
        await state.EnsureAsync();
        var gate = api.NextWrite = NewGate();
        var save = state.SaveAsync("Forsinket navn");
        try
        {
            await context.Auth.LogoutLocalAsync();
            Assert.Null(state.DisplayName);
            Assert.False(state.IsLoaded);
            Assert.False(state.IsSaving);
            Assert.False(state.CanEdit);
        }
        finally { gate.TrySetResult(); }

        Assert.False(await save);
        Assert.Null(state.DisplayName);
        Assert.Null(state.SaveError);
    }

    private static ProfileNameState NewState(LearningUiTestContext context) =>
        new(context.Services.GetRequiredService<HttpClient>(), context.Auth, context.ViewAs);

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ProfileApi : HttpMessageHandler
    {
        public string? Name { get; set; } = "Min signatur";
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public string? ExpectedSubject { get; private set; }
        public CancellationToken LastReadToken { get; private set; }
        public HttpStatusCode? ReadFailure { get; set; }
        public HttpStatusCode? WriteFailure { get; set; }
        public TaskCompletionSource? NextRead { get; set; }
        public TaskCompletionSource? NextWrite { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.PathAndQuery != "/api/me/profile")
                throw new InvalidOperationException($"Unexpected profile request {request.Method} {request.RequestUri}");
            if (request.Method == HttpMethod.Get)
            {
                Reads++;
                LastReadToken = ct;
                var name = Name;
                var gate = NextRead;
                NextRead = null;
                // A response can arrive after the browser requested cancellation.
                if (gate is not null) await gate.Task;
                return ReadFailure is HttpStatusCode status ? new(status) : Reply(name);
            }
            if (request.Method != HttpMethod.Put || request.Content is null)
                throw new InvalidOperationException($"Unexpected profile method {request.Method}");

            Writes++;
            ExpectedSubject = request.Headers.TryGetValues(ProfileNameRules.SubjectHeader, out var subjects) ? subjects.Single() : null;
            var update = await request.Content.ReadFromJsonAsync<UpdateProfileNameRequest>(ct)
                ?? throw new InvalidOperationException("Missing profile update body.");
            var writeGate = NextWrite;
            NextWrite = null;
            if (writeGate is not null) await writeGate.Task;
            if (WriteFailure is HttpStatusCode failure) return new(failure);
            Name = update.DisplayName?.Trim();
            return Reply(Name);
        }

        private static HttpResponseMessage Reply(string? name) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ProfileNameDto(name))
        };
    }
}
