using FluentValidation;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Leave;

public static class LeaveScopes
{
    /// <summary>The signed-in employee's own requests.</summary>
    public const string Mine = "mine";

    /// <summary>Pending requests the signed-in user is allowed to approve.</summary>
    public const string Approvals = "approvals";

    /// <summary>Everyone's requests for Admin/HR, direct reports' requests for managers.</summary>
    public const string Team = "team";
}

public class LeaveRequestQuery : PagedQuery
{
    public string Scope { get; set; } = LeaveScopes.Mine;
    public LeaveStatus? Status { get; set; }
    public int? LeaveTypeId { get; set; }
    public int? Year { get; set; }
}

public record LeaveRequestDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    int LeaveTypeId,
    string LeaveTypeName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalDays,
    string Reason,
    LeaveStatus Status,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewComment,
    DateTime CreatedAt,
    bool CanReview,
    bool CanCancel);

public record LeaveBalanceDto(
    int Id,
    int LeaveTypeId,
    string LeaveTypeCode,
    string LeaveTypeName,
    int Year,
    decimal AllocatedDays,
    decimal UsedDays,
    decimal PendingDays,
    decimal AvailableDays);

public record LeavePreviewDto(int WorkingDays, decimal? AvailableDays);

public record SubmitLeaveRequest(int LeaveTypeId, DateOnly StartDate, DateOnly EndDate, string Reason);

public record ApproveLeaveRequest(string? Comment);

public record RejectLeaveRequest(string Comment);

public class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequest>
{
    public SubmitLeaveRequestValidator()
    {
        RuleFor(x => x.LeaveTypeId).GreaterThan(0).WithMessage("Leave type is required.");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("End date can't be before the start date.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class ApproveLeaveRequestValidator : AbstractValidator<ApproveLeaveRequest>
{
    public ApproveLeaveRequestValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public class RejectLeaveRequestValidator : AbstractValidator<RejectLeaveRequest>
{
    public RejectLeaveRequestValidator()
    {
        RuleFor(x => x.Comment).NotEmpty().WithMessage("Please give the employee a reason.").MaximumLength(500);
    }
}
