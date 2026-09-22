using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Application.Projects;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;
using WorkFlow360.Domain.Rules;

namespace WorkFlow360.Application.Tasks;

public class TaskService
{
    private readonly IAppDbContext _db;
    private readonly ProjectAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly AuditTrail _auditTrail;
    private readonly NotificationSender _notifications;
    private readonly CompanyTime _companyTime;

    public TaskService(
        IAppDbContext db,
        ProjectAccess access,
        ICurrentUser currentUser,
        AuditTrail auditTrail,
        NotificationSender notifications,
        CompanyTime companyTime)
    {
        _db = db;
        _access = access;
        _currentUser = currentUser;
        _auditTrail = auditTrail;
        _notifications = notifications;
        _companyTime = companyTime;
    }

    public async Task<PagedResult<TaskListItemDto>> ListAsync(TaskQuery query, CancellationToken cancellationToken)
    {
        var today = _companyTime.Today;
        var visibleProjectIds = _access.VisibleProjects().Select(p => p.Id);
        var tasks = _db.ProjectTasks.AsNoTracking().Where(t => visibleProjectIds.Contains(t.ProjectId));

        if (query.SearchTerm is { } term)
            tasks = tasks.Where(t => t.Title.Contains(term));
        if (query.ProjectId.HasValue)
            tasks = tasks.Where(t => t.ProjectId == query.ProjectId);
        if (query.AssigneeId.HasValue)
            tasks = tasks.Where(t => t.AssigneeId == query.AssigneeId);
        if (query.AssignedToMe)
        {
            var me = _currentUser.EmployeeId ?? -1;
            tasks = tasks.Where(t => t.AssigneeId == me);
        }
        if (query.Status.HasValue)
            tasks = tasks.Where(t => t.Status == query.Status);
        if (query.Priority.HasValue)
            tasks = tasks.Where(t => t.Priority == query.Priority);
        if (query.OpenOnly)
            tasks = tasks.WhereOpen();
        if (query.OverdueOnly)
            tasks = tasks.WhereOverdue(today);

        // Enums are stored as text, so priority and status are ranked explicitly instead of sorted alphabetically.
        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "title" => tasks.OrderByDirection(t => t.Title, query.Descending),
            "priority" => tasks.OrderByDirection(t =>
                t.Priority == TaskPriority.Critical ? 3 :
                t.Priority == TaskPriority.High ? 2 :
                t.Priority == TaskPriority.Medium ? 1 : 0, query.Descending),
            "status" => tasks.OrderByDirection(t =>
                t.Status == ProjectTaskStatus.Todo ? 0 :
                t.Status == ProjectTaskStatus.InProgress ? 1 :
                t.Status == ProjectTaskStatus.InReview ? 2 :
                t.Status == ProjectTaskStatus.Done ? 3 : 4, query.Descending),
            "updated" => tasks.OrderByDirection(t => t.UpdatedAt ?? t.CreatedAt, query.Descending),
            // Tasks without a due date go last in either direction.
            _ => tasks.OrderBy(t => t.DueDate == null).ThenByDirection(t => t.DueDate, query.Descending)
        };

        return await ordered
            .ThenBy(t => t.Id)
            .Select(t => new TaskListItemDto(
                t.Id,
                t.Title,
                t.ProjectId,
                t.Project.Code,
                t.AssigneeId,
                t.Assignee != null ? t.Assignee.FirstName + " " + t.Assignee.LastName : null,
                t.Priority,
                t.Status,
                t.DueDate,
                t.DueDate < today
                    && (t.Status == ProjectTaskStatus.Todo || t.Status == ProjectTaskStatus.InProgress || t.Status == ProjectTaskStatus.InReview),
                t.UpdatedAt ?? t.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<TaskDetailsDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var visibleProjectIds = _access.VisibleProjects().Select(p => p.Id);

        var row = await _db.ProjectTasks
            .AsNoTracking()
            .Where(t => t.Id == id && visibleProjectIds.Contains(t.ProjectId))
            .Select(t => new
            {
                Task = t,
                t.Project.Code,
                t.Project.Name,
                t.Project.ManagerId,
                ProjectStatus = t.Project.Status,
                AssigneeName = t.Assignee != null ? t.Assignee.FirstName + " " + t.Assignee.LastName : null,
                CreatedByName = _db.Users.Where(u => u.Id == t.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Task", id);

        var task = row.Task;
        var projectOpen = row.ProjectStatus is not (ProjectStatus.Completed or ProjectStatus.Cancelled);
        var isManager = _access.CanManage(row.ManagerId);
        var isAssignee = task.AssigneeId != null && task.AssigneeId == _currentUser.EmployeeId;

        var allowedStatuses = projectOpen && (isManager || isAssignee)
            ? TaskWorkflow.AllowedTransitions(task.Status, isManager)
            : [];

        return new TaskDetailsDto(
            task.Id,
            task.ProjectId,
            row.Code,
            row.Name,
            task.Title,
            task.Description,
            task.AssigneeId,
            row.AssigneeName,
            task.Priority,
            task.Status,
            task.DueDate,
            task.CompletedAt,
            row.CreatedByName ?? "Unknown",
            task.CreatedAt,
            task.UpdatedAt,
            task.DueDate < _companyTime.Today && TaskWorkflow.IsOpen(task.Status),
            projectOpen && isManager,
            allowedStatuses);
    }

    public async Task<TaskDetailsDto> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var project = await _access.GetManageableAsync(request.ProjectId, cancellationToken);
        EnsureProjectIsOpen(project);

        if (request.DueDate < _companyTime.Today)
            throw new BusinessRuleException("The due date can't be in the past.");

        await EnsureAssignableAsync(project, request.AssigneeId, cancellationToken);

        var task = new ProjectTask
        {
            ProjectId = project.Id,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            AssigneeId = request.AssigneeId,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Status = ProjectTaskStatus.Todo,
            CreatedByUserId = _currentUser.UserId
        };
        task.History.Add(HistoryEntry("Created", null, task.Title));
        _db.ProjectTasks.Add(task);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (task.AssigneeId.HasValue)
            await RecordAssignmentAsync(task, project, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(task.Id, cancellationToken);
    }

    public async Task<TaskDetailsDto> UpdateAsync(int id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await GetVisibleTaskAsync(id, cancellationToken);
        var project = task.Project;

        if (!_access.CanManage(project.ManagerId))
            throw new ForbiddenAccessException("Only the project manager can edit task details.");
        EnsureProjectIsOpen(project);

        var title = request.Title.Trim();
        if (task.Title != title)
        {
            task.History.Add(HistoryEntry("Title", task.Title, title));
            task.Title = title;
        }

        var description = Clean(request.Description);
        if (task.Description != description)
        {
            task.History.Add(HistoryEntry("Description", null, null));
            task.Description = description;
        }

        if (task.Priority != request.Priority)
        {
            task.History.Add(HistoryEntry("Priority", task.Priority.ToString(), request.Priority.ToString()));
            task.Priority = request.Priority;
        }

        if (task.DueDate != request.DueDate)
        {
            task.History.Add(HistoryEntry("DueDate", task.DueDate?.ToString("yyyy-MM-dd"), request.DueDate?.ToString("yyyy-MM-dd")));
            task.DueDate = request.DueDate;
        }

        if (task.AssigneeId != request.AssigneeId)
        {
            await EnsureAssignableAsync(project, request.AssigneeId, cancellationToken);

            task.History.Add(HistoryEntry(
                "Assignee",
                await GetEmployeeNameAsync(task.AssigneeId, cancellationToken),
                await GetEmployeeNameAsync(request.AssigneeId, cancellationToken)));
            task.AssigneeId = request.AssigneeId;

            if (task.AssigneeId.HasValue)
                await RecordAssignmentAsync(task, project, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TaskDetailsDto> ChangeStatusAsync(int id, ChangeTaskStatusRequest request, CancellationToken cancellationToken)
    {
        var task = await GetVisibleTaskAsync(id, cancellationToken);
        var project = task.Project;

        var isManager = _access.CanManage(project.ManagerId);
        var isAssignee = task.AssigneeId != null && task.AssigneeId == _currentUser.EmployeeId;
        if (!isManager && !isAssignee)
            throw new ForbiddenAccessException("Only the assignee or the project manager can change a task's status.");

        EnsureProjectIsOpen(project);

        if (task.Status == request.Status)
            return await GetAsync(id, cancellationToken);

        if (!TaskWorkflow.AllowedTransitions(task.Status, isManager).Contains(request.Status))
        {
            if (TaskWorkflow.AllowedTransitions(task.Status, isProjectManager: true).Contains(request.Status))
                throw new ForbiddenAccessException("Only the project manager can cancel or reopen tasks.");

            throw new BusinessRuleException($"A task can't move from {task.Status} to {request.Status}.");
        }

        task.History.Add(HistoryEntry("Status", task.Status.ToString(), request.Status.ToString()));
        task.Status = request.Status;
        task.CompletedAt = request.Status == ProjectTaskStatus.Done ? _companyTime.UtcNow : null;

        _notifications.Notify(
            [
                await _notifications.GetUserIdAsync(project.ManagerId, cancellationToken),
                await _notifications.GetUserIdAsync(task.AssigneeId, cancellationToken)
            ],
            NotificationType.TaskStatusChanged,
            "Task status changed",
            $"\"{task.Title}\" is now {request.Status}.",
            $"/tasks/{task.Id}");

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskCommentDto>> ListCommentsAsync(int taskId, CancellationToken cancellationToken)
    {
        await GetVisibleTaskAsync(taskId, cancellationToken);

        return await _db.TaskComments
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new TaskCommentDto(c.Id, c.AuthorUserId, c.Author.FullName, c.Body, c.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskCommentDto> AddCommentAsync(int taskId, AddTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var task = await GetVisibleTaskAsync(taskId, cancellationToken);

        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorUserId = _currentUser.UserId,
            Body = request.Body.Trim(),
            CreatedAt = _companyTime.UtcNow
        };
        _db.TaskComments.Add(comment);

        _notifications.Notify(
            [
                await _notifications.GetUserIdAsync(task.Project.ManagerId, cancellationToken),
                await _notifications.GetUserIdAsync(task.AssigneeId, cancellationToken)
            ],
            NotificationType.TaskCommented,
            "New comment",
            $"New comment on \"{task.Title}\".",
            $"/tasks/{taskId}");

        await _db.SaveChangesAsync(cancellationToken);

        var authorName = await _db.Users.Where(u => u.Id == comment.AuthorUserId).Select(u => u.FullName).FirstAsync(cancellationToken);
        return new TaskCommentDto(comment.Id, comment.AuthorUserId, authorName, comment.Body, comment.CreatedAt);
    }

    public async Task<IReadOnlyList<TaskHistoryDto>> ListHistoryAsync(int taskId, CancellationToken cancellationToken)
    {
        await GetVisibleTaskAsync(taskId, cancellationToken);

        return await _db.TaskHistory
            .AsNoTracking()
            .Where(h => h.TaskId == taskId)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Select(h => new TaskHistoryDto(h.Id, h.FieldName, h.OldValue, h.NewValue, h.ChangedBy.FullName, h.ChangedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<ProjectTask> GetVisibleTaskAsync(int id, CancellationToken cancellationToken)
    {
        var visibleProjectIds = _access.VisibleProjects().Select(p => p.Id);

        return await _db.ProjectTasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id && visibleProjectIds.Contains(t.ProjectId), cancellationToken)
            ?? throw new NotFoundException("Task", id);
    }

    private async Task EnsureAssignableAsync(Project project, int? assigneeId, CancellationToken cancellationToken)
    {
        if (assigneeId == null || assigneeId == project.ManagerId)
            return;

        var isMember = await _db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.EmployeeId == assigneeId, cancellationToken);
        if (!isMember)
            throw new BusinessRuleException("Tasks can only be assigned to project members. Add the person to the project first.");
    }

    private async Task RecordAssignmentAsync(ProjectTask task, Project project, CancellationToken cancellationToken)
    {
        _auditTrail.Record(AuditActions.TaskAssigned, "Task", task.Id, new { task.AssigneeId, project.Code });
        _notifications.Notify(
            await _notifications.GetUserIdAsync(task.AssigneeId, cancellationToken),
            NotificationType.TaskAssigned,
            "New task assigned",
            $"{project.Code}: \"{task.Title}\" was assigned to you.",
            $"/tasks/{task.Id}");
    }

    private static void EnsureProjectIsOpen(Project project)
    {
        if (project.IsClosed)
            throw new BusinessRuleException($"{project.Code} is {project.Status.ToString().ToLowerInvariant()}; its tasks can no longer be changed.");
    }

    private Task<string?> GetEmployeeNameAsync(int? employeeId, CancellationToken cancellationToken) =>
        employeeId == null
            ? Task.FromResult<string?>(null)
            : _db.Employees.Where(e => e.Id == employeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(cancellationToken);

    private TaskHistoryEntry HistoryEntry(string field, string? oldValue, string? newValue) => new()
    {
        ChangedByUserId = _currentUser.UserId,
        FieldName = field,
        OldValue = oldValue,
        NewValue = newValue,
        ChangedAt = _companyTime.UtcNow
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
