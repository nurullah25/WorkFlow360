using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Notifications;

public class NotificationQuery : PagedQuery
{
    public bool UnreadOnly { get; set; }
}

public record NotificationDto(
    int Id,
    NotificationType Type,
    string Title,
    string Message,
    string? LinkUrl,
    bool IsRead,
    DateTime CreatedAt);

public record UnreadCountDto(int Count);

/// <summary>Reading side of in-app notifications. Everyone only ever sees their own.</summary>
public class NotificationService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public NotificationService(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(NotificationQuery query, CancellationToken cancellationToken)
    {
        var notifications = Mine().AsNoTracking();
        if (query.UnreadOnly)
            notifications = notifications.Where(n => !n.IsRead);

        return await notifications
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.LinkUrl, n.IsRead, n.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    /// <summary>Polled by every open browser tab, so it's a single indexed COUNT (UserId, IsRead).</summary>
    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken) =>
        new(await Mine().CountAsync(n => !n.IsRead, cancellationToken));

    public async Task MarkReadAsync(int id, CancellationToken cancellationToken)
    {
        var notification = await Mine().FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), id);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var unread = await Mine().Where(n => !n.IsRead).ToListAsync(cancellationToken);
        foreach (var notification in unread)
            notification.IsRead = true;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Notification> Mine()
    {
        var userId = _currentUser.UserId;
        return _db.Notifications.Where(n => n.UserId == userId);
    }
}
