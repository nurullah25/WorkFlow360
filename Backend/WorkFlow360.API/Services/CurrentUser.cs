using System.Security.Claims;
using WorkFlow360.Application.Auth;
using WorkFlow360.Application.Common;

namespace WorkFlow360.API.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int UserId =>
        int.TryParse(Principal?.FindFirstValue(AppClaimTypes.UserId), out var id)
            ? id
            : throw new InvalidOperationException("There is no authenticated user for this request.");

    public int? EmployeeId =>
        int.TryParse(Principal?.FindFirstValue(AppClaimTypes.EmployeeId), out var id) ? id : null;

    public string? Role => Principal?.FindFirstValue(AppClaimTypes.Role);

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
