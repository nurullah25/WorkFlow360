using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WorkFlow360.API.Extensions;
using WorkFlow360.Application.Auth;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "wf360_refresh";
    private const string RefreshCookiePath = "/api/auth";

    private readonly AuthService _authService;
    private readonly ICurrentUser _currentUser;

    public AuthController(AuthService authService, ICurrentUser currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        SetRefreshCookie(result);
        return Ok(result.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized();

        try
        {
            var result = await _authService.RefreshAsync(refreshToken, cancellationToken);
            SetRefreshCookie(result);
            return Ok(result.Response);
        }
        catch (AuthenticationFailedException)
        {
            DeleteRefreshCookie();
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await _authService.LogoutAsync(refreshToken, cancellationToken);

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me(CancellationToken cancellationToken)
    {
        return Ok(await _authService.GetUserAsync(_currentUser.UserId, cancellationToken));
    }

    private void SetRefreshCookie(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, CreateCookieOptions(result.RefreshTokenExpiresAt));
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions(expires: null));
    }

    // Path limits the cookie to the auth endpoints; SameSite=Strict stops other sites from triggering a refresh.
    private static CookieOptions CreateCookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = RefreshCookiePath,
        Expires = expires
    };
}
