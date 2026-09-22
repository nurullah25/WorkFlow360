using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Tasks;

public static class TaskQueryExtensions
{
    /// <summary>SQL-translatable version of <see cref="Domain.Rules.TaskWorkflow.IsOpen"/>.</summary>
    public static IQueryable<ProjectTask> WhereOpen(this IQueryable<ProjectTask> query) =>
        query.Where(t => t.Status == ProjectTaskStatus.Todo
            || t.Status == ProjectTaskStatus.InProgress
            || t.Status == ProjectTaskStatus.InReview);

    public static IQueryable<ProjectTask> WhereOverdue(this IQueryable<ProjectTask> query, DateOnly today) =>
        query.WhereOpen().Where(t => t.DueDate < today);
}
