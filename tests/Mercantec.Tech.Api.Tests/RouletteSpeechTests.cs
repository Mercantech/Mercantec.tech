using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Learning;
using Mercantec.Tech.Api.Speech;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Tests;

public sealed class RouletteSpeechTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly FakeGenerator generator = new();
    private readonly RouletteSpeechOptions options = new() { Enabled = true };
    private readonly Guid teacher = Guid.NewGuid(), colleague = Guid.NewGuid(), pupil = Guid.NewGuid(), outsider = Guid.NewGuid();
    private AppDbContext db = null!;
    private RouletteSpeechService speech = null!;
    private Guid holdId, otherHoldId, annaId, boId, foreignId;

    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var hold = new Hold { Id = Guid.NewGuid(), Name = "Testhold", Forloeb = new() { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = teacher } };
        var other = new Hold { Id = Guid.NewGuid(), Name = "Andet hold", Forloeb = new() { Id = Guid.NewGuid(), Name = "H4", CreatedBySub = teacher } };
        holdId = hold.Id; otherHoldId = other.Id;
        annaId = Guid.NewGuid(); boId = Guid.NewGuid(); foreignId = Guid.NewGuid();
        db.Hold.AddRange(hold, other);
        db.Memberships.AddRange(
            new() { Id = annaId, Hold = hold, UserSub = pupil, DisplayName = "Anna" },
            new() { Id = boId, Hold = hold, DisplayName = "Bo" },
            new() { Id = Guid.NewGuid(), Hold = hold, UserSub = colleague, DisplayName = "Medunderviser" },
            new() { Id = foreignId, Hold = other, DisplayName = "Anden elev" });
        await db.SaveChangesAsync();
        speech = new(generator, Options.Create(options));
    }

    public async Task DisposeAsync()
    {
        speech.Dispose();
        await db.DisposeAsync();
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task Audio_cache_is_private_to_teacher_hold_and_text_and_responses_cannot_be_publicly_cached()
    {
        var request = new CreateRouletteSpeech("TurboRobotterne", []);
        var api = Api(teacher, true);
        var status = Value<RouletteSpeechStatus>(await api.Status(holdId, default));
        Assert.True(status.Available);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());

        var first = Value<RouletteSpeechAudio>(await api.Create(holdId, request, default));
        var again = Value<RouletteSpeechAudio>(await api.Create(holdId, request, default));
        Assert.Equal(first, again);
        Assert.Equal("audio/wav", first.ContentType);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
        Assert.Single(generator.Texts);
        await Api(colleague, true).Create(holdId, request, default);
        await api.Create(otherHoldId, request, default);
        await api.Create(holdId, request with { GroupName = "DiscoRobotterne" }, default);
        Assert.Equal(4, generator.Texts.Count);
        Assert.Equal(new[] { "Turbo Robotterne.", "Turbo Robotterne.", "Turbo Robotterne.", "Disco Robotterne." }, generator.Texts.ToArray());
    }

    [Fact]
    public async Task A_warm_audio_cache_does_not_bypass_hold_management_or_authentication()
    {
        var request = new CreateRouletteSpeech("TurboRobotterne", []);
        Value<RouletteSpeechAudio>(await Api(teacher, true).Create(holdId, request, default));
        foreach (var api in new[] { Api(pupil), Api(outsider, true) })
        {
            Assert.IsType<NotFoundResult>(await api.Status(holdId, default));
            Assert.IsType<NotFoundResult>(await api.Create(holdId, request, default));
        }
        Assert.IsType<UnauthorizedResult>(await Api(null).Create(holdId, request, default));
        Assert.Single(generator.Texts);
        Assert.Equal(0, generator.StatusCalls);
    }

    [Fact]
    public async Task Member_names_require_explicit_opt_in_and_resolve_from_this_holds_roster_in_request_order()
    {
        var api = Api(teacher, true);
        await api.Create(holdId, new("TurboRobotterne", [annaId, boId]), default);
        await api.Create(holdId, new("TurboRobotterne", [boId, annaId], true), default);
        Assert.Equal(new[] { "Turbo Robotterne.", "Turbo Robotterne. Bo, Anna." }, generator.Texts.ToArray());
    }

    [Fact]
    public async Task Invalid_or_foreign_references_never_reach_the_local_voice_generator()
    {
        CreateRouletteSpeech[] invalid = [
            new("", []), new("Turbo\nRobotterne", []), new(new string('A', 121), []),
            new("TurboRobotterne", null!), new("TurboRobotterne", [foreignId], true),
            new("TurboRobotterne", [annaId, annaId], true), new("TurboRobotterne", [], true),
            new("TurboRobotterne", Enumerable.Range(0, 13).Select(_ => Guid.NewGuid()).ToArray(), true)
        ];
        foreach (var request in invalid)
            Assert.IsType<BadRequestObjectResult>(await Api(teacher, true).Create(holdId, request, default));
        Assert.Empty(generator.Texts);
    }

    [Fact]
    public async Task Disabled_local_voice_reports_unavailable_and_returns_503_without_loading_the_generator()
    {
        options.Enabled = false;
        var api = Api(teacher, true);
        Assert.False(Value<RouletteSpeechStatus>(await api.Status(holdId, default)).Available);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await api.Create(holdId, new("TurboRobotterne", []), default)).StatusCode);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
        Assert.Empty(generator.Texts);
        Assert.Equal(0, generator.StatusCalls);
    }

    [Theory]
    [InlineData("unreachable")]
    [InlineData("truncated")]
    [InlineData("bad-header")]
    [InlineData("wrong-type")]
    public async Task Unavailable_or_invalid_audio_returns_503_instead_of_an_unplayable_clip(string failure)
    {
        if (failure == "unreachable") generator.Failure = new HttpRequestException("Private service diagnostic");
        if (failure == "truncated") generator.Bytes = new byte[12];
        if (failure == "bad-header") generator.Bytes = new byte[48];
        if (failure == "wrong-type") generator.ContentType = "text/html";
        var api = Api(teacher, true);
        var result = Assert.IsType<ObjectResult>(await api.Create(holdId, new("TurboRobotterne", []), default));
        Assert.Equal(503, result.StatusCode);
        var payload = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("local_speech_unavailable", payload);
        Assert.DoesNotContain("Private service diagnostic", payload);
        Assert.Equal("no-store", api.Response.Headers.CacheControl.ToString());
        Assert.Single(generator.Texts);
    }

    private RouletteSpeechController Api(Guid? sub, bool teaching = false)
    {
        var claims = new List<Claim>();
        if (sub is Guid id) claims.Add(new("sub", id.ToString()));
        if (teaching) claims.Add(new(ClaimTypes.Role, "Underviser"));
        return new(db, new CourseAccess(db), speech) { ControllerContext = new() {
            HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, sub is null ? null : "test")) } } };
    }
    private static T Value<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private sealed class FakeGenerator : IRouletteSpeechGenerator
    {
        public ConcurrentQueue<string> Texts { get; } = new();
        public int StatusCalls;
        public Exception? Failure { get; set; }
        public string ContentType { get; set; } = "audio/wav";
        public byte[] Bytes { get; set; } = Wav();
        public Task<RouletteSpeechStatus> StatusAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref StatusCalls);
            return Task.FromResult(new RouletteSpeechStatus(true, "Røst"));
        }
        public Task<GeneratedRouletteSpeech> GenerateAsync(string text, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Texts.Enqueue(text);
            if (Failure is not null) throw Failure;
            return Task.FromResult(new GeneratedRouletteSpeech(Bytes, ContentType, "Røst"));
        }
        private static byte[] Wav()
        {
            var bytes = new byte[48];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
            Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
            return bytes;
        }
    }
}
