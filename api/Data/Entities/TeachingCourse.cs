namespace Mercantec.Tech.Api.Data.Entities;

/// <summary>A teaching sequence inside a hold, independent of its legacy 1:1 Forloeb shell.</summary>
public sealed class TeachingCourse
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string LearningObjectives { get; set; } = "";
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool Published { get; set; }
    public bool Archived { get; set; }
    public int Version { get; set; }
    public ICollection<TeachingCourseResource> Resources { get; set; } = new List<TeachingCourseResource>();
}

public sealed class TeachingCourseResource
{
    public Guid CourseId { get; set; }
    public TeachingCourse Course { get; set; } = null!;
    public string Kind { get; set; } = "";
    public Guid ResourceId { get; set; }
}
