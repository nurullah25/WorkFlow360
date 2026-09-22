using WorkFlow360.Application.Common;

namespace WorkFlow360.Tests;

public class TestCurrentUser : ICurrentUser
{
    public int? Id { get; set; }
    public int? EmployeeId { get; set; }
    public string? Role { get; set; }
    public string? IpAddress { get; set; } = "127.0.0.1";

    public bool IsAuthenticated => Id.HasValue;
    public int UserId => Id ?? throw new InvalidOperationException("No authenticated user.");

    public bool IsInRole(string role) => Role == role;
}
