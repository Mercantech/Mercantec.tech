using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Notebooks;

/// <summary>One policy for document bodies, metadata, search, links, collaboration and private media.</summary>
public sealed class NotebookAccess(AppDbContext db)
{
    public IQueryable<NotebookDocument> ReadableDocuments(ClaimsPrincipal user)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return db.NotebookDocuments.Where(d => false);
        if (UserClaims.IsTeacher(user)) return db.NotebookDocuments;
        return db.NotebookDocuments.Where(d => d.NotebookId != null &&
            (d.Notebook!.OwnerSub == sub || db.NotebookShares.Any(s => s.NotebookId == d.NotebookId
                && s.RecipientSub == sub && (s.PageId == null || s.PageId == d.Id))));
    }

    public Task<bool> CanReadAsync(Guid documentId, ClaimsPrincipal user, CancellationToken ct) =>
        ReadableDocuments(user).AnyAsync(d => d.Id == documentId, ct);

    public Task<bool> CanEditAsync(Guid documentId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return Task.FromResult(false);
        var teacher = UserClaims.IsTeacher(user);
        return db.NotebookDocuments.AnyAsync(d => d.Id == documentId &&
            (d.Kind == "template" ? teacher : d.NotebookId != null && d.Notebook!.OwnerSub == sub), ct);
    }

    public Task<bool> CanCommentAsync(Guid documentId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return Task.FromResult(false);
        var teacher = UserClaims.IsTeacher(user);
        return db.NotebookDocuments.AnyAsync(d => d.Id == documentId && d.NotebookId != null
            && (teacher || d.Notebook!.OwnerSub == sub), ct);
    }

    public Task<bool> CanShareNotebookAsync(Guid notebookId, ClaimsPrincipal user, CancellationToken ct)
    {
        if (UserClaims.GetSub(user) is not Guid sub) return Task.FromResult(false);
        return db.Notebooks.AnyAsync(n => n.Id == notebookId && n.OwnerSub == sub, ct);
    }
}
