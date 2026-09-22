namespace WorkFlow360.Application.Common;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Throws if there is no authenticated user; check <see cref="IsAuthenticated"/> first when that is possible.</summary>
    int UserId { get; }

    int? EmployeeId { get; }
    string? Role { get; }
    string? IpAddress { get; }

    bool IsInRole(string role);
}
