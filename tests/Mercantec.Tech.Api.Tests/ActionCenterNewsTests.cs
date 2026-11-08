using System.Text.Json;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Tests;

public sealed partial class ActionCenterTests
{
    private (Notebook Notebook, NotebookDocument Page) NotebookPage(Guid owner, string title = "Delt note")
    {
        var notebook = new Notebook { Id = Guid.NewGuid(), OwnerSub = owner, CreatedAtUtc = _now };
        var page = new NotebookDocument { Id = Guid.NewGuid(), Notebook = notebook, Kind = "note", Title = title,
            SearchText = "Sensitive notebook contents", CreatedAtUtc = _now, UpdatedAtUtc = _now };
        _db.Notebooks.Add(notebook); _db.NotebookDocuments.Add(page); return (notebook, page);
    }
    private NotebookComment Comment(NotebookDocument page, Guid author)
    {
        var comment = new NotebookComment { Id = Guid.NewGuid(), PageId = page.Id, AuthorSub = author,
            Text = "Sensitive comment body", CreatedAtUtc = _now };
        _db.NotebookComments.Add(comment); return comment;
    }
    private NotebookShare Share(Notebook notebook, Guid recipient, Guid? page = null)
    {
        var share = new NotebookShare { Id = Guid.NewGuid(), NotebookId = notebook.Id, RecipientSub = recipient,
            PageId = page, CreatedAtUtc = _now };
        _db.NotebookShares.Add(share); return share;
    }
    private SubmissionFeedback Feedback(SubmissionRevision revision, Guid author, string? decision = null)
    {
        var feedback = new SubmissionFeedback { Id = Guid.NewGuid(), RevisionId = revision.Id, TeacherSub = author,
            Decision = decision, Text = "Sensitive feedback body", CreatedAtUtc = _now };
        _db.SubmissionFeedback.Add(feedback); return feedback;
    }
    private static ActionCenterReadRequest Acknowledge(params ActionCenterItem[] items) => new(items.Select(i => new ActionCenterReadItem(i.Kind, i.ResourceId)).ToArray());

    [Fact]
    public async Task Feedback_news_is_personal_content_free_and_can_be_acknowledged_idempotently()
    {
        var quest = Quest("Min opgave"); var submission = Submission(quest, "approved", [_member]);
        var feedback = Feedback(submission.Revisions.Single(), _teacher, "approved");
        var course = new TeachingCourse { Id = Guid.NewGuid(), HoldId = _hold.Id, Title = "Web", Published = true };
        course.Resources.Add(new() { Kind = "quest", ResourceId = quest.Id }); _db.TeachingCourses.Add(course);
        await _db.SaveChangesAsync();
        var snapshot = Read(await Api(_student).Get(null, default, 2));
        var item = Assert.Single(snapshot.Items);
        Assert.Equal("feedback", item.Kind); Assert.Equal("optional", item.Severity); Assert.Equal("unread", item.State);
        Assert.Equal(feedback.Id, item.ResourceId); Assert.Equal(quest.Id, item.TargetId);
        Assert.Equal(submission.Id, item.SubmissionId); Assert.Equal(course.Id, item.CourseId);
        Assert.DoesNotContain("Sensitive", JsonSerializer.Serialize(snapshot));
        Assert.Empty(Read(await Api(_student).Get(null, default, 1)).Items);
        Assert.IsType<NoContentResult>(await Api(_student).Read(Acknowledge(item), default));
        var receipt = Assert.Single(await _db.ActionReadReceipts.AsNoTracking().ToArrayAsync());
        Assert.Equal(_student, receipt.UserSub); Assert.Equal(feedback.Id, receipt.ResourceId);
        Assert.Empty(Read(await Api(_student).Get(null, default, 2)).Items);
        Assert.IsType<NoContentResult>(await Api(_student).Read(Acknowledge(item), default));
        Assert.Equal(receipt.ReadAtUtc, Assert.Single(await _db.ActionReadReceipts.AsNoTracking().ToArrayAsync()).ReadAtUtc);
    }

    [Fact]
    public async Task Feedback_uses_the_personal_revision_snapshot_after_group_changes_and_never_another_participants_feedback()
    {
        var quest = Quest("Gruppeopgave"); var group = Group(_member, _peerMember);
        var submission = Submission(quest, "approved", [_member], group);
        var own = Feedback(submission.Revisions.Single(), _teacher, "approved");
        var later = Revision(submission, 2, [_peerMember], _now.AddHours(1));
        Feedback(later, _teacher, "approved"); Feedback(submission.Revisions.First(), _student);
        await _db.SaveChangesAsync();
        var item = Assert.Single(Read(await Api(_student).Get(null, default, 2)).Items);
        Assert.Equal(own.Id, item.ResourceId);
        Assert.Empty(Read(await Api(_teacher, true).Get(_student, default, 2)).Items);
        Assert.IsType<NoContentResult>(await Api(_peer).Read(Acknowledge(item), default));
        Assert.Empty(await _db.ActionReadReceipts.ToArrayAsync());
        Assert.Equal(own.Id, Assert.Single(Read(await Api(_student).Get(null, default, 2)).Items).ResourceId);
    }

    [Fact]
    public async Task Changes_requested_is_one_required_action_and_unpublished_or_own_feedback_is_not_news()
    {
        var returned = Quest("Retur"); var submission = Submission(returned, "changes-requested", [_member]);
        Feedback(submission.Revisions.Single(), _teacher, "changes-requested");
        Feedback(submission.Revisions.Single(), _student);
        var hidden = Quest("Trukket tilbage"); hidden.Published = false;
        Feedback(Submission(hidden, "approved", [_member]).Revisions.Single(), _teacher);
        await _db.SaveChangesAsync();
        var item = Assert.Single(Read(await Api(_student).Get(null, default, 2)).Items);
        Assert.Equal("submission", item.Kind); Assert.Equal("changes-requested", item.State);
    }

    [Fact]
    public async Task Owner_notebook_comment_news_works_without_holds_and_is_own_receipt_scoped()
    {
        var owner = Guid.NewGuid(); var (notebook, page) = NotebookPage(owner);
        var comment = Comment(page, _teacher); Comment(page, owner);
        await _db.SaveChangesAsync();
        var snapshot = Read(await Api(owner).Get(null, default, 2));
        var item = Assert.Single(snapshot.Items);
        Assert.Equal("notebook-comment", item.Kind); Assert.Equal(comment.Id, item.ResourceId); Assert.Equal(page.Id, item.TargetId);
        Assert.Null(item.HoldId); Assert.Null(item.WorkspaceId); Assert.Null(item.SubmissionId);
        Assert.DoesNotContain("Sensitive", JsonSerializer.Serialize(snapshot));
        Assert.Empty(Read(await Api(owner).Get(null, default, 1)).Items);
        await Api(_student).Read(Acknowledge(item), default);
        Assert.Empty(await _db.ActionReadReceipts.ToArrayAsync());
        await Api(owner).Read(Acknowledge(item), default);
        Assert.Empty(Read(await Api(owner).Get(null, default, 2)).Items);
    }

    [Fact]
    public async Task Share_reader_gets_share_notice_but_no_new_comment_privileges_and_teacher_global_access_is_not_subscription()
    {
        var (notebook, page) = NotebookPage(_peer);
        var comment = Comment(page, _otherTeacher);
        var studentShare = Share(notebook, _student, page.Id);
        await _db.SaveChangesAsync();
        var student = Assert.Single(Read(await Api(_student).Get(null, default, 2)).Items);
        Assert.Equal("notebook-share", student.Kind); Assert.Equal(studentShare.Id, student.ResourceId);
        Assert.Equal("shared-page", student.State); Assert.Equal(page.Id, student.TargetId);
        Assert.Empty(Read(await Api(_teacher, true).Get(null, default, 2)).Items);
        Share(notebook, _teacher, page.Id); await _db.SaveChangesAsync();
        var teacher = Read(await Api(_teacher, true).Get(null, default, 2));
        Assert.Equal(2, teacher.Items.Length);
        Assert.Contains(teacher.Items, i => i.Kind == "notebook-comment" && i.ResourceId == comment.Id);
        Assert.Contains(teacher.Items, i => i.Kind == "notebook-share");
        await Api(_student).Read(new([new("notebook-comment", comment.Id)]), default);
        Assert.Empty(await _db.ActionReadReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task Revoked_share_hides_its_notice_and_comment_news_and_cannot_be_acknowledged_after_revocation()
    {
        var (notebook, page) = NotebookPage(_peer);
        Comment(page, _otherTeacher); var share = Share(notebook, _teacher, page.Id);
        await _db.SaveChangesAsync();
        var before = Read(await Api(_teacher, true).Get(null, default, 2));
        Assert.Equal(2, before.Items.Length);
        _db.NotebookShares.Remove(share); await _db.SaveChangesAsync();
        Assert.Empty(Read(await Api(_teacher, true).Get(null, default, 2)).Items);
        await Api(_teacher, true).Read(Acknowledge(before.Items), default);
        Assert.Empty(await _db.ActionReadReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task Whole_notebook_share_has_notebook_target_and_mixed_batch_reads_only_its_three_news_kinds()
    {
        var (notebook, page) = NotebookPage(_peer);
        var whole = Share(notebook, _student);
        var (mine, myPage) = NotebookPage(_student); Comment(myPage, _teacher);
        var quest = Quest("Godkendt"); Feedback(Submission(quest, "approved", [_member]).Revisions.Single(), _teacher, "approved");
        var required = Quest("Skal afleveres"); var gift = Gift(_student); var poll = Poll("Mangler svar");
        await _db.SaveChangesAsync();
        var snapshot = Read(await Api(_student).Get(null, default, 2));
        var shareItem = snapshot.Items.Single(i => i.ResourceId == whole.Id);
        Assert.Equal("shared-notebook", shareItem.State); Assert.Equal(notebook.Id, shareItem.TargetId);
        await Api(_student).Read(new(snapshot.Items.Select(i => new ActionCenterReadItem(i.Kind, i.ResourceId))
            .Append(new("notebook-share", Guid.NewGuid())).Append(new("unknown", Guid.NewGuid())).ToArray()), default);
        Assert.Equal(3, await _db.ActionReadReceipts.CountAsync());
        var remaining = Read(await Api(_student).Get(null, default, 2));
        Assert.Equal(3, remaining.Items.Length);
        Assert.Contains(remaining.Items, i => i.ResourceId == required.Id);
        Assert.Contains(remaining.Items, i => i.ResourceId == gift.Id);
        Assert.Contains(remaining.Items, i => i.ResourceId == poll.Id);
    }

    [Fact]
    public async Task Receipt_batches_require_auth_and_are_bounded_without_changing_anyone_elses_state()
    {
        Assert.IsType<UnauthorizedResult>(await Api(null).Read(new([]), default));
        Assert.IsType<BadRequestObjectResult>(await Api(_student).Read(new(null!), default));
        var tooMany = Enumerable.Range(0, 201).Select(_ => new ActionCenterReadItem("feedback", Guid.NewGuid())).ToArray();
        Assert.IsType<BadRequestObjectResult>(await Api(_student).Read(new(tooMany), default));
        Assert.IsType<NoContentResult>(await Api(_student).Read(new([new("feedback", Guid.Empty), new("gift", Guid.NewGuid())]), default));
        Assert.Empty(await _db.ActionReadReceipts.ToArrayAsync());
    }
    [Fact]
    public async Task Privacy_export_contains_only_the_callers_receipts()
    {
        var (notebook, page) = NotebookPage(_student); var comment = Comment(page, _teacher);
        await _db.SaveChangesAsync();
        await Api(_student).Read(new([new("notebook-comment", comment.Id)]), default);
        _db.ActionReadReceipts.Add(new() { UserSub = _peer, Kind = "feedback", ResourceId = Guid.NewGuid(), ReadAtUtc = _now });
        await _db.SaveChangesAsync();
        var privacy = new Mercantec.Tech.Api.Controllers.PrivacyController(_db) { ControllerContext = Api(_student).ControllerContext };
        var file = Assert.IsType<FileContentResult>(await privacy.Export(default));
        using var json = JsonDocument.Parse(file.FileContents);
        var own = Assert.Single(json.RootElement.GetProperty("PlatformRecords").GetProperty("actionReadReceipts").EnumerateArray());
        Assert.Equal(_student, own.GetProperty("UserSub").GetGuid());
        Assert.Equal(comment.Id, own.GetProperty("ResourceId").GetGuid());
    }
}
