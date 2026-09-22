using WorkFlow360.Domain.Enums;
using static WorkFlow360.Domain.Enums.ProjectTaskStatus;

namespace WorkFlow360.Domain.Rules;

/// <summary>
/// Which status changes a task allows. Assignees move open work forward (or back a step);
/// cancelling a task and reopening finished or cancelled work is the project manager's call.
/// </summary>
public static class TaskWorkflow
{
    private static readonly Dictionary<ProjectTaskStatus, ProjectTaskStatus[]> Transitions = new()
    {
        [Todo] = [InProgress, Cancelled],
        [InProgress] = [Todo, InReview, Done, Cancelled],
        [InReview] = [InProgress, Done, Cancelled],
        [Done] = [InProgress],
        [Cancelled] = [Todo],
    };

    public static bool IsOpen(ProjectTaskStatus status) => status is Todo or InProgress or InReview;

    public static IReadOnlyList<ProjectTaskStatus> AllowedTransitions(ProjectTaskStatus from, bool isProjectManager) =>
        Transitions[from]
            .Where(to => isProjectManager || (IsOpen(from) && to != Cancelled))
            .ToList();
}
