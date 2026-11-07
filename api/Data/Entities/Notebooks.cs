namespace Mercantec.Tech.Api.Data.Entities;

public sealed class Notebook
{
    public Guid Id { get; set; }
    public Guid OwnerSub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class NotebookProfile
{
    public Guid Sub { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsTeacher { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

/// <summary>Canonical Yjs state, shared by personal pages and teacher-owned template drafts.</summary>
public sealed class NotebookDocument
{
    public Guid Id { get; set; }
    public Guid? NotebookId { get; set; }
    public Notebook? Notebook { get; set; }
    public string Kind { get; set; } = "note";
    public string Title { get; set; } = "";
    public Guid? ParentPageId { get; set; }
    public string TagsJson { get; set; } = "[]";
    public DateOnly? PeriodStart { get; set; }
    public Guid? HoldId { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? TemplateVersionId { get; set; }
    public Guid? RequestId { get; set; }
    public string? PeriodKey { get; set; }
    public byte[]? YjsState { get; set; }
    public long Revision { get; set; }
    public string? Fingerprint { get; set; }
    public string SearchText { get; set; } = "";
    public string AssetIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class NotebookShare
{
    public Guid Id { get; set; }
    public Guid NotebookId { get; set; }
    public Guid? PageId { get; set; }
    public Guid RecipientSub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class NotebookComment
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public Guid AuthorSub { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class NotebookLink
{
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
}

public sealed class NotebookTemplate
{
    public Guid Id { get; set; }
    public string Cadence { get; set; } = "daily";
    public bool IsActive { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public int Version { get; set; }
}

public sealed class NotebookTemplateVersion
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Cadence { get; set; } = "daily";
    public byte[] State { get; set; } = [];
    public string AssetIdsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class NotebookAsset
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    // Several page-owned grants can reference one immutable object copied from a template.
    public string Key { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Kind { get; set; } = "download";
    public long SizeBytes { get; set; }
    public Guid UploadedBySub { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
