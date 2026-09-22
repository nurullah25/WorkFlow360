using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Notifications;

public class NotificationServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly User _alice;
    private readonly User _bob;

    public NotificationServiceTests()
    {
        var db = _database.Context;
        _alice = TestData.AddUser(db, "alice@test.local");
        _bob = TestData.AddUser(db, "bob@test.local");

        foreach (var (user, title) in new[] { (_alice, "A1"), (_alice, "A2"), (_bob, "B1") })
        {
            db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = NotificationType.TaskAssigned,
                Title = title,
                Message = title,
                CreatedAt = DateTime.UtcNow
            });
        }
        db.SaveChanges();
    }

    private NotificationService ServiceFor(User user) =>
        new(_database.Context, new TestCurrentUser { Id = user.Id });

    [Fact]
    public async Task List_ReturnsOnlyTheCurrentUsersNotifications()
    {
        var result = await ServiceFor(_alice).ListAsync(new NotificationQuery(), default);

        Assert.Equal(["A1", "A2"], result.Items.Select(n => n.Title).Order());
    }

    [Fact]
    public async Task MarkRead_SomeoneElsesNotification_ThrowsNotFound()
    {
        var bobsNotification = await _database.Context.Notifications.SingleAsync(n => n.UserId == _bob.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => ServiceFor(_alice).MarkReadAsync(bobsNotification.Id, default));
    }

    [Fact]
    public async Task MarkAllRead_LeavesOtherUsersUntouched()
    {
        await ServiceFor(_alice).MarkAllReadAsync(default);

        Assert.Equal(0, (await ServiceFor(_alice).GetUnreadCountAsync(default)).Count);
        Assert.Equal(1, (await ServiceFor(_bob).GetUnreadCountAsync(default)).Count);
    }

    public void Dispose() => _database.Dispose();
}
