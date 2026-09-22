using FluentValidation;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Projects;

public class ProjectQuery : PagedQuery
{
    public ProjectStatus? Status { get; set; }
}

public record ProjectListItemDto(
    int Id,
    string Code,
    string Name,
    string ManagerName,
    ProjectStatus Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    int MemberCount,
    int TaskCount,
    int DoneTaskCount,
    int OverdueTaskCount);

public record ProjectMemberDto(
    int EmployeeId,
    string FullName,
    string EmployeeCode,
    string DesignationTitle,
    string? RoleInProject,
    DateTime AddedAt);

public record TaskSummaryDto(int Todo, int InProgress, int InReview, int Done, int Cancelled, int Overdue);

public record ProjectDetailsDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int ManagerId,
    string ManagerName,
    DateOnly StartDate,
    DateOnly? EndDate,
    ProjectStatus Status,
    IReadOnlyList<ProjectMemberDto> Members,
    TaskSummaryDto Tasks,
    bool CanManage);

public record SaveProjectRequest(
    string Code,
    string Name,
    string? Description,
    int ManagerId,
    DateOnly StartDate,
    DateOnly? EndDate,
    ProjectStatus Status);

public record AddProjectMemberRequest(int EmployeeId, string? RoleInProject);

public class SaveProjectRequestValidator : AbstractValidator<SaveProjectRequest>
{
    public SaveProjectRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Code can only contain letters, numbers and hyphens.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ManagerId).GreaterThan(0).WithMessage("Project manager is required.");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("End date can't be before the start date.");
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class AddProjectMemberRequestValidator : AbstractValidator<AddProjectMemberRequest>
{
    public AddProjectMemberRequestValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.RoleInProject).MaximumLength(50);
    }
}
