using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Employees;
using WorkFlow360.Application.Leave;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Dashboard;

/// <summary>
/// One call for the whole dashboard. Every figure is a COUNT or a small TOP-N on indexed columns,
/// so the page stays cheap even with many people opening it at 9am.
/// </summary>
public class DashboardService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly CompanyTime _companyTime;
    private readonly LeaveService _leaveService;
    private readonly TaskService _taskService;

    public DashboardService(
        IAppDbContext db,
        ICurrentUser currentUser,
        CompanyTime companyTime,
        LeaveService leaveService,
        TaskService taskService)
    {
        _db = db;
        _currentUser = currentUser;
        _companyTime = companyTime;
        _leaveService = leaveService;
        _taskService = taskService;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var today = _companyTime.Today;
        var seesCompany = _currentUser.IsInRole(RoleNames.Admin) || _currentUser.IsInRole(RoleNames.HR);

        var company = seesCompany ? await GetCompanyAsync(today, cancellationToken) : null;
        var team = _currentUser.EmployeeId is { } employeeId ? await GetTeamAsync(employeeId, today, cancellationToken) : null;
        var me = _currentUser.EmployeeId is { } myId ? await GetMineAsync(myId, today, cancellationToken) : null;

        var holidays = await _db.Holidays.AsNoTracking()
            .Where(h => h.Date >= today)
            .OrderBy(h => h.Date)
            .Take(3)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name))
            .ToListAsync(cancellationToken);

        return new DashboardDto(company, team, me, holidays);
    }

    private async Task<CompanyOverviewDto> GetCompanyAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var currentEmployees = _db.Employees.AsNoTracking().WhereCurrent();

        var headcount = (await currentEmployees
                .GroupBy(e => e.Department.Name)
                .Select(g => new { Department = g.Key, Employees = g.Count() })
                .OrderByDescending(d => d.Employees).ThenBy(d => d.Department)
                .ToListAsync(cancellationToken))
            .Select(d => new DepartmentHeadcountDto(d.Department, d.Employees))
            .ToList();

        return new CompanyOverviewDto(
            ActiveEmployees: headcount.Sum(d => d.Employees),
            CheckedInToday: await _db.AttendanceRecords.CountAsync(a => a.WorkDate == today, cancellationToken),
            LateToday: await _db.AttendanceRecords.CountAsync(a => a.WorkDate == today && a.IsLate, cancellationToken),
            OnLeaveToday: await OnLeaveOn(today).CountAsync(cancellationToken),
            PendingLeaveRequests: await _db.LeaveRequests.CountAsync(r => r.Status == LeaveStatus.Pending, cancellationToken),
            ActiveProjects: await _db.Projects.CountAsync(p => p.Status == ProjectStatus.Active, cancellationToken),
            OpenTasks: await _db.ProjectTasks.WhereOpen().CountAsync(cancellationToken),
            OverdueTasks: await _db.ProjectTasks.WhereOverdue(today).CountAsync(cancellationToken),
            Headcount: headcount);
    }

    private async Task<TeamOverviewDto?> GetTeamAsync(int managerId, DateOnly today, CancellationToken cancellationToken)
    {
        var reportIds = _db.Employees.WhereCurrent().Where(e => e.ManagerId == managerId).Select(e => e.Id);
        var directReports = await reportIds.CountAsync(cancellationToken);

        var projects = (await _db.Projects.AsNoTracking()
                .Where(p => p.ManagerId == managerId && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Cancelled)
                .Select(p => new
                {
                    p.Id,
                    p.Code,
                    p.Name,
                    Done = p.Tasks.Count(t => t.Status == ProjectTaskStatus.Done),
                    Total = p.Tasks.Count(t => t.Status != ProjectTaskStatus.Cancelled),
                    Overdue = p.Tasks.Count(t => t.DueDate < today
                        && (t.Status == ProjectTaskStatus.Todo || t.Status == ProjectTaskStatus.InProgress || t.Status == ProjectTaskStatus.InReview))
                })
                .OrderByDescending(p => p.Overdue).ThenBy(p => p.Code)
                .Take(5)
                .ToListAsync(cancellationToken))
            .Select(p => new ProjectProgressDto(p.Id, p.Code, p.Name, p.Done, p.Total, p.Overdue))
            .ToList();

        if (directReports == 0 && projects.Count == 0)
            return null;

        return new TeamOverviewDto(
            directReports,
            CheckedInToday: await _db.AttendanceRecords.CountAsync(a => a.WorkDate == today && reportIds.Contains(a.EmployeeId), cancellationToken),
            OnLeaveToday: await OnLeaveOn(today).CountAsync(r => reportIds.Contains(r.EmployeeId), cancellationToken),
            PendingApprovals: await _db.LeaveRequests.CountAsync(r => r.Status == LeaveStatus.Pending && r.Employee.ManagerId == managerId, cancellationToken),
            projects);
    }

    private async Task<MyOverviewDto> GetMineAsync(int employeeId, DateOnly today, CancellationToken cancellationToken)
    {
        var attendance = await _db.AttendanceRecords.AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == today, cancellationToken);

        var onLeave = await OnLeaveOn(today)
            .Where(r => r.EmployeeId == employeeId)
            .Select(r => r.LeaveType.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var isHoliday = await _db.Holidays.AnyAsync(h => h.Date == today, cancellationToken);

        var myOpenTasks = _db.ProjectTasks.WhereOpen().Where(t => t.AssigneeId == employeeId);

        var dueSoon = await _taskService.ListAsync(
            new TaskQuery { AssignedToMe = true, OpenOnly = true, PageSize = 5, SortBy = "dueDate" }, cancellationToken);

        var upcomingLeave = await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.EmployeeId == employeeId
                && r.EndDate >= today
                && (r.Status == LeaveStatus.Pending || r.Status == LeaveStatus.Approved))
            .OrderBy(r => r.StartDate)
            .Take(3)
            .Select(r => new UpcomingLeaveDto(r.Id, r.LeaveType.Name, r.StartDate, r.EndDate, r.TotalDays, r.Status))
            .ToListAsync(cancellationToken);

        return new MyOverviewDto(
            attendance?.CheckInAt,
            attendance?.CheckOutAt,
            attendance?.IsLate ?? false,
            IsWorkingDay: !isHoliday && !_companyTime.WeekendDays.Contains(today.DayOfWeek),
            onLeave,
            OpenTasks: await myOpenTasks.CountAsync(cancellationToken),
            OverdueTasks: await myOpenTasks.CountAsync(t => t.DueDate < today, cancellationToken),
            dueSoon.Items,
            await _leaveService.GetMyBalancesAsync(today.Year, cancellationToken),
            upcomingLeave);
    }

    private IQueryable<LeaveRequest> OnLeaveOn(DateOnly date) =>
        _db.LeaveRequests.Where(r => r.Status == LeaveStatus.Approved && r.StartDate <= date && r.EndDate >= date);
}
