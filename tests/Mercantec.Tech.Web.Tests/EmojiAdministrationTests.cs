using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using Mercantec.Tech.Web.Emojis;
using EmojiPage = Mercantec.Tech.Web.Pages.Emojis.Index;

namespace Mercantec.Tech.Web.Tests;

public sealed class EmojiAdministrationTests
{
    [Fact]
    public async Task Teacher_can_manage_uploaded_and_legacy_standard_emojis_but_not_character_originals()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api, teacher: true);
        var page = context.Render<EmojiPage>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".emoji-library > li").Count));

        var character = page.FindAll(".emoji-library > li").Single(row => row.TextContent.Contains(":raymond_wave:"));
        Assert.Empty(character.QuerySelectorAll("button, form"));
        Assert.Equal(api.Character.ContentUrl, character.QuerySelector("img")!.GetAttribute("src"));
        Assert.Equal(api.Character, Assert.Single(EmojiPickerCatalog.Filter(api.Items, "Raymond vinker", "all")));

        foreach (var original in new[] { api.LegacyStandard, api.Uploaded })
        {
            var row = page.FindAll(".emoji-library > li").Single(item => item.TextContent.Contains($":{original.Name}:"));
            Assert.Equal(new[] { "Omdøb", "Slet" }, row.QuerySelectorAll("button").Select(button => button.TextContent));
            row.QuerySelectorAll("button").Single(button => button.TextContent == "Omdøb").Click();
            var renamed = original.Name + "_new";
            row = page.FindAll(".emoji-library > li").Single(item => item.TextContent.Contains($":{original.Name}:"));
            row.QuerySelector("form input")!.Change(renamed);
            row = page.FindAll(".emoji-library > li").Single(item => item.TextContent.Contains($":{original.Name}:"));
            row.QuerySelector("form")!.Submit();
            page.WaitForAssertion(() => Assert.Contains((original.Id, renamed), api.Renames));

            row = page.FindAll(".emoji-library > li").Single(item => item.TextContent.Contains($":{renamed}:"));
            row.QuerySelectorAll("button").Single(button => button.TextContent == "Slet").Click();
            row = page.FindAll(".emoji-library > li").Single(item => item.TextContent.Contains($":{renamed}:"));
            row.QuerySelectorAll("button").Single(button => button.TextContent == $"Slet :{renamed}:").Click();
            page.WaitForAssertion(() => Assert.Contains(original.Id, api.Deletes));
        }

        Assert.Equal(2, api.Renames.Count);
        Assert.Equal(2, api.Deletes.Count);
        Assert.DoesNotContain(api.Renames, item => item.Id == api.Character.Id);
        Assert.DoesNotContain(api.Character.Id, api.Deletes);
        Assert.Single(page.FindAll(".emoji-library > li"));
    }

    [Fact]
    public async Task Student_can_read_all_emojis_without_any_management_actions()
    {
        using var api = new EmojiApi();
        using var context = await LearningUiTestContext.CreateAsync(api);
        var page = context.Render<EmojiPage>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll(".emoji-library > li").Count));

        Assert.Empty(page.FindAll(".emoji-library button, .emoji-library form, .emoji-upload"));
        Assert.Empty(api.Renames);
        Assert.Empty(api.Deletes);
    }

    [Fact]
    public void Legacy_catalogue_payloads_without_the_new_field_remain_editable()
    {
        var item = JsonSerializer.Deserialize<CustomEmojiItem>("""
            {"id":"11111111-1111-4111-8111-111111111111","name":"legacy","contentType":"image/svg+xml",
            "sizeBytes":123,"updatedAtUtc":"2026-11-08T00:00:00Z","contentUrl":"/legacy.svg","isStandard":true}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(item);
        Assert.True(item.IsStandard);
        Assert.False(item.IsReadOnly);
    }

    private sealed class EmojiApi : HttpMessageHandler
    {
        public CustomEmojiItem Character { get; } = new(Guid.NewGuid(), "raymond_wave", "image/png", 200,
            DateTimeOffset.UtcNow, "/images/characters/raymond/reactions/wave.png", true, "gestures", ["Raymond", "vinker"], "Raymond vinker", true);
        public CustomEmojiItem LegacyStandard { get; } = new(Guid.NewGuid(), "legacy_wave", "image/svg+xml", 100,
            DateTimeOffset.UtcNow, "/legacy.svg", true);
        public CustomEmojiItem Uploaded { get; } = new(Guid.NewGuid(), "our_wave", "image/png", 100,
            DateTimeOffset.UtcNow, "/uploaded.png");
        public List<CustomEmojiItem> Items { get; }
        public List<(Guid Id, string Name)> Renames { get; } = [];
        public List<Guid> Deletes { get; } = [];

        public EmojiApi() => Items = [Character, LegacyStandard, Uploaded];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == "/api/emojis") return Json(Items.ToArray());
            var id = Guid.Parse(path.Split('/')[^1]);
            Assert.NotEqual(Character.Id, id);
            var item = Items.Single(item => item.Id == id);
            if (request.Method == HttpMethod.Patch)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var name = body.RootElement.GetProperty("name").GetString()!;
                var updated = item with { Name = name };
                Items[Items.IndexOf(item)] = updated;
                Renames.Add((id, name));
                return Json(updated);
            }
            if (request.Method == HttpMethod.Delete)
            {
                Items.Remove(item);
                Deletes.Add(id);
                return new(HttpStatusCode.NoContent);
            }
            throw new InvalidOperationException($"Unexpected emoji request: {request.Method} {path}");
        }

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    }
}
