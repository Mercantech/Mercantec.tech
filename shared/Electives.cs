namespace Mercantec.Tech.Contracts;

public sealed record ElectiveWorkspace(bool CanManage, ElectiveView[] Electives);

public sealed record ElectiveView(Guid Id, string Title, string Description, string Schedule,
    DateOnly? StartsOn, DateOnly? EndsOn, DateOnly? RegistrationOpensOn, DateOnly? RegistrationClosesOn,
    int? Capacity, int ReservedSeats, string Status, string? SourceUrl, Guid? TemplateId, int Version,
    int EnrollmentCount, bool IsEnrolled, bool CanEnroll, bool CanWithdraw);

public sealed record SaveElective(string Title, string Description, string Schedule,
    DateOnly? StartsOn, DateOnly? EndsOn, DateOnly? RegistrationOpensOn, DateOnly? RegistrationClosesOn,
    int? Capacity, int ReservedSeats, string Status, string? SourceUrl, Guid? TemplateId, int Version);

public sealed record EnrollInElective(bool HasEmployerAgreement);

public sealed record ElectiveParticipant(Guid UserSub, string DisplayName, DateTimeOffset EnrolledAtUtc);
