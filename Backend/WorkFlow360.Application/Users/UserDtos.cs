using FluentValidation;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Users;

public class UserQuery : PagedQuery
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public record UserListItemDto(
    int Id,
    string Email,
    string FullName,
    string Role,
    bool IsActive,
    DateTime? LastLoginAt,
    int? EmployeeId,
    string? EmployeeCode);

public record CreateUserRequest(string Email, string FullName, string Role, string Password, int? EmployeeId);

public record UpdateUserRequest(string FullName, string Role, bool IsActive);

public record ResetPasswordRequest(string NewPassword);

public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.");
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).Must(RoleNames.All.Contains).WithMessage("Unknown role.");
        RuleFor(x => x.Password).StrongPassword();
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).Must(RoleNames.All.Contains).WithMessage("Unknown role.");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}
