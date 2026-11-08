using System.Reflection;
using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class GiftReadTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private static readonly Guid Sender = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Recipient = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("33333333-3333-4333-8333-333333333333");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
    private GiftsController As(Guid? sub, bool teacher = false)
    {
        var claims = sub is Guid id ? new List<Claim> { new("sub", id.ToString()) } : [];
        if (teacher) claims.Add(new("role", "Underviser"));
        return new(_db) { ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity(claims, sub is null ? null : "test")) } } };
    }
    private static ChristmasGift Gift(DateTime? created = null) => new()
    {
        Id = Guid.NewGuid(), SenderSub = Sender, RecipientSub = Recipient,
        SenderName = "Afsender", RecipientName = "Modtager", Kind = "thanks", Wrapping = "pine",
        Message = "En privat hilsen", CreatedAtUtc = created ?? DateTime.UtcNow
    };

    [Fact]
    public async Task The_recipient_can_read_a_gift_without_opening_it_or_changing_the_unread_count()
    {
        var gift = Gift(); _db.ChristmasGifts.Add(gift); await _db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await As(Recipient).Get(gift.Id, default));
        var response = Assert.IsType<GiftsController.GiftDto>(result.Value);
        Assert.Equal(gift.Id, response.Id);
        Assert.Equal("En privat hilsen", response.Message);
        Assert.Null(response.OpenedAtUtc);
        Assert.Null(await _db.ChristmasGifts.AsNoTracking().Where(g => g.Id == gift.Id).Select(g => g.OpenedAtUtc).SingleAsync());
        var mailbox = Assert.IsType<GiftsController.MailboxDto>(Assert.IsType<OkObjectResult>(await As(Recipient).Mailbox(default)).Value);
        Assert.Equal(1, mailbox.Unread);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Other_accounts_and_teachers_cannot_read_or_impersonate_the_recipient(bool teacher)
    {
        var gift = Gift(); _db.ChristmasGifts.Add(gift); await _db.SaveChangesAsync();
        var controller = As(Stranger, teacher);
        controller.Request.QueryString = new QueryString($"?asUser={Recipient}");
        Assert.IsType<NotFoundResult>(await controller.Get(gift.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Get(Guid.NewGuid(), default));
        Assert.IsType<NotFoundResult>(await As(Sender).Get(gift.Id, default));
        Assert.Null(await _db.ChristmasGifts.AsNoTracking().Where(g => g.Id == gift.Id).Select(g => g.OpenedAtUtc).SingleAsync());
    }

    [Fact]
    public async Task Anonymous_reads_are_rejected()
    {
        Assert.IsType<UnauthorizedResult>(await As(null).Get(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task A_direct_link_can_read_an_older_gift_outside_the_mailboxs_fifty_rows()
    {
        var oldest = Gift(DateTime.UtcNow.AddDays(-100));
        _db.ChristmasGifts.Add(oldest);
        _db.ChristmasGifts.AddRange(Enumerable.Range(0, 60).Select(index => Gift(DateTime.UtcNow.AddMinutes(-index))));
        await _db.SaveChangesAsync();
        var mailbox = Assert.IsType<GiftsController.MailboxDto>(Assert.IsType<OkObjectResult>(await As(Recipient).Mailbox(default)).Value);
        Assert.Equal(50, mailbox.Received.Count);
        Assert.DoesNotContain(mailbox.Received, gift => gift.Id == oldest.Id);
        var direct = Assert.IsType<GiftsController.GiftDto>(Assert.IsType<OkObjectResult>(await As(Recipient).Get(oldest.Id, default)).Value);
        Assert.Equal(oldest.Id, direct.Id);
        Assert.Null(direct.OpenedAtUtc);
    }

    [Fact]
    public void Private_gift_reads_disable_shared_and_browser_response_caching()
    {
        var policy = typeof(GiftsController).GetMethod(nameof(GiftsController.Get))!.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(policy);
        Assert.True(policy.NoStore);
        Assert.Equal(ResponseCacheLocation.None, policy.Location);
    }
}
