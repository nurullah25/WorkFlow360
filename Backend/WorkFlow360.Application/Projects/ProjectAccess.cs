using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Projects;

/// <summary>
/// Who can see and who can manage a project. Admin and HR see every project; everyone else sees
/// the projects they manage or are a member of. Only Admin and the project's manager can change it.
/// </summary>
public class ProjectAccess
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ProjectAccess(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public IQueryable<Project> VisibleProjects()
    {
        if (_currentUser.IsInRole(RoleNames.Admin) || _currentUser.IsInRole(RoleNames.HR))
            return _db.Projects;

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return _db.Projects.Where(_ => false);

        return _db.Projects.Where(p => p.ManagerId == employeeId || p.Members.Any(m => m.EmployeeId == employeeId));
    }

    public bool CanManage(int projectManagerId) =>
        _currentUser.IsInRole(RoleNames.Admin) || _currentUser.EmployeeId == projectManagerId;

    /// <summary>Returns 404 rather than 403 for projects the user can't see, so ids can't be probed.</summary>
    public async Task<Project> GetVisibleAsync(int projectId, CancellationToken cancellationToken) =>
        await VisibleProjects().FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException(nameof(Project), projectId);

    public async Task<Project> GetManageableAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await GetVisibleAsync(projectId, cancellationToken);
        if (!CanManage(project.ManagerId))
            throw new ForbiddenAccessException("Only the project manager can change this project.");
        return project;
    }
}
