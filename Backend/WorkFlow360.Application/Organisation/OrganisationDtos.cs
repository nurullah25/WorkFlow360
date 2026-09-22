using FluentValidation;

namespace WorkFlow360.Application.Organisation;

public record DepartmentDto(int Id, string Code, string Name, bool IsActive, int EmployeeCount);

public record DesignationDto(int Id, string Title, bool IsActive, int EmployeeCount);

public record SaveDepartmentRequest(string Code, string Name, bool IsActive = true);

public record SaveDesignationRequest(string Title, bool IsActive = true);

public class SaveDepartmentRequestValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Code can only contain letters, numbers and hyphens.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class SaveDesignationRequestValidator : AbstractValidator<SaveDesignationRequest>
{
    public SaveDesignationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
    }
}
