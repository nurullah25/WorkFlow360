using FluentValidation;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Employees;

public class EmployeeQuery : PagedQuery
{
    public int? DepartmentId { get; set; }
    public int? ManagerId { get; set; }
    public EmploymentStatus? Status { get; set; }

    /// <summary>Resigned and terminated employees are hidden unless asked for.</summary>
    public bool IncludeFormer { get; set; }
}

public record EmployeeListItemDto(
    int Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string DepartmentName,
    string DesignationTitle,
    string? ManagerName,
    DateOnly JoiningDate,
    EmploymentStatus Status);

public record EmployeeDetailsDto(
    int Id,
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int DepartmentId,
    string DepartmentName,
    int DesignationId,
    string DesignationTitle,
    int? ManagerId,
    string? ManagerName,
    DateOnly JoiningDate,
    EmploymentStatus Status,
    int? UserId,
    int DirectReportCount);

public record EmployeeLookupDto(int Id, string FullName, string EmployeeCode, string Email);

public record SaveEmployeeRequest(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    int DepartmentId,
    int DesignationId,
    int? ManagerId,
    DateOnly JoiningDate,
    EmploymentStatus Status);

public class SaveEmployeeRequestValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeRequestValidator()
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Employee code can only contain letters, numbers and hyphens.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleFor(x => x.DesignationId).GreaterThan(0).WithMessage("Designation is required.");
        RuleFor(x => x.JoiningDate).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
