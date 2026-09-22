using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Projects;

public class ProjectService
{
    private readonly IAppDbContext _db;
    private readonly ProjectAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly AuditTrail _auditTrail;
    private readonly NotificationSender _notifications;
    private readonly CompanyTime _companyTime;

    public ProjectService(
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

    public async Task<PagedResult<ProjectListItemDto>> ListAsync(ProjectQuery query, CancellationToken cancellationToken)
    {
        var today = _companyTime.Today;
        var projects = _access.VisibleProjects().AsNoTracking();

        if (query.SearchTerm is { } term)
            projects = projects.Where(p => p.Name.Contains(term) || p.Code.Contains(term));

        if (query.Status.HasValue)
            projects = projects.Where(p => p.Status == query.Status);

        projects = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => projects.OrderByDirection(p => p.Code, query.Descending),
            "startdate" => projects.OrderByDirection(p => p.StartDate, query.Descending),
            "enddate" => projects.OrderByDirection(p => p.EndDate, query.Descending),
            _ => projects.OrderByDirection(p => p.Name, query.Descending)
        };

        return await projects
            .Select(p => new ProjectListItemDto(
                p.Id,
                p.Code,
                p.Name,
                p.Manager.FirstName + " " + p.Manager.LastName,
                p.Status,
                p.StartDate,
                p.EndDate,
                p.Members.Count,
                p.Tasks.Count(t => t.Status != ProjectTaskStatus.Cancelled),
                p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done),
                p.Tasks.Count(t => t.DueDate < today
                    && t.Status != ProjectTaskStatus.Done
                    && t.Status != ProjectTaskStatus.Cancelled)))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ProjectDetailsDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await _access.GetVisibleAsync(id, cancellationToken);
        var today = _companyTime.Today;

        var managerName = await _db.Employees
            .Where(e => e.Id == project.ManagerId)
            .Select(e => e.FirstName + " " + e.LastName)
            .FirstAsync(cancellationToken);

        var members = await _db.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == id)
            .OrderBy(m => m.Employee.FirstName).ThenBy(m => m.Employee.LastName)
            .Select(m => new ProjectMemberDto(
                m.EmployeeId,
                m.Employee.FirstName + " " + m.Employee.LastName,
                m.Employee.EmployeeCode,
                m.Employee.Designation.Title,
                m.RoleInProject,
                m.AddedAt))
            .ToListAsync(cancellationToken);

        var statusCounts = await _db.ProjectTasks
            .Where(t => t.ProjectId == id)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var overdue = await _db.ProjectTasks.WhereOverdue(today).CountAsync(t => t.ProjectId == id, cancellationToken);

        var summary = new TaskSummaryDto(
            statusCounts.GetValueOrDefault(ProjectTaskStatus.Todo),
            statusCounts.GetValueOrDefault(ProjectTaskStatus.InProgress),
            statusCounts.GetValueOrDefault(ProjectTaskStatus.InReview),
            statusCounts.GetValueOrDefault(ProjectTaskStatus.Done),
            statusCounts.GetValueOrDefault(ProjectTaskStatus.Cancelled),
            overdue);

        return new ProjectDetailsDto(
            project.Id,
            project.Code,
            project.Name,
            project.Description,
            project.ManagerId,
            managerName,
            project.StartDate,
            project.EndDate,
            project.Status,
            members,
            summary,
            _access.CanManage(project.ManagerId));
    }

    public async Task<ProjectDetailsDto> CreateAsync(SaveProjectRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(RoleNames.Admin) && request.ManagerId != _currentUser.EmployeeId)
            throw new ForbiddenAccessException("Managers can only create projects that they manage themselves.");

        var project = new Project { CreatedByUserId = _currentUser.UserId };
        await ValidateAsync(project, request, cancellationToken);
        Apply(project, request);
        _db.Projects.Add(project);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _auditTrail.Record(AuditActions.ProjectCreated, nameof(Project), project.Id, new { project.Code, project.Name, project.ManagerId });
        _notifications.Notify(
            await _notifications.GetUserIdAsync(project.ManagerId, cancellationToken),
            NotificationType.ProjectMemberAdded,
            "You manage a new project",
            $"{project.Code} – {project.Name} was created with you as project manager.",
            $"/projects/{project.Id}");

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(project.Id, cancellationToken);
    }

    public async Task<ProjectDetailsDto> UpdateAsync(int id, SaveProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _access.GetManageableAsync(id, cancellationToken);

        if (request.ManagerId != project.ManagerId && !_currentUser.IsInRole(RoleNames.Admin))
            throw new ForbiddenAccessException("Only an administrator can hand a project to another manager.");

        await ValidateAsync(project, request, cancellationToken);

        var changes = new List<string>();
        if (project.Status != request.Status)
        {
            await ApplyStatusChangeAsync(project, request.Status, cancellationToken);
            changes.Add($"Status: {project.Status} -> {request.Status}");
        }
        if (project.ManagerId != request.ManagerId) changes.Add("Manager");
        if (project.EndDate != request.EndDate) changes.Add("EndDate");

        Apply(project, request);

        _auditTrail.Record(AuditActions.ProjectUpdated, nameof(Project), id, new { changes });
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<ProjectDetailsDto> AddMemberAsync(int projectId, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var project = await _access.GetManageableAsync(projectId, cancellationToken);

        if (project.IsClosed)
            throw new BusinessRuleException("Members can't be added to a completed or cancelled project.");

        if (request.EmployeeId == project.ManagerId)
            throw new BusinessRuleException("The project manager is already part of the project.");

        var employee = await _db.Employees.WhereCurrent().FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new BusinessRuleException("The selected employee is not a current employee.");

        if (await _db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.EmployeeId == request.EmployeeId, cancellationToken))
            throw new ConflictException($"{employee.FullName} is already a member of this project.");

        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            EmployeeId = employee.Id,
            RoleInProject = string.IsNullOrWhiteSpace(request.RoleInProject) ? null : request.RoleInProject.Trim(),
            AddedAt = _companyTime.UtcNow
        });

        _auditTrail.Record(AuditActions.ProjectMemberAdded, nameof(Project), projectId, new { employee.EmployeeCode });
        _notifications.Notify(
            employee.UserId,
            NotificationType.ProjectMemberAdded,
            "Added to a project",
            $"You were added to {project.Code} – {project.Name}.",
            $"/projects/{projectId}");

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    public async Task<ProjectDetailsDto> RemoveMemberAsync(int projectId, int employeeId, CancellationToken cancellationToken)
    {
        await _access.GetManageableAsync(projectId, cancellationToken);

        var member = await _db.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.EmployeeId == employeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProjectMember), employeeId);

        var openTasks = await _db.ProjectTasks
            .WhereOpen()
            .CountAsync(t => t.ProjectId == projectId && t.AssigneeId == employeeId, cancellationToken);
        if (openTasks > 0)
            throw new BusinessRuleException($"Reassign this member's {openTasks} open task(s) before removing them from the project.");

        _db.ProjectMembers.Remove(member);
        _auditTrail.Record(AuditActions.ProjectMemberRemoved, nameof(Project), projectId, new { employeeId });
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(projectId, cancellationToken);
    }

    private async Task ApplyStatusChangeAsync(Project project, ProjectStatus newStatus, CancellationToken cancellationToken)
    {
        var openTasks = await _db.ProjectTasks
            .WhereOpen()
            .Where(t => t.ProjectId == project.Id)
            .ToListAsync(cancellationToken);

        if (newStatus == ProjectStatus.Completed && openTasks.Count > 0)
            throw new BusinessRuleException($"Finish or cancel the {openTasks.Count} open task(s) before completing the project.");

        if (newStatus == ProjectStatus.Cancelled)
        {
            foreach (var task in openTasks)
            {
                task.History.Add(new TaskHistoryEntry
                {
                    ChangedByUserId = _currentUser.UserId,
                    FieldName = "Status",
                    OldValue = task.Status.ToString(),
                    NewValue = $"{ProjectTaskStatus.Cancelled} (project cancelled)",
                    ChangedAt = _companyTime.UtcNow
                });
                task.Status = ProjectTaskStatus.Cancelled;
            }
        }
    }

    private async Task ValidateAsync(Project project, SaveProjectRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Projects.AnyAsync(p => p.Id != project.Id && p.Code == code, cancellationToken))
            throw new ConflictException($"Project code {code} is already in use.");

        if (request.ManagerId != project.ManagerId
            && !await _db.Employees.WhereCurrent().AnyAsync(e => e.Id == request.ManagerId, cancellationToken))
            throw new BusinessRuleException("The selected project manager is not a current employee.");
    }

    private static void Apply(Project project, SaveProjectRequest request)
    {
        project.Code = request.Code.Trim().ToUpperInvariant();
        project.Name = request.Name.Trim();
        project.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        project.ManagerId = request.ManagerId;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Status = request.Status;
    }
}
