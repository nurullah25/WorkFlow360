using FluentValidation;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Tasks;

public class TaskQuery : PagedQuery
{
    public int? ProjectId { get; set; }
    public int? AssigneeId { get; set; }
    public bool AssignedToMe { get; set; }
    public ProjectTaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public bool OpenOnly { get; set; }
    public bool OverdueOnly { get; set; }
}

public record TaskListItemDto(
    int Id,
    string Title,
    int ProjectId,
    string ProjectCode,
    int? AssigneeId,
    string? AssigneeName,
    TaskPriority Priority,
    ProjectTaskStatus Status,
    DateOnly? DueDate,
    bool IsOverdue,
    DateTime LastUpdatedAt);

public record TaskDetailsDto(
    int Id,
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    string Title,
    string? Description,
    int? AssigneeId,
    string? AssigneeName,
    TaskPriority Priority,
    ProjectTaskStatus Status,
    DateOnly? DueDate,
    DateTime? CompletedAt,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsOverdue,
    bool CanEdit,
    IReadOnlyList<ProjectTaskStatus> AllowedStatuses);

public record TaskCommentDto(int Id, int AuthorUserId, string AuthorName, string Body, DateTime CreatedAt);

public record TaskHistoryDto(int Id, string FieldName, string? OldValue, string? NewValue, string ChangedByName, DateTime ChangedAt);

public record CreateTaskRequest(
    int ProjectId,
    string Title,
    string? Description,
    int? AssigneeId,
    TaskPriority Priority,
    DateOnly? DueDate);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    int? AssigneeId,
    TaskPriority Priority,
    DateOnly? DueDate);

public record ChangeTaskStatusRequest(ProjectTaskStatus Status);

public record AddTaskCommentRequest(string Body);

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public class ChangeTaskStatusRequestValidator : AbstractValidator<ChangeTaskStatusRequest>
{
    public ChangeTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class AddTaskCommentRequestValidator : AbstractValidator<AddTaskCommentRequest>
{
    public AddTaskCommentRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}
