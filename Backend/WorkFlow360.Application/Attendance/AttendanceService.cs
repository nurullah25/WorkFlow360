using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Attendance;

public class AttendanceService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly CompanyTime _companyTime;
    private readonly AttendanceOptions _options;

    public AttendanceService(IAppDbContext db, ICurrentUser currentUser, CompanyTime companyTime, IOptions<AttendanceOptions> options)
    {
        _db = db;
        _currentUser = currentUser;
        _companyTime = companyTime;
        _options = options.Value;
    }

    public async Task<TodayAttendanceDto> GetTodayAsync(CancellationToken cancellationToken)
    {
        var employeeId = RequireEmployeeId();
        var today = _companyTime.Today;

        var record = await _db.AttendanceRecords.AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == today, cancellationToken);
        var holidayName = await _db.Holidays.Where(h => h.Date == today).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken);
        var leaveTypeName = await GetApprovedLeaveOnAsync(employeeId, today, cancellationToken);

        return new TodayAttendanceDto(
            today,
            holidayName == null && !_companyTime.WeekendDays.Contains(today.DayOfWeek),
            holidayName,
            leaveTypeName,
            record?.CheckInAt,
            record?.CheckOutAt,
            record?.IsLate ?? false,
            record?.WorkedMinutes,
            _options.OfficeStartTime,
            _options.GraceMinutes);
    }

    public async Task<TodayAttendanceDto> CheckInAsync(CancellationToken cancellationToken)
    {
        var employeeId = RequireEmployeeId();
        var today = _companyTime.Today;

        var existing = await _db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == today, cancellationToken);
        if (existing != null)
            throw new BusinessRuleException($"You already checked in today at {_companyTime.ToLocal(existing.CheckInAt):HH:mm}.");

        var leaveTypeName = await GetApprovedLeaveOnAsync(employeeId, today, cancellationToken);
        if (leaveTypeName != null)
            throw new BusinessRuleException($"You're on approved {leaveTypeName} today. Cancel the leave first if you're working.");

        var lateAfter = _options.OfficeStartTime.AddMinutes(_options.GraceMinutes);

        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            EmployeeId = employeeId,
            WorkDate = today,
            CheckInAt = _companyTime.UtcNow,
            IsLate = TimeOnly.FromDateTime(_companyTime.LocalNow) > lateAfter
        });

        // The unique (EmployeeId, WorkDate) index is the backstop if two check-ins race each other.
        await _db.SaveChangesAsync(cancellationToken);
        return await GetTodayAsync(cancellationToken);
    }

    public async Task<TodayAttendanceDto> CheckOutAsync(CancellationToken cancellationToken)
    {
        var employeeId = RequireEmployeeId();
        var today = _companyTime.Today;

        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == today, cancellationToken)
            ?? throw new BusinessRuleException("You haven't checked in today.");

        if (record.CheckOutAt != null)
            throw new BusinessRuleException($"You already checked out today at {_companyTime.ToLocal(record.CheckOutAt.Value):HH:mm}.");

        record.CheckOutAt = _companyTime.UtcNow;
        record.WorkedMinutes = (int)(record.CheckOutAt.Value - record.CheckInAt).TotalMinutes;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetTodayAsync(cancellationToken);
    }

    /// <summary>Day-by-day view of a month. Without an employee id it's the signed-in employee's own month.</summary>
    public async Task<EmployeeMonthDto> GetMonthAsync(int? employeeId, int? year, int? month, CancellationToken cancellationToken)
    {
        var targetId = employeeId ?? RequireEmployeeId();
        var (y, m) = ResolveMonth(year, month);

        var employee = await VisibleEmployees()
            .Where(e => e.Id == targetId)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeCode, e.JoiningDate })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), targetId);

        var data = await LoadMonthDataAsync([employee.Id], y, m, cancellationToken);
        var days = BuildDays(employee.Id, employee.JoiningDate, y, m, data);

        return new EmployeeMonthDto(
            employee.Id,
            $"{employee.FirstName} {employee.LastName}",
            employee.EmployeeCode,
            y,
            m,
            AttendanceCalendar.Summarize(days),
            days);
    }

    public async Task<PagedResult<TeamAttendanceRowDto>> GetTeamAsync(TeamAttendanceQuery query, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInRole(RoleNames.Admin) && !_currentUser.IsInRole(RoleNames.HR) && !_currentUser.IsInRole(RoleNames.Manager))
            throw new ForbiddenAccessException("Only managers and HR can view team attendance.");

        var (y, m) = ResolveMonth(query.Year, query.Month);
        var employees = VisibleEmployees().WhereCurrent().Where(e => e.Id != _currentUser.EmployeeId);

        if (query.SearchTerm is { } term)
            employees = employees.Where(e => (e.FirstName + " " + e.LastName).Contains(term) || e.EmployeeCode.Contains(term));

        var page = await employees
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.EmployeeCode, Department = e.Department.Name, e.JoiningDate })
            .ToPagedResultAsync(query, cancellationToken);

        // One query per data set for the whole page, rather than one per employee.
        var data = await LoadMonthDataAsync(page.Items.Select(e => e.Id).ToList(), y, m, cancellationToken);

        var rows = page.Items
            .Select(e => new TeamAttendanceRowDto(
                e.Id, e.Name, e.EmployeeCode, e.Department,
                AttendanceCalendar.Summarize(BuildDays(e.Id, e.JoiningDate, y, m, data))))
            .ToList();

        return new PagedResult<TeamAttendanceRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>Admin and HR see everyone, managers see themselves and their direct reports, employees only themselves.</summary>
    private IQueryable<Employee> VisibleEmployees()
    {
        var employees = _db.Employees.AsNoTracking();
        if (_currentUser.IsInRole(RoleNames.Admin) || _currentUser.IsInRole(RoleNames.HR))
            return employees;

        var me = _currentUser.EmployeeId;
        return employees.Where(e => e.Id == me || e.ManagerId == me);
    }

    private sealed record MonthData(
        ILookup<int, AttendanceRecord> Records,
        ILookup<int, (DateOnly Start, DateOnly End, string LeaveTypeName)> Leaves,
        IReadOnlyDictionary<DateOnly, string> Holidays);

    private async Task<MonthData> LoadMonthDataAsync(IReadOnlyCollection<int> employeeIds, int year, int month, CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var records = await _db.AttendanceRecords.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.WorkDate >= monthStart && a.WorkDate <= monthEnd)
            .ToListAsync(cancellationToken);

        var leaves = await _db.LeaveRequests.AsNoTracking()
            .Where(r => employeeIds.Contains(r.EmployeeId)
                && r.Status == LeaveStatus.Approved
                && r.StartDate <= monthEnd && r.EndDate >= monthStart)
            .Select(r => new { r.EmployeeId, r.StartDate, r.EndDate, r.LeaveType.Name })
            .ToListAsync(cancellationToken);

        var holidays = await _db.Holidays.AsNoTracking()
            .Where(h => h.Date >= monthStart && h.Date <= monthEnd)
            .ToDictionaryAsync(h => h.Date, h => h.Name, cancellationToken);

        return new MonthData(
            records.ToLookup(r => r.EmployeeId),
            leaves.ToLookup(l => l.EmployeeId, l => (l.StartDate, l.EndDate, l.Name)),
            holidays);
    }

    private IReadOnlyList<AttendanceDayDto> BuildDays(int employeeId, DateOnly joiningDate, int year, int month, MonthData data) =>
        AttendanceCalendar.BuildMonth(
            year,
            month,
            _companyTime.Today,
            joiningDate,
            data.Records[employeeId].ToDictionary(r => r.WorkDate),
            AttendanceCalendar.ExpandLeaveDays(data.Leaves[employeeId], year, month),
            data.Holidays,
            _companyTime.WeekendDays);

    private async Task<string?> GetApprovedLeaveOnAsync(int employeeId, DateOnly date, CancellationToken cancellationToken) =>
        await _db.LeaveRequests
            .Where(r => r.EmployeeId == employeeId && r.Status == LeaveStatus.Approved && r.StartDate <= date && r.EndDate >= date)
            .Select(r => r.LeaveType.Name)
            .FirstOrDefaultAsync(cancellationToken);

    private (int Year, int Month) ResolveMonth(int? year, int? month)
    {
        var today = _companyTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        if (m is < 1 or > 12 || y is < 2000 or > 2100)
            throw new BusinessRuleException("Choose a valid month.");

        return (y, m);
    }

    private int RequireEmployeeId() =>
        _currentUser.EmployeeId ?? throw new BusinessRuleException("Only accounts linked to an employee record have attendance.");
}
