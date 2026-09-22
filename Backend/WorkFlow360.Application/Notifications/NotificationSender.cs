using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Notifications;

/// <summary>
/// Queues in-app notifications on the current DbContext; they are saved with the caller's change.
/// People are never notified about their own actions.
/// </summary>
public class NotificationSender
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public NotificationSender(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public void Notify(IEnumerable<int?> userIds, NotificationType type, string title, string message, string? linkUrl = null)
    {
        var actorId = _currentUser.IsAuthenticated ? _currentUser.UserId : (int?)null;
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var userId in userIds.OfType<int>().Distinct().Where(id => id != actorId))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                CreatedAt = now
            });
        }
    }

    public void Notify(int? userId, NotificationType type, string title, string message, string? linkUrl = null) =>
        Notify([userId], type, title, message, linkUrl);

    /// <summary>Employees without a user account simply don't get in-app notifications.</summary>
    public async Task<int?> GetUserIdAsync(int? employeeId, CancellationToken cancellationToken)
    {
        if (employeeId == null)
            return null;

        return await _db.Employees
            .Where(e => e.Id == employeeId)
            .Select(e => e.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
