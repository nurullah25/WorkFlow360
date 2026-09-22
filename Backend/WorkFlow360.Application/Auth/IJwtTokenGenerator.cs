using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Auth;

public interface IJwtTokenGenerator
{
    /// <summary>Expects <see cref="User.Role"/> (and <see cref="User.Employee"/> when linked) to be loaded.</summary>
    AccessToken CreateAccessToken(User user);
}

public record AccessToken(string Token, DateTime ExpiresAt);
