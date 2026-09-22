using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common;

namespace WorkFlow360.Application.Auth;

public static class SessionExtensions
{
    /// <summary>Marks every active refresh token for the user as revoked. Saved by the caller.</summary>
    public static async Task RevokeAllSessionsAsync(this IAppDbContext db, int userId, DateTime now, CancellationToken cancellationToken)
    {
        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.RevokedAt = now;
    }
}
