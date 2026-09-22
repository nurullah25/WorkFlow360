using Microsoft.AspNetCore.Identity;
using WorkFlow360.Domain.Entities;
using IPasswordHasher = WorkFlow360.Application.Common.IPasswordHasher;

namespace WorkFlow360.Infrastructure.Auth;

/// <summary>
/// Uses ASP.NET Core Identity's PBKDF2 hasher without pulling in the rest of the Identity schema.
/// </summary>
public class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    // The default Identity hasher never reads the user argument, so there is nothing meaningful to pass.
    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
}
