using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Auth;

public class AuthService
{
    // Two browser tabs can refresh with the same cookie at almost the same moment. A rotated token
    // presented again inside this window is treated as that race, not as a stolen token.
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly AuditTrail _auditTrail;
    private readonly TimeProvider _clock;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        AuditTrail auditTrail,
        TimeProvider clock,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _auditTrail = auditTrail;
        _clock = clock;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user == null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            _auditTrail.Record(AuditActions.LoginFailed, nameof(User), user?.Id, new { email }, user?.Id);
            await _db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException("Invalid email or password.");
        }

        if (!user.IsActive)
            throw new AuthenticationFailedException("Your account has been deactivated. Please contact an administrator.");

        var now = UtcNow();
        user.LastLoginAt = now;

        var result = IssueTokens(user, now);
        _auditTrail.Record(AuditActions.Login, nameof(User), user.Id, userId: user.Id);
        await _db.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);

        var stored = await _db.RefreshTokens
            .Include(t => t.User).ThenInclude(u => u.Role)
            .Include(t => t.User).ThenInclude(u => u.Employee)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored == null)
            throw new AuthenticationFailedException("Invalid session. Please sign in again.");

        var now = UtcNow();

        if (stored.RevokedAt != null)
        {
            var isConcurrentRefresh = stored.ReplacedByTokenHash != null && now - stored.RevokedAt < ReuseGracePeriod;
            if (!isConcurrentRefresh)
            {
                _logger.LogWarning("Revoked refresh token was reused for user {UserId}. Revoking all sessions.", stored.UserId);
                await _db.RevokeAllSessionsAsync(stored.UserId, now, cancellationToken);
                _auditTrail.Record(AuditActions.SessionsRevoked, nameof(User), stored.UserId, new { reason = "Refresh token reuse" }, stored.UserId);
                await _db.SaveChangesAsync(cancellationToken);
            }

            throw new AuthenticationFailedException("Your session has expired. Please sign in again.");
        }

        if (stored.ExpiresAt <= now || !stored.User.IsActive)
            throw new AuthenticationFailedException("Your session has expired. Please sign in again.");

        var result = IssueTokens(stored.User, now, replacing: stored);
        await _db.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored == null || stored.RevokedAt != null)
            return;

        stored.RevokedAt = UtcNow();
        _auditTrail.Record(AuditActions.Logout, nameof(User), stored.UserId, userId: stored.UserId);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthUserDto> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new AuthUserDto(u.Id, u.Email, u.FullName, u.Role.Name, u.Employee != null ? u.Employee.Id : null))
            .FirstOrDefaultAsync(cancellationToken);

        return user ?? throw new NotFoundException(nameof(User), userId);
    }

    private AuthResult IssueTokens(User user, DateTime now, RefreshToken? replacing = null)
    {
        var accessToken = _tokenGenerator.CreateAccessToken(user);

        var refreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var refreshTokenHash = HashToken(refreshToken);
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        });

        if (replacing != null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByTokenHash = refreshTokenHash;
        }

        var userDto = new AuthUserDto(user.Id, user.Email, user.FullName, user.Role.Name, user.Employee?.Id);
        var response = new AuthResponse(accessToken.Token, accessToken.ExpiresAt, userDto);

        return new AuthResult(response, refreshToken, refreshExpiresAt);
    }

    // Only the hash is stored, so a leaked database backup can't be used to hijack sessions.
    private static string HashToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private DateTime UtcNow() => _clock.GetUtcNow().UtcDateTime;
}
