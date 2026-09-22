namespace WorkFlow360.Application.Auth;

public record LoginRequest(string Email, string Password);

public record AuthUserDto(int Id, string Email, string FullName, string Role, int? EmployeeId);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, AuthUserDto User);

/// <summary>The refresh token is kept out of <see cref="AuthResponse"/> because it goes into an HttpOnly cookie, not the response body.</summary>
public record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);
