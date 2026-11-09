using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Emojis;
using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class AiControllerEmojiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ControllerUsesDatabaseMetadataAndRechecksNamesAfterTheAnswer(bool renameDuringAnswer)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var emoji = new CustomEmoji
        {
            Id = Guid.NewGuid(), Name = "vores_raket", Key = "emojis/ours", ContentType = "image/png",
            AltText = "Lilla raket", Category = "technology", KeywordsJson = "[\"opstart\",\"rumfart\"]",
            CreatedBySub = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.CustomEmojis.Add(emoji);
        await db.SaveChangesAsync();
        var forged = Guid.NewGuid().ToString();
        using var transport = new EmojiTransport(emoji.Id.ToString(), forged, async () =>
        {
            if (!renameDuringAnswer) return;
            emoji.Name = "nyt_navn";
            await db.SaveChangesAsync();
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };
        var controller = Controller(db, http);
        var request = new EditorAiRequest("generate", "Brug vores emoji")
        {
            Emojis = [new(forged, "falsk_fra_klienten", "Må ikke komme med")]
        };

        var response = Assert.IsType<EditorAiResponse>(Assert.IsType<OkObjectResult>(await controller.Editor(request, default)).Value);
        var content = Assert.Single(response.Blocks).Content!.Value;
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal(emoji.Id.ToString(), content[1].GetProperty("props").GetProperty("emojiId").GetString());
        Assert.Equal(renameDuringAnswer ? "nyt_navn" : "vores_raket", content[1].GetProperty("props").GetProperty("name").GetString());
        using var payload = JsonDocument.Parse(transport.Body!);
        var prompt = payload.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!;
        Assert.Contains("vores_raket", prompt);
        Assert.Contains("rumfart", prompt);
        Assert.DoesNotContain("falsk_fra_klienten", prompt);
        Assert.DoesNotContain(forged, prompt);
        Assert.DoesNotContain("emojis/ours", prompt);
    }

    [Fact]
    public async Task ControllerDoesNotReturnAnEmojiDeletedWhileTheModelWasAnswering()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var emoji = new CustomEmoji { Id = Guid.NewGuid(), Name = "forsvinder", Key = "emojis/deleted", ContentType = "image/png" };
        db.CustomEmojis.Add(emoji);
        await db.SaveChangesAsync();
        using var transport = new EmojiTransport(emoji.Id.ToString(), Guid.NewGuid().ToString(), async () =>
        {
            db.CustomEmojis.Remove(emoji);
            await db.SaveChangesAsync();
        });
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };

        var response = Assert.IsType<EditorAiResponse>(Assert.IsType<OkObjectResult>(await Controller(db, http).Editor(new("generate", "Brug emoji"), default)).Value);
        var content = Assert.Single(response.Blocks).Content!.Value;
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("Hej ", content[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("raymond")]
    [InlineData("beaver")]
    [InlineData("fox")]
    [InlineData("lupp")]
    [InlineData("chameleon")]
    [InlineData("whale")]
    [InlineData("gummianden")]
    public async Task CharacterEmojiMetadataIsAuthoritativeAndKnownIdsSurviveSanitizing(string character)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var asset = CharacterEmojiCatalog.All.First(item => item.CharacterSlug == character);
        var unknown = Guid.NewGuid().ToString();
        using var transport = new EmojiTransport(asset.Id.ToString(), unknown, () => Task.CompletedTask);
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };

        var response = Assert.IsType<EditorAiResponse>(Assert.IsType<OkObjectResult>(
            await Controller(db, http).Editor(new("generate", "Brug en passende karakteremoji"), default)).Value);
        var content = Assert.Single(response.Blocks).Content!.Value;
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal(asset.Id.ToString(), content[1].GetProperty("props").GetProperty("emojiId").GetString());
        Assert.Equal(asset.Name, content[1].GetProperty("props").GetProperty("name").GetString());

        var prompt = UserPrompt(transport);
        var metadataLine = Assert.Single(prompt.Split('\n'), line => line.Contains(asset.Id.ToString(), StringComparison.Ordinal));
        using var metadataJson = JsonDocument.Parse(metadataLine);
        var expected = CharacterEmojiPolicy.Resolve(asset);
        Assert.Equal(asset.Name, metadataJson.RootElement.GetProperty("name").GetString());
        Assert.Equal(expected.AltText, metadataJson.RootElement.GetProperty("altText").GetString());
        Assert.Equal(expected.Category, metadataJson.RootElement.GetProperty("category").GetString());
        Assert.Equal(expected.Keywords, metadataJson.RootElement.GetProperty("keywords").EnumerateArray().Select(item => item.GetString()!));
        Assert.DoesNotContain(unknown, prompt);
        Assert.DoesNotContain("/images/characters/", prompt);
        Assert.DoesNotContain("ticket=", prompt);
        Assert.Empty(await db.CustomEmojis.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CharacterCatalogKeepsLegacyDatabaseNamesAndIdsAuthoritative(bool collideById)
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var asset = CharacterEmojiCatalog.All[0];
        var legacy = new CustomEmoji
        {
            Id = collideById ? asset.Id : Guid.NewGuid(), Name = collideById ? "legacy_upload" : asset.Name,
            Key = "emojis/private-legacy", ContentType = "image/png", AltText = "Existing uploaded image",
            Category = "learning", KeywordsJson = "[\"legacy\"]"
        };
        db.CustomEmojis.Add(legacy);
        await db.SaveChangesAsync();
        var secondId = collideById ? Guid.NewGuid().ToString() : legacy.Id.ToString();
        using var transport = new EmojiTransport(asset.Id.ToString(), secondId, () => Task.CompletedTask);
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };

        var response = Assert.IsType<EditorAiResponse>(Assert.IsType<OkObjectResult>(
            await Controller(db, http).Editor(new("generate", "Bevar vores emoji"), default)).Value);
        var content = Assert.Single(response.Blocks).Content!.Value;
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal(legacy.Id.ToString(), content[1].GetProperty("props").GetProperty("emojiId").GetString());
        Assert.Equal(legacy.Name, content[1].GetProperty("props").GetProperty("name").GetString());
        var prompt = UserPrompt(transport);
        var line = Assert.Single(prompt.Split('\n'), line => line.Contains(legacy.Id.ToString(), StringComparison.Ordinal));
        using var metadata = JsonDocument.Parse(line);
        Assert.Equal(legacy.Name, metadata.RootElement.GetProperty("name").GetString());
        Assert.Equal(legacy.AltText, metadata.RootElement.GetProperty("altText").GetString());
        if (!collideById) Assert.DoesNotContain(asset.Id.ToString(), prompt);
        Assert.DoesNotContain(legacy.Key, prompt);
    }

    [Fact]
    public async Task CharacterCatalogRemainsAvailableWhenTheUploadedEmojiLibraryIsFull()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.CustomEmojis.AddRange(Enumerable.Range(0, EmojiName.MaxCount).Select(index => new CustomEmoji
        {
            Id = Guid.NewGuid(), Name = $"stored_{index:D3}", Key = $"emojis/stored-{index}", ContentType = "image/png"
        }));
        await db.SaveChangesAsync();
        var asset = CharacterEmojiCatalog.All[^1];
        using var transport = new EmojiTransport(asset.Id.ToString(), Guid.NewGuid().ToString(), () => Task.CompletedTask);
        using var http = new HttpClient(transport) { BaseAddress = new Uri("https://openai.test/v1/") };

        var response = Assert.IsType<EditorAiResponse>(Assert.IsType<OkObjectResult>(
            await Controller(db, http).Editor(new("generate", "Brug en karakteremoji"), default)).Value);
        var content = Assert.Single(response.Blocks).Content!.Value;
        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal(asset.Id.ToString(), content[1].GetProperty("props").GetProperty("emojiId").GetString());
        Assert.Equal(asset.Name, content[1].GetProperty("props").GetProperty("name").GetString());
        Assert.Contains("stored_399", UserPrompt(transport));
    }

    private static string UserPrompt(EmojiTransport transport)
    {
        using var payload = JsonDocument.Parse(transport.Body!);
        return payload.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").GetString()!;
    }

    private static AiController Controller(AppDbContext db, HttpClient http) => new(
        new OpenAiEditorClient(new Factory(http), Options.Create(new OpenAiOptions { ApiKey = "test-only-key" }), NullLogger<OpenAiEditorClient>.Instance), db)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", Guid.NewGuid().ToString()), new("role", "Teacher")], "test"))
        } }
    };

    private sealed class Factory(HttpClient http) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => http;
    }

    private sealed class EmojiTransport(string id, string forged, Func<Task> beforeResponse) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Body = await request.Content!.ReadAsStringAsync(ct);
            await beforeResponse();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                choices = new[] { new { message = new { content = JsonSerializer.Serialize(new
                {
                    blocks = new[] { new { type = "paragraph", content = new object[]
                    {
                        new { type = "text", text = "Hej " },
                        new { type = "customEmoji", props = new { emojiId = id, name = "modelgæt" } },
                        new { type = "customEmoji", props = new { emojiId = forged, name = "falsk" } }
                    } } }, assistantNote = "Her er jeres emoji."
                }) } } }
            }) };
        }
    }
}
