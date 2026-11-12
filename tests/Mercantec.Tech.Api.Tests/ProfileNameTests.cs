using System.Security.Claims;
using System.Text.Json;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class ProfileNameTests : IAsyncLifetime
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000102");
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"profile-name-tests-{Guid.NewGuid():N}.db");

    public async Task InitializeAsync()
    {
        await using var db = OpenDatabase();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(_databasePath + suffix);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_is_read_only_and_names_are_saved_per_account_across_contexts()
    {
        const string name = "Søren 李";
        await using (var firstDevice = OpenDatabase())
        {
            var alice = As(firstDevice, Alice);
            Assert.Null(Success(await alice.Get(default)).DisplayName);
            Assert.Empty(await firstDevice.ProfileNames.ToListAsync());
            Assert.Equal(name, Success(await alice.Update(new($"  {name}  "), default)).DisplayName);
        }

        await using var secondDevice = OpenDatabase();
        Assert.Equal(name, Success(await As(secondDevice, Alice).Get(default)).DisplayName);
        Assert.Null(Success(await As(secondDevice, Bob).Get(default)).DisplayName);
        Assert.Equal("Élodie", Success(await As(secondDevice, Bob).Update(new("Élodie"), default)).DisplayName);
        Assert.Equal(name, Success(await As(secondDevice, Alice).Get(default)).DisplayName);
    }

    [Fact]
    public async Task Reset_deletes_only_the_callers_override_and_is_safe_to_retry()
    {
        await using var db = OpenDatabase();
        var alice = As(db, Alice);
        Success(await alice.Update(new("Søren"), default));
        Success(await As(db, Bob).Update(new("Élodie"), default));

        Assert.Null(Success(await alice.Update(new(null), default)).DisplayName);
        Assert.Null(Success(await alice.Update(new(null), default)).DisplayName);

        await using var fresh = OpenDatabase();
        Assert.Null(Success(await As(fresh, Alice).Get(default)).DisplayName);
        var remaining = Assert.Single(await fresh.ProfileNames.ToListAsync());
        Assert.Equal(Bob, remaining.UserSub);
        Assert.Equal("Élodie", remaining.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("\tSøren")]
    [InlineData("Søren\n")]
    [InlineData("Sø\0ren")]
    [InlineData("Sø\u2028ren")]
    [InlineData("Sø\u2029ren")]
    public async Task Invalid_values_do_not_reset_or_replace_a_saved_name(string input)
    {
        await using var db = OpenDatabase();
        var alice = As(db, Alice);
        Success(await alice.Update(new("Søren"), default));

        Failure(await alice.Update(new(input), default), 400, "invalid_display_name");
        Assert.Equal("Søren", Success(await alice.Get(default)).DisplayName);
    }

    [Fact]
    public async Task Limit_is_measured_in_utf16_after_trimming()
    {
        await using var db = OpenDatabase();
        var alice = As(db, Alice);
        var atLimit = new string('Æ', 58) + "😀";

        Assert.Equal(atLimit, Success(await alice.Update(new($"  {atLimit}  "), default)).DisplayName);
        Failure(await alice.Update(new(atLimit + "x"), default), 400, "invalid_display_name");
        Assert.Equal(atLimit, Success(await alice.Get(default)).DisplayName);
    }

    [Fact]
    public async Task Update_requires_the_same_valid_subject_in_the_guard_and_the_authenticated_claim()
    {
        await using var db = OpenDatabase();
        var alice = As(db, Alice);
        Success(await alice.Update(new("Søren"), default));
        var bob = As(db, Bob);

        bob.Request.Headers[ProfileNameRules.SubjectHeader] = Alice.ToString();
        Failure(await bob.Update(new(null), default), 409, "account_changed");
        bob.Request.Headers[ProfileNameRules.SubjectHeader] = "not-a-guid";
        Failure(await bob.Update(new("Forkert"), default), 409, "account_changed");
        bob.Request.Headers.Remove(ProfileNameRules.SubjectHeader);
        Failure(await bob.Update(new("Forkert"), default), 409, "account_changed");

        Assert.Equal("Søren", Success(await alice.Get(default)).DisplayName);
        Assert.Null(Success(await bob.Get(default)).DisplayName);
    }

    [Fact]
    public async Task Get_rejects_a_mismatched_subject_guard_but_also_supports_reads_without_it()
    {
        await using var db = OpenDatabase();
        var bob = As(db, Bob);
        Success(await bob.Update(new("Élodie"), default));

        bob.Request.Headers[ProfileNameRules.SubjectHeader] = Alice.ToString();
        Failure(await bob.Get(default), 409, "account_changed");
        bob.Request.Headers.Remove(ProfileNameRules.SubjectHeader);
        Assert.Equal("Élodie", Success(await bob.Get(default)).DisplayName);
    }

    [Fact]
    public async Task Get_and_update_require_an_authenticated_nonempty_subject()
    {
        await using var db = OpenDatabase();
        foreach (var controller in new[] { As(db, null), As(db, Guid.Empty), As(db, Alice, authenticated: false) })
        {
            Assert.IsType<UnauthorizedResult>((await controller.Get(default)).Result);
            Assert.IsType<UnauthorizedResult>((await controller.Update(new("Søren"), default)).Result);
        }
        Assert.Empty(await db.ProfileNames.ToListAsync());
    }

    [Fact]
    public void Json_distinguishes_explicit_reset_from_a_missing_property()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateProfileNameRequest>("{}", json));
        Assert.Null(JsonSerializer.Deserialize<UpdateProfileNameRequest>("""{"displayName":null}""", json)!.DisplayName);
    }

    [Fact]
    public async Task Concurrent_first_updates_succeed_with_a_single_complete_row()
    {
        async Task<string?> Write(string value)
        {
            await using var db = OpenDatabase();
            return Success(await As(db, Alice).Update(new(value), default)).DisplayName;
        }

        var results = await Task.WhenAll(Task.Run(() => Write("Søren")), Task.Run(() => Write("Élodie")));

        Assert.Equal(new[] { "Søren", "Élodie" }, results);
        await using var fresh = OpenDatabase();
        var saved = Assert.Single(await fresh.ProfileNames.ToListAsync());
        Assert.Equal(Alice, saved.UserSub);
        Assert.Contains(saved.DisplayName, results);
    }

    [Fact]
    public async Task Privacy_export_includes_only_the_callers_display_name()
    {
        await using var db = OpenDatabase();
        var alice = As(db, Alice);
        Success(await alice.Update(new("Søren"), default));
        Success(await As(db, Bob).Update(new("Élodie"), default));
        var privacy = new PrivacyController(db) { ControllerContext = alice.ControllerContext };

        var export = Assert.IsType<FileContentResult>(await privacy.Export(default));
        using var data = JsonDocument.Parse(export.FileContents);

        Assert.Equal("Søren", data.RootElement.GetProperty("Profile").GetProperty("displayName").GetString());
        Assert.DoesNotContain(JsonSerializer.Serialize("Élodie"), data.RootElement.GetRawText());
    }

    private AppDbContext OpenDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"Data Source={_databasePath};Default Timeout=2;Pooling=False").Options);

    private static ProfileNameController As(AppDbContext db, Guid? subject, bool authenticated = true)
    {
        Claim[] claims = subject is Guid id ? [new("sub", id.ToString())] : [];
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
        };
        if (subject is Guid expected)
            http.Request.Headers[ProfileNameRules.SubjectHeader] = expected.ToString();
        return new ProfileNameController(db) { ControllerContext = new() { HttpContext = http } };
    }

    private static ProfileNameDto Success(ActionResult<ProfileNameDto> result) =>
        Assert.IsType<ProfileNameDto>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void Failure(ActionResult<ProfileNameDto> result, int status, string code)
    {
        var response = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, Assert.IsType<ProblemDetails>(response.Value).Extensions["code"]);
    }
}
