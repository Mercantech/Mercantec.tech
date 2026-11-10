namespace Mercantec.Tech.Contracts;

public sealed record CreateDrawing(string Domain, Guid DocumentId, string BlockId, Guid? SourceDrawingId = null);
public sealed record DrawingContext(Guid Id, string Domain, Guid DocumentId, string BlockId,
    bool CanRead, bool CanEditDrawing, bool CanManageDrawing, string Mode, long Revision, string? Fingerprint,
    long AccessVersion, string? StateBase64, string? SceneJson, Guid? SnapshotId,
    string? PreviewLightUrl, string? PreviewDarkUrl, DrawingFileView[] Files, DateTimeOffset UpdatedAtUtc, long LifecycleVersion = 0);
public sealed record DrawingFileView(Guid Id, string MimeType, long SizeBytes, DateTimeOffset Created, string ContentUrl);
public sealed record UpdateDrawingSettings(string Mode, long ExpectedAccessVersion);
public sealed record DrawingGrantView(Guid Id, Guid? UserSub, Guid? HoldId, string DisplayName);
public sealed record SetDrawingGrant(Guid? UserSub, Guid? HoldId);
public sealed record DrawingCandidate(Guid Id, string DisplayName);
public sealed record DrawingCandidates(DrawingCandidate[] People, DrawingCandidate[] Holds);
public sealed record DrawingCollaborationAccess(Guid DocumentId, bool CanRead, bool CanEdit, long AccessVersion,
    string? EditorName = null, long LifecycleVersion = 0);
public sealed record AuthorizeDrawingCollaboration(string DocumentName);
public sealed record StoreDrawingDocument(string StateBase64, string Fingerprint, string SceneJson, Guid[] AssetIds,
    string? EditorName = null, bool PreserveEditMetadata = false, long LifecycleVersion = 0);
public sealed record DrawingStored(DateTimeOffset SavedAtUtc, long Revision, string Fingerprint, long AccessVersion);
public sealed record DrawingPreviews(long Revision, string Fingerprint, string PreviewLightUrl, string PreviewDarkUrl);
public sealed record DrawingPublicationRequest(Guid DrawingId, string Mode, long ExpectedRevision, string ExpectedFingerprint);
public sealed record DrawingPublicationBinding(Guid DrawingId, string BlockId, string Mode, Guid? SnapshotId,
    long Revision, string Fingerprint);
public sealed record DrawingCloneResult(Dictionary<string, string> DrawingMap);
