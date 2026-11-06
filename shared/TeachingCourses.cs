namespace Mercantec.Tech.Contracts;

public sealed record TeachingCourseWorkspace(Guid HoldId, bool CanManage, TeachingCourseView[] Courses);
public sealed record TeachingCourseView(Guid Id, Guid HoldId, string Title, string Description,
    string LearningObjectives, DateOnly? StartsOn, DateOnly? EndsOn, bool Published, bool Archived,
    int Version, TeachingCourseResourceView[] Resources);
public sealed record TeachingCourseResourceView(Guid ResourceId, string Kind, string Title, bool Published,
    Guid? ParentPageId = null, Guid? FileAssetId = null, DateTimeOffset? StartsAtUtc = null,
    DateTimeOffset? EndsAtUtc = null, Guid? LinkedCourseId = null);
public sealed record SaveTeachingCourse(string Title, string Description, string LearningObjectives,
    DateOnly? StartsOn, DateOnly? EndsOn, bool Published = false, bool Archived = false, int Version = 0);
public sealed record TeachingCourseResourceReference(string Kind, Guid ResourceId);
public sealed record SaveTeachingCourseResources(int Version, TeachingCourseResourceReference[] Resources);
public sealed record CreateTeachingCoursePage(string Title, Guid? ParentPageId = null, int Version = 0);
public sealed record TeachingCoursePageCreated(Guid PageId, TeachingCourseView Course);
