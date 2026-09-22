using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Auth;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Employees;

public class EmployeeService
{
    private readonly IAppDbContext _db;
    private readonly AuditTrail _auditTrail;
    private readonly TimeProvider _clock;

    public EmployeeService(IAppDbContext db, AuditTrail auditTrail, TimeProvider clock)
    {
        _db = db;
        _auditTrail = auditTrail;
        _clock = clock;
    }

    public async Task<PagedResult<EmployeeListItemDto>> ListAsync(EmployeeQuery query, CancellationToken cancellationToken)
    {
        var employees = _db.Employees.AsNoTracking();

        if (query.SearchTerm is { } term)
        {
            employees = employees.Where(e =>
                (e.FirstName + " " + e.LastName).Contains(term)
                || e.EmployeeCode.Contains(term)
                || e.Email.Contains(term));
        }

        if (query.DepartmentId.HasValue)
            employees = employees.Where(e => e.DepartmentId == query.DepartmentId);

        if (query.ManagerId.HasValue)
            employees = employees.Where(e => e.ManagerId == query.ManagerId);

        if (query.Status.HasValue)
            employees = employees.Where(e => e.Status == query.Status);
        else if (!query.IncludeFormer)
            employees = employees.WhereCurrent();

        employees = query.SortBy?.ToLowerInvariant() switch
        {
            "code" => employees.OrderByDirection(e => e.EmployeeCode, query.Descending),
            "department" => employees.OrderByDirection(e => e.Department.Name, query.Descending).ThenBy(e => e.FirstName),
            "joiningdate" => employees.OrderByDirection(e => e.JoiningDate, query.Descending).ThenBy(e => e.FirstName),
            _ => employees.OrderByDirection(e => e.FirstName, query.Descending).ThenBy(e => e.LastName)
        };

        return await employees
            .Select(e => new EmployeeListItemDto(
                e.Id,
                e.EmployeeCode,
                e.FirstName + " " + e.LastName,
                e.Email,
                e.Department.Name,
                e.Designation.Title,
                e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                e.JoiningDate,
                e.Status))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<EmployeeDetailsDto> GetAsync(int id, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDetailsDto(
                e.Id,
                e.EmployeeCode,
                e.FirstName,
                e.LastName,
                e.Email,
                e.Phone,
                e.DepartmentId,
                e.Department.Name,
                e.DesignationId,
                e.Designation.Title,
                e.ManagerId,
                e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : null,
                e.JoiningDate,
                e.Status,
                e.UserId,
                e.DirectReports.Count(r => r.Status != EmploymentStatus.Resigned && r.Status != EmploymentStatus.Terminated)))
            .FirstOrDefaultAsync(cancellationToken);

        return employee ?? throw new NotFoundException(nameof(Employee), id);
    }

    /// <summary>Small result set for autocomplete fields (manager, assignee, linking a user account).</summary>
    public async Task<IReadOnlyList<EmployeeLookupDto>> LookupAsync(string? search, bool withoutUserAccount, CancellationToken cancellationToken)
    {
        var employees = _db.Employees.AsNoTracking().WhereCurrent();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            employees = employees.Where(e => (e.FirstName + " " + e.LastName).Contains(term) || e.EmployeeCode.Contains(term));
        }

        if (withoutUserAccount)
            employees = employees.Where(e => e.UserId == null);

        return await employees
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Take(20)
            .Select(e => new EmployeeLookupDto(e.Id, e.FirstName + " " + e.LastName, e.EmployeeCode, e.Email))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeeDetailsDto> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = new Employee();
        await ValidateReferencesAsync(employee, request, cancellationToken);
        Apply(employee, request);

        _db.Employees.Add(employee);
        await AddLeaveBalancesAsync(employee, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _auditTrail.Record(AuditActions.EmployeeCreated, nameof(Employee), employee.Id, new { employee.EmployeeCode, Name = employee.FullName });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(employee.Id, cancellationToken);
    }

    public async Task<EmployeeDetailsDto> UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Employee), id);

        await ValidateReferencesAsync(employee, request, cancellationToken);
        await EnsureNoReportingLoopAsync(employee, request.ManagerId, cancellationToken);

        var changes = DescribeChanges(employee, request);
        var isLeaving = employee.IsCurrent && request.Status is EmploymentStatus.Resigned or EmploymentStatus.Terminated;

        if (isLeaving)
            await EndEmploymentAsync(employee, cancellationToken);

        Apply(employee, request);

        _auditTrail.Record(AuditActions.EmployeeUpdated, nameof(Employee), id, new { changes });
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task EndEmploymentAsync(Employee employee, CancellationToken cancellationToken)
    {
        var directReports = await _db.Employees.WhereCurrent().CountAsync(e => e.ManagerId == employee.Id, cancellationToken);
        if (directReports > 0)
            throw new BusinessRuleException($"{employee.FullName} still has {directReports} direct report(s). Assign them a new manager first.");

        var managedProjects = await _db.Projects.CountAsync(p => p.ManagerId == employee.Id
            && p.Status != ProjectStatus.Completed
            && p.Status != ProjectStatus.Cancelled, cancellationToken);
        if (managedProjects > 0)
            throw new BusinessRuleException($"{employee.FullName} still manages {managedProjects} open project(s). Hand them to another manager first.");

        if (employee.User is { IsActive: true } user)
        {
            user.IsActive = false;
            await _db.RevokeAllSessionsAsync(user.Id, _clock.GetUtcNow().UtcDateTime, cancellationToken);
        }
    }

    private async Task ValidateReferencesAsync(Employee employee, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var code = request.EmployeeCode.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Employees.AnyAsync(e => e.Id != employee.Id && e.EmployeeCode == code, cancellationToken))
            throw new ConflictException($"Employee code {code} is already in use.");

        if (await _db.Employees.AnyAsync(e => e.Id != employee.Id && e.Email == email, cancellationToken))
            throw new ConflictException($"Another employee already uses {email}.");

        // An inactive department or designation is only accepted when it is the one the employee already has.
        if (request.DepartmentId != employee.DepartmentId
            && !await _db.Departments.AnyAsync(d => d.Id == request.DepartmentId && d.IsActive, cancellationToken))
            throw new BusinessRuleException("The selected department does not exist or is inactive.");

        if (request.DesignationId != employee.DesignationId
            && !await _db.Designations.AnyAsync(d => d.Id == request.DesignationId && d.IsActive, cancellationToken))
            throw new BusinessRuleException("The selected designation does not exist or is inactive.");

        if (request.ManagerId.HasValue && request.ManagerId != employee.ManagerId
            && !await _db.Employees.WhereCurrent().AnyAsync(e => e.Id == request.ManagerId, cancellationToken))
            throw new BusinessRuleException("The selected manager is not a current employee.");
    }

    private async Task EnsureNoReportingLoopAsync(Employee employee, int? newManagerId, CancellationToken cancellationToken)
    {
        if (newManagerId == null || newManagerId == employee.ManagerId)
            return;

        if (newManagerId == employee.Id)
            throw new BusinessRuleException("An employee cannot be their own manager.");

        // Walk up from the proposed manager. Reaching this employee means the new manager reports to them.
        var visited = new HashSet<int>();
        int? currentId = newManagerId;

        while (currentId.HasValue && visited.Add(currentId.Value))
        {
            if (currentId == employee.Id)
                throw new BusinessRuleException("The selected manager reports to this employee, which would create a reporting loop.");

            currentId = await _db.Employees
                .Where(e => e.Id == currentId)
                .Select(e => e.ManagerId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    private async Task AddLeaveBalancesAsync(Employee employee, CancellationToken cancellationToken)
    {
        var year = _clock.GetUtcNow().Year;
        var leaveTypes = await _db.LeaveTypes.Where(t => t.IsActive).ToListAsync(cancellationToken);

        foreach (var leaveType in leaveTypes)
        {
            _db.LeaveBalances.Add(new LeaveBalance
            {
                Employee = employee,
                LeaveTypeId = leaveType.Id,
                Year = year,
                AllocatedDays = leaveType.DefaultDaysPerYear
            });
        }
    }

    private static void Apply(Employee employee, SaveEmployeeRequest request)
    {
        employee.EmployeeCode = request.EmployeeCode.Trim().ToUpperInvariant();
        employee.FirstName = request.FirstName.Trim();
        employee.LastName = request.LastName.Trim();
        employee.Email = request.Email.Trim().ToLowerInvariant();
        employee.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        employee.DepartmentId = request.DepartmentId;
        employee.DesignationId = request.DesignationId;
        employee.ManagerId = request.ManagerId;
        employee.JoiningDate = request.JoiningDate;
        employee.Status = request.Status;
    }

    private static List<string> DescribeChanges(Employee employee, SaveEmployeeRequest request)
    {
        var changes = new List<string>();
        if (employee.DepartmentId != request.DepartmentId) changes.Add("Department");
        if (employee.DesignationId != request.DesignationId) changes.Add("Designation");
        if (employee.ManagerId != request.ManagerId) changes.Add("Manager");
        if (employee.Status != request.Status) changes.Add($"Status: {employee.Status} -> {request.Status}");
        if (employee.Email != request.Email.Trim().ToLowerInvariant()) changes.Add("Email");
        return changes;
    }
}
