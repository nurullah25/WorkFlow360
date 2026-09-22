using WorkFlow360.Domain.Enums;
using WorkFlow360.Domain.Rules;

namespace WorkFlow360.Tests.Tasks;

public class TaskWorkflowTests
{
    [Fact]
    public void Assignee_CanMoveAnOpenTaskForwardButCannotCancelIt()
    {
        var allowed = TaskWorkflow.AllowedTransitions(ProjectTaskStatus.InProgress, isProjectManager: false);

        Assert.Contains(ProjectTaskStatus.InReview, allowed);
        Assert.Contains(ProjectTaskStatus.Done, allowed);
        Assert.DoesNotContain(ProjectTaskStatus.Cancelled, allowed);
    }

    [Theory]
    [InlineData(ProjectTaskStatus.Done)]
    [InlineData(ProjectTaskStatus.Cancelled)]
    public void Assignee_CannotReopenClosedTasks(ProjectTaskStatus closedStatus)
    {
        Assert.Empty(TaskWorkflow.AllowedTransitions(closedStatus, isProjectManager: false));
    }

    [Fact]
    public void ProjectManager_CanReopenADoneTask()
    {
        Assert.Contains(ProjectTaskStatus.InProgress, TaskWorkflow.AllowedTransitions(ProjectTaskStatus.Done, isProjectManager: true));
    }

    [Fact]
    public void Todo_CannotJumpStraightToDone()
    {
        Assert.DoesNotContain(ProjectTaskStatus.Done, TaskWorkflow.AllowedTransitions(ProjectTaskStatus.Todo, isProjectManager: true));
    }
}
