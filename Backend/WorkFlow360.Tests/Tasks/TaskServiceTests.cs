using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Tasks;

public class TaskServiceTests : ProjectTestContext
{
    private TaskService AsManager => TaskServiceFor(ActingAs(Manager, RoleNames.Manager));
    private TaskService AsDeveloper => TaskServiceFor(ActingAs(Developer));

    private Task<TaskDetailsDto> CreateTaskAsync(int? assigneeId) =>
        AsManager.CreateAsync(
            new CreateTaskRequest(Project.Id, "Build leave API", null, assigneeId, TaskPriority.High, new DateOnly(2026, 3, 20)),
            default);

    [Fact]
    public async Task Create_WithAssignee_NotifiesThemAndRecordsHistory()
    {
        var task = await CreateTaskAsync(Developer.Id);

        using var verify = Database.CreateVerificationContext();
        var notification = await verify.Notifications.SingleAsync();
        Assert.Equal(Developer.UserId, notification.UserId);
        Assert.Equal(NotificationType.TaskAssigned, notification.Type);
        Assert.Equal($"/tasks/{task.Id}", notification.LinkUrl);
        Assert.Contains(await verify.TaskHistory.ToListAsync(), h => h.TaskId == task.Id && h.FieldName == "Created");
    }

    [Fact]
    public async Task Create_AssignedToSomeoneOutsideTheProject_ThrowsBusinessRule()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateTaskAsync(Outsider.Id));
    }

    [Fact]
    public async Task Create_ByProjectMemberWhoIsNotTheManager_ThrowsForbidden()
    {
        var request = new CreateTaskRequest(Project.Id, "Sneaky task", null, null, TaskPriority.Low, null);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => AsDeveloper.CreateAsync(request, default));
    }

    [Fact]
    public async Task ChangeStatus_AssigneeCompletesTask_SetsCompletedAtAndNotifiesManager()
    {
        var task = await CreateTaskAsync(Developer.Id);
        await AsDeveloper.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(ProjectTaskStatus.InProgress), default);

        var done = await AsDeveloper.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(ProjectTaskStatus.Done), default);

        Assert.NotNull(done.CompletedAt);
        Assert.Empty(done.AllowedStatuses);

        using var verify = Database.CreateVerificationContext();
        Assert.Contains(await verify.Notifications.ToListAsync(),
            n => n.UserId == Manager.UserId && n.Type == NotificationType.TaskStatusChanged);
    }

    [Fact]
    public async Task ChangeStatus_AssigneeCancellingTheTask_ThrowsForbidden()
    {
        var task = await CreateTaskAsync(Developer.Id);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => AsDeveloper.ChangeStatusAsync(task.Id, new ChangeTaskStatusRequest(ProjectTaskStatus.Cancelled), default));
    }

    [Fact]
    public async Task Get_ForSomeoneOutsideTheProject_ThrowsNotFound()
    {
        var task = await CreateTaskAsync(Developer.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => TaskServiceFor(ActingAs(Outsider)).GetAsync(task.Id, default));
    }
}
