using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WorkFlow360.Application.Auth;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public AccessToken CreateAccessToken(User user)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [AppClaimTypes.UserId] = user.Id.ToString(),
            [AppClaimTypes.Email] = user.Email,
            [AppClaimTypes.Name] = user.FullName,
            [AppClaimTypes.Role] = user.Role.Name
        };

        if (user.Employee != null)
            claims[AppClaimTypes.EmployeeId] = user.Employee.Id.ToString();

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expiresAt);
    }
}
