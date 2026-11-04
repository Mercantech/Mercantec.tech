using System.Security.Claims;
using Mercantec.Tech.Api.Controllers;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed class GiftsControllerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private AppDbContext _db = null!;
    private static readonly Guid Sender = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Receiver = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Stranger = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Teacher = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        _db.GiftContacts.AddRange(new GiftContact { UserSub = Sender, DisplayName = "Kasper" }, new GiftContact { UserSub = Receiver, DisplayName = "Mathias" }, new GiftContact { UserSub = Stranger, DisplayName = "Andet hold" }, new GiftContact { UserSub = Teacher, DisplayName = "Underviser", IsTeacher = true });
        var course = new Forloeb { Id = Guid.NewGuid(), Name = "H3", CreatedBySub = Teacher, CreatedAtUtc = DateTimeOffset.UtcNow };
        var hold = new Hold { Id = Guid.NewGuid(), Name = "H3 hold", Forloeb = course, CreatedAtUtc = DateTimeOffset.UtcNow };
        _db.Memberships.AddRange(new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = Sender, Source = "manual", CreatedAtUtc = DateTimeOffset.UtcNow }, new Membership { Id = Guid.NewGuid(), Hold = hold, UserSub = Receiver, Source = "manual", CreatedAtUtc = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
    private GiftsController As(Guid? sub, bool teacher = false)
    {
        var claims = sub is Guid id ? new List<Claim> { new("sub", id.ToString()), new("name", "Navn fra Auth") } : [];
        if (teacher) claims.Add(new("role", "Underviser"));
        return new(_db) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, sub is null ? null : "test")) } } };
    }
    private static GiftsController.SendGiftRequest Draft(Guid? recipient = null) => new(Guid.NewGuid(), recipient ?? Receiver, "cheer", "berry", "Tak for hjælpen!");
    private async Task<GiftsController.GiftDto> Send(GiftsController.SendGiftRequest request) => Assert.IsType<GiftsController.GiftDto>(Assert.IsType<CreatedResult>(await As(Sender).Send(request, default)).Value);

    [Fact] public async Task AnonymousRequestsAreRejected()
    {
        var controller = As(null);
        Assert.IsType<UnauthorizedResult>(await controller.Register(default));
        Assert.IsType<UnauthorizedResult>(await controller.Recipients(default));
        Assert.IsType<UnauthorizedResult>(await controller.Mailbox(default));
        Assert.IsType<UnauthorizedResult>(await controller.Send(Draft(), default));
        Assert.IsType<UnauthorizedResult>(await controller.Open(Guid.NewGuid(), default));
    }
    [Fact] public async Task StudentOnlySeesPeersAndTheirTeacher()
    {
        var recipients = Assert.IsType<List<GiftsController.RecipientDto>>(Assert.IsType<OkObjectResult>(await As(Sender).Recipients(default)).Value);
        Assert.Contains(recipients, r => r.UserSub == Receiver);
        Assert.Contains(recipients, r => r.UserSub == Teacher);
        Assert.DoesNotContain(recipients, r => r.UserSub == Sender || r.UserSub == Stranger);
    }
    [Fact] public async Task TeacherCanChooseOtherRegisteredUsers()
    {
        var recipients = Assert.IsType<List<GiftsController.RecipientDto>>(Assert.IsType<OkObjectResult>(await As(Sender, true).Recipients(default)).Value);
        Assert.Contains(recipients, r => r.UserSub == Stranger);
    }
    [Fact] public async Task RecipientAndPayloadAreValidated()
    {
        Assert.IsType<BadRequestObjectResult>(await As(Sender).Send(Draft(Stranger), default));
        Assert.IsType<BadRequestObjectResult>(await As(Sender).Send(Draft(Sender), default));
        Assert.IsType<BadRequestObjectResult>(await As(Sender).Send(Draft() with { Message = new string('x', 241) }, default));
        Assert.IsType<BadRequestObjectResult>(await As(Sender).Send(Draft() with { Kind = "guld" }, default));
        Assert.IsType<BadRequestObjectResult>(await As(Sender).Send(Draft() with { Wrapping = "unknown" }, default));
        Assert.Empty(await _db.ChristmasGifts.ToListAsync());
    }
    [Fact] public async Task OnlyTheRecipientCanOpenAGiftAndOpeningIsIdempotent()
    {
        var gift = await Send(Draft());
        Assert.IsType<NotFoundResult>(await As(Stranger).Open(gift.Id, default));
        Assert.IsType<NotFoundResult>(await As(Sender).Open(gift.Id, default));
        var opened = Assert.IsType<GiftsController.GiftDto>(Assert.IsType<OkObjectResult>(await As(Receiver).Open(gift.Id, default)).Value);
        var again = Assert.IsType<GiftsController.GiftDto>(Assert.IsType<OkObjectResult>(await As(Receiver).Open(gift.Id, default)).Value);
        Assert.NotNull(opened.OpenedAtUtc);
        Assert.Equal(opened.OpenedAtUtc, again.OpenedAtUtc);
    }
    [Fact] public async Task RetryDoesNotSendTwiceAndChangedPayloadConflicts()
    {
        var draft = Draft();
        var gift = await Send(draft);
        Assert.IsType<OkObjectResult>(await As(Sender).Send(draft, default));
        Assert.IsType<ConflictObjectResult>(await As(Sender).Send(draft with { Message = "Anden besked" }, default));
        Assert.Single(await _db.ChristmasGifts.ToListAsync());
        Assert.Equal("Kasper", gift.SenderName);
    }
    [Fact] public async Task MailboxOnlyContainsOwnGiftsAndUnreadChangesWhenOpened()
    {
        var gift = await Send(Draft());
        var stranger = Assert.IsType<GiftsController.MailboxDto>(Assert.IsType<OkObjectResult>(await As(Stranger).Mailbox(default)).Value);
        Assert.Empty(stranger.Received); Assert.Empty(stranger.Sent);
        var inbox = Assert.IsType<GiftsController.MailboxDto>(Assert.IsType<OkObjectResult>(await As(Receiver).Mailbox(default)).Value);
        Assert.Equal(1, inbox.Unread); Assert.Single(inbox.Received);
        await As(Receiver).Open(gift.Id, default);
        inbox = Assert.IsType<GiftsController.MailboxDto>(Assert.IsType<OkObjectResult>(await As(Receiver).Mailbox(default)).Value);
        Assert.Equal(0, inbox.Unread);
        var sent = Assert.IsType<GiftsController.MailboxDto>(Assert.IsType<OkObjectResult>(await As(Sender).Mailbox(default)).Value);
        Assert.NotNull(Assert.Single(sent.Sent).OpenedAtUtc);
    }
    [Fact] public async Task RegisterUsesTheVerifiedIdentity()
    {
        Assert.IsType<NoContentResult>(await As(Sender, true).Register(default));
        var profile = await _db.GiftContacts.FindAsync(Sender);
        Assert.Equal("Navn fra Auth", profile!.DisplayName); Assert.True(profile.IsTeacher);
    }
}
