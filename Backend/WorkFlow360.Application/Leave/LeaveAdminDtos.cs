using FluentValidation;
using WorkFlow360.Application.Common;

namespace WorkFlow360.Application.Leave;

public record LeaveTypeDto(int Id, string Code, string Name, decimal DefaultDaysPerYear, bool IsPaid, bool IsActive);

public record SaveLeaveTypeRequest(string Code, string Name, decimal DefaultDaysPerYear, bool IsPaid, bool IsActive = true);

public record HolidayDto(int Id, DateOnly Date, string Name);

public record CreateHolidayRequest(DateOnly Date, string Name);

public class LeaveBalanceQuery : PagedQuery
{
    public int? Year { get; set; }
    public int? LeaveTypeId { get; set; }
}

public record EmployeeLeaveBalanceDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    string LeaveTypeName,
    int Year,
    decimal AllocatedDays,
    decimal UsedDays,
    decimal PendingDays);

public record UpdateLeaveBalanceRequest(decimal AllocatedDays);

public record GenerateLeaveBalancesRequest(int Year);

public record GenerateLeaveBalancesResult(int Year, int Created);

public class SaveLeaveTypeRequestValidator : AbstractValidator<SaveLeaveTypeRequest>
{
    public SaveLeaveTypeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(10)
            .Matches("^[A-Za-z0-9]+$").WithMessage("Code can only contain letters and numbers.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DefaultDaysPerYear).InclusiveBetween(0, 365).Must(BeWholeOrHalf)
            .WithMessage("Use whole or half days.");
    }

    internal static bool BeWholeOrHalf(decimal days) => days * 2 == decimal.Truncate(days * 2);
}

public class CreateHolidayRequestValidator : AbstractValidator<CreateHolidayRequest>
{
    public CreateHolidayRequestValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateLeaveBalanceRequestValidator : AbstractValidator<UpdateLeaveBalanceRequest>
{
    public UpdateLeaveBalanceRequestValidator()
    {
        RuleFor(x => x.AllocatedDays).InclusiveBetween(0, 365).Must(SaveLeaveTypeRequestValidator.BeWholeOrHalf)
            .WithMessage("Use whole or half days.");
    }
}

public class GenerateLeaveBalancesRequestValidator : AbstractValidator<GenerateLeaveBalancesRequest>
{
    public GenerateLeaveBalancesRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
    }
}
