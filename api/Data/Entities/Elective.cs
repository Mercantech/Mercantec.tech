namespace Mercantec.Tech.Api.Data.Entities;

public sealed class Elective
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Schedule { get; set; } = "";
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public DateOnly? RegistrationOpensOn { get; set; }
    public DateOnly? RegistrationClosesOn { get; set; }
    public int? Capacity { get; set; }
    public int ReservedSeats { get; set; }
    public string Status { get; set; } = "draft";
    public string? SourceUrl { get; set; }
    public Guid? TemplateId { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<ElectiveEnrollment> Enrollments { get; set; } = [];
}

public sealed class ElectiveEnrollment
{
    public Guid ElectiveId { get; set; }
    public Elective Elective { get; set; } = null!;
    public Guid UserSub { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset EnrolledAtUtc { get; set; }
}
