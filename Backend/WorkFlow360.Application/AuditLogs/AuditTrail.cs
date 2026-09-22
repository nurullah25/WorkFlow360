using System.Text.Json;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.AuditLogs;

/// <summary>
/// Adds audit entries to the current DbContext without saving. The entry is committed by the caller's
/// SaveChangesAsync, so the audit row and the change it describes succeed or fail together.
/// </summary>
public class AuditTrail
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public AuditTrail(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public void Record(string action, string entityType, int? entityId, object? details = null, int? userId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? (_currentUser.IsAuthenticated ? _currentUser.UserId : null),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details == null ? null : JsonSerializer.Serialize(details),
            IpAddress = _currentUser.IpAddress,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        });
    }
}
