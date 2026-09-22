using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace WorkFlow360.API.Extensions;

public static class RateLimitPolicies
{
    public const string Login = "login";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Slows down password guessing: 10 login attempts per minute per client IP.
            options.AddPolicy(Login, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1)
                }));
        });

        return services;
    }
}
