using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Auth;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Infrastructure.Auth;

namespace WorkFlow360.Tests.Auth;

public class AuthServiceTests : IDisposable
{
    private const string Email = "rafiq.hasan@test.local";
    private const string Password = "Secret123!";

    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly AuthService _authService;
    private readonly User _user;

    public AuthServiceTests()
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "WorkFlow360.Tests",
            Audience = "WorkFlow360.Tests",
            SigningKey = "test-signing-key-that-is-long-enough-for-hs256",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

        var db = _database.Context;
        var hasher = new IdentityPasswordHasher();

        _user = new User
        {
            Email = Email,
            FullName = "Rafiq Hasan",
            PasswordHash = hasher.Hash(Password),
            RoleId = 4
        };
        db.Users.Add(_user);
        db.SaveChanges();

        _authService = new AuthService(
            db,
            hasher,
            new JwtTokenGenerator(jwtOptions, _clock),
            new AuditTrail(db, new TestCurrentUser(), _clock),
            _clock,
            jwtOptions,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndStoresOnlyTheRefreshTokenHash()
    {
        var result = await _authService.LoginAsync(new LoginRequest(" Rafiq.Hasan@TEST.local ", Password), default);

        Assert.False(string.IsNullOrEmpty(result.Response.AccessToken));
        Assert.Equal("Employee", result.Response.User.Role);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddDays(7), result.RefreshTokenExpiresAt);

        using var verify = _database.CreateVerificationContext();
        var stored = await verify.RefreshTokens.SingleAsync();
        Assert.NotEqual(result.RefreshToken, stored.TokenHash);
        Assert.Contains(await verify.AuditLogs.ToListAsync(), a => a.Action == AuditActions.Login && a.UserId == _user.Id);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsAndRecordsFailedAttempt()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _authService.LoginAsync(new LoginRequest(Email, "wrong-password"), default));

        using var verify = _database.CreateVerificationContext();
        Assert.Contains(await verify.AuditLogs.ToListAsync(), a => a.Action == AuditActions.LoginFailed);
        Assert.Empty(await verify.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Login_WhenAccountIsDeactivated_Throws()
    {
        _user.IsActive = false;
        await _database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _authService.LoginAsync(new LoginRequest(Email, Password), default));
    }

    [Fact]
    public async Task Refresh_IssuesNewTokenAndRevokesTheOldOne()
    {
        var login = await _authService.LoginAsync(new LoginRequest(Email, Password), default);

        var refreshed = await _authService.RefreshAsync(login.RefreshToken, default);

        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        using var verify = _database.CreateVerificationContext();
        var tokens = await verify.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.RevokedAt == null);
    }

    [Fact]
    public async Task Refresh_WithOldTokenInsideGracePeriod_FailsButKeepsTheNewSession()
    {
        var login = await _authService.LoginAsync(new LoginRequest(Email, Password), default);
        var refreshed = await _authService.RefreshAsync(login.RefreshToken, default);

        _clock.Advance(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _authService.RefreshAsync(login.RefreshToken, default));
        await _authService.RefreshAsync(refreshed.RefreshToken, default);
    }

    [Fact]
    public async Task Refresh_WithOldTokenAfterGracePeriod_RevokesEverySessionForTheUser()
    {
        var laptop = await _authService.LoginAsync(new LoginRequest(Email, Password), default);
        var phone = await _authService.LoginAsync(new LoginRequest(Email, Password), default);
        await _authService.RefreshAsync(laptop.RefreshToken, default);

        _clock.Advance(TimeSpan.FromMinutes(5));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _authService.RefreshAsync(laptop.RefreshToken, default));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _authService.RefreshAsync(phone.RefreshToken, default));

        using var verify = _database.CreateVerificationContext();
        Assert.All(await verify.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        Assert.Contains(await verify.AuditLogs.ToListAsync(), a => a.Action == AuditActions.SessionsRevoked);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_Throws()
    {
        var login = await _authService.LoginAsync(new LoginRequest(Email, Password), default);

        _clock.Advance(TimeSpan.FromDays(8));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => _authService.RefreshAsync(login.RefreshToken, default));
    }

    public void Dispose() => _database.Dispose();
}
