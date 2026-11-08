namespace Mercantec.Tech.Contracts;

public sealed record EntityReference(string Kind, string Id)
{
    public string Key => Kind + ":" + Id;
}
public sealed record ResolvedReference(EntityReference Reference, string Title, string Href, string ContextLabel,
    Guid? HoldId = null, Guid? CollectionId = null, bool CanRename = false, string? MentionStatus = null);
public sealed record ReferenceSearchResult(ResolvedReference[] Items, int Total);
public sealed record ResolveReferences(EntityReference[] References, string? SourceKind = null, string? SourceId = null,
    bool PublishedOnly = false);
public sealed record ReferenceGraphRequest(EntityReference? Focus = null, string? ScopeKind = null,
    string? ScopeId = null, int Offset = 0, int Limit = 400, bool PublishedOnly = false);
public sealed record ReferenceGraphNode(string Key, ResolvedReference Entity);
public sealed record ReferenceGraphEdge(string FromKey, string ToKey);
public sealed record EntityGraph(ReferenceGraphNode[] Nodes, ReferenceGraphEdge[] Edges, int Total, bool HasMore);
public sealed record ReferencePerson(Guid Sub, string DisplayName, bool IsTeacher, ResolvedReference[] Memberships);
