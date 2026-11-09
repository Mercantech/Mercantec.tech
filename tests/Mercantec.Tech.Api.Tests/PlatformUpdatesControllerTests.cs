using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Updates;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mercantec.Tech.Api.Tests;

public sealed class PlatformUpdatesControllerTests
{
    private static readonly PlatformUpdatesFeed Feed = new(NullLogger<PlatformUpdatesFeed>.Instance);

    [Fact]
    public void AnonymousCannotReadCatalogOrImages()
    {
        var controller = As(authenticated: false);
        Assert.IsType<UnauthorizedResult>(controller.List());
        Assert.IsType<UnauthorizedResult>(controller.List(includeText: true));
        Assert.IsType<UnauthorizedResult>(controller.Image(ImageId));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public void StudentsCannotReadCatalogOrImagesEvenWhenTheyKnowAnImageId()
    {
        var controller = As("Elev");
        Assert.IsType<ForbidResult>(controller.List());
        Assert.IsType<ForbidResult>(controller.List(includeText: true));
        Assert.IsType<ForbidResult>(controller.Image(ImageId));
        Assert.IsType<ForbidResult>(controller.Image("unknown"));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Theory]
    [InlineData("Underviser")]
    [InlineData("Teacher")]
    [InlineData("Instructor")]
    [InlineData("Admin")]
    public void ExistingTeacherRolesCanReadCatalogAndPrivateImages(string role)
    {
        var controller = As(role);
        var result = Assert.IsType<OkObjectResult>(controller.List());
        var updates = Assert.IsAssignableFrom<IReadOnlyList<PlatformUpdate>>(result.Value);
        Assert.NotEmpty(updates);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);

        var image = Assert.IsType<FileStreamResult>(controller.Image(Assert.IsType<UpdateImage>(updates[0].After).Id));
        using var content = image.FileStream;
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"]);
        Span<byte> header = stackalloc byte[12];
        Assert.Equal(12, content.Read(header));
        Assert.True(header[..4].SequenceEqual("RIFF"u8));
        Assert.True(header[8..].SequenceEqual("WEBP"u8));
    }

    [Fact]
    public void TeacherRoleWithoutValidAuthenticatedAccountIsRejected()
    {
        var controller = As("Admin", authenticated: false);
        Assert.IsType<UnauthorizedResult>(controller.List());
        Assert.IsType<UnauthorizedResult>(controller.Image(ImageId));

        controller = As("Teacher", sub: "not-a-subject");
        Assert.IsType<UnauthorizedResult>(controller.List());
        Assert.IsType<UnauthorizedResult>(controller.Image(ImageId));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("../appsettings")]
    [InlineData("..%2Fappsettings")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("image.webp")]
    [InlineData("")]
    public void ImageLookupRejectsUnknownIdsAndPaths(string id)
    {
        var controller = As("Underviser");
        Assert.IsType<NotFoundResult>(controller.Image(id));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public void EveryCuratedScreenshotIsEmbeddedAndEveryEntryHasUsefulText()
    {
        var updates = Feed.Entries;
        Assert.Equal(updates.Count, updates.Select(update => update.Id).Distinct().Count());
        Assert.Equal(updates.OrderByDescending(update => update.Date), updates);
        Assert.Contains(updates, update => update.Before is not null);
        foreach (var update in updates)
        {
            Assert.False(string.IsNullOrWhiteSpace(update.Title));
            Assert.False(string.IsNullOrWhiteSpace(update.Description));
            if (update.Before is not null) Assert.NotNull(update.After);
            foreach (var image in new[] { update.After, update.Before }.OfType<UpdateImage>())
            {
                Assert.False(string.IsNullOrWhiteSpace(image.Alt));
                using var stream = Feed.OpenImage(image.Id);
                Assert.NotNull(stream);
                Assert.True(stream.Length > 12);
            }
        }
    }

    [Fact]
    public void EmbeddedReleaseNotesJoinTheFeedAndOldClientsReceiveOnlyIllustratedEntries()
    {
        var controller = As("Underviser");
        var updates = Assert.IsAssignableFrom<IReadOnlyList<PlatformUpdate>>(
            Assert.IsType<OkObjectResult>(controller.List(includeText: true)).Value);
        var legacy = Assert.IsAssignableFrom<IReadOnlyList<PlatformUpdate>>(
            Assert.IsType<OkObjectResult>(controller.List()).Value);

        var note = Assert.Single(updates, update => update.Id == "nyheder-med-udgivelser");
        Assert.Null(note.After);
        Assert.Null(note.Before);
        Assert.DoesNotContain(legacy, update => update.Id == note.Id);
        Assert.Equal(updates.Where(update => update.After is not null), legacy);
        Assert.Contains(PlatformUpdatesCatalog.Entries[0], updates);

        const string prefix = "platform-updates/notes/";
        var embeddedIds = typeof(PlatformUpdatesFeed).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => name[prefix.Length..^".json".Length]);
        var publishedIds = updates.Select(update => update.Id)
            .Except(PlatformUpdatesCatalog.Entries.Select(update => update.Id));
        Assert.Equal(embeddedIds.Order(StringComparer.Ordinal), publishedIds.Order(StringComparer.Ordinal));
    }

    private static string ImageId => PlatformUpdatesCatalog.Entries[0].After!.Id;

    private static PlatformUpdatesController As(string role = "Elev", bool authenticated = true, string? sub = null)
    {
        var claims = new[] { new Claim("sub", sub ?? Guid.NewGuid().ToString()), new Claim("role", role) };
        return new PlatformUpdatesController(Feed)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null))
                }
            }
        };
    }
}
