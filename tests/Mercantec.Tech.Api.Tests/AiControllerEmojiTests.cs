using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.OpenAI;
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
