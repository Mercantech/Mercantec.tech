using Bunit;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Avatars;
using Mercantec.Tech.Web.Components;
using Mercantec.Tech.Web.Demo;
using Mercantec.Tech.Web.Pages.Demo;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PublicContent = Mercantec.Tech.Web.Pages.Public.Content;

namespace Mercantec.Tech.Web.Tests;

public sealed class PublicLibraryNavigationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Badge_catalogue_and_requirements_never_present_example_awards_as_the_readers_own(bool authenticated)
    {
        using var api = new NoNetwork();
        var avatars = new PublicAvatarClient();
        using var context = await LearningUiTestContext.CreateAsync(api,
            configure: services => services.AddSingleton<IAvatarClient>(avatars).AddSingleton<DemoWorld>());
        if (!authenticated) await context.Auth.LogoutLocalAsync();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/badges");
        var catalogue = context.Render<Explore>();

        Assert.Single(catalogue.FindComponents<PublicBadgeCatalog>());
        Assert.Empty(catalogue.FindComponents<BadgeGallery>());
        Assert.Equal(authenticated, catalogue.FindAll("a[href='/progress/badges']").Count == 1);
        Assert.Contains(catalogue.FindAll(".breadcrumbs a"), link => link.GetAttribute("href") == "/library?category=badges");

        var requirements = context.Render<ContentDetail>(p => p.Add(x => x.BadgeId, "mqtt"));
        Assert.Single(requirements.FindComponents<PublicBadgeRequirements>());
        Assert.Empty(requirements.FindAll(".v2-owned, .v2-missing, .rank-dot.achieved"));
        Assert.Equal(5, requirements.FindAll(".v2-rank").Count);
        Assert.Contains(requirements.FindAll(".breadcrumbs a"), link => link.GetAttribute("href") == "/library?category=badges");
        Assert.Equal(0, avatars.Reads);
    }

    [Theory]
    [InlineData("iot", null, null)]
    [InlineData("iot", "mqtt", null)]
    [InlineData(null, null, "sensorstation")]
    public void Public_subject_lesson_and_project_pages_have_a_library_return_link(string? subject, string? lesson, string? project)
    {
        using var context = new BunitContext();
        var page = context.Render<PublicContent>(p => p.Add(x => x.SubjectId, subject)
            .Add(x => x.LessonId, lesson).Add(x => x.ProjectId, project));
        var returnPath = project is null ? "/library?category=subjects" : "/library?category=projects";
        Assert.Contains(page.FindAll(".breadcrumbs a"), link => link.GetAttribute("href") == returnPath);
    }

    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Public catalogue must not request personal records: {request.Method} {request.RequestUri}");
    }

    private sealed class PublicAvatarClient : IAvatarClient
    {
        public int Reads { get; private set; }

        public Task<AvatarProfileDto> GetAsync(CancellationToken ct = default, string? expectedSubject = null)
        {
            Reads++;
            return Task.FromResult(new AvatarProfileDto(AvatarEconomy.StarterId, [AvatarEconomy.StarterId],
                AvatarEconomy.InitialGold, AvatarEconomy.InitialLevel, AvatarEconomy.InitialItemIds, true, 0));
        }

        private static Task<AvatarProfileDto> RejectMutation() => throw new InvalidOperationException("Public catalogue must not change an avatar profile.");
        public Task<AvatarProfileDto> UnlockAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => RejectMutation();
        public Task<AvatarProfileDto> SelectAsync(string avatarId, CancellationToken ct = default, string? expectedSubject = null) => RejectMutation();
        public Task<AvatarProfileDto> ImportLegacyAsync(AvatarLegacyImportRequest request, CancellationToken ct = default, string? expectedSubject = null) => RejectMutation();
        public Task<AvatarProfileDto> BuyItemAsync(string itemId, CancellationToken ct = default, string? expectedSubject = null) => RejectMutation();
    }
}
