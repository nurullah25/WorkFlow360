using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Leave;

/// <summary>HR's view of every employee's balances: adjust individual allocations and open a new leave year.</summary>
public class LeaveBalanceService
{
    private readonly IAppDbContext _db;
    private readonly AuditTrail _auditTrail;
    private readonly CompanyTime _companyTime;

    public LeaveBalanceService(IAppDbContext db, AuditTrail auditTrail, CompanyTime companyTime)
    {
        _db = db;
        _auditTrail = auditTrail;
        _companyTime = companyTime;
    }

    public async Task<PagedResult<EmployeeLeaveBalanceDto>> ListAsync(LeaveBalanceQuery query, CancellationToken cancellationToken)
    {
        var year = query.Year ?? _companyTime.Today.Year;
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var balances = _db.LeaveBalances.AsNoTracking().Where(b => b.Year == year);

        if (query.LeaveTypeId.HasValue)
            balances = balances.Where(b => b.LeaveTypeId == query.LeaveTypeId);

        if (query.SearchTerm is { } term)
        {
            balances = balances.Where(b =>
                (b.Employee.FirstName + " " + b.Employee.LastName).Contains(term) || b.Employee.EmployeeCode.Contains(term));
        }

        return await balances
            .OrderBy(b => b.Employee.FirstName).ThenBy(b => b.Employee.LastName).ThenBy(b => b.LeaveType.Name)
            .Select(b => new EmployeeLeaveBalanceDto(
                b.Id,
                b.EmployeeId,
                b.Employee.FirstName + " " + b.Employee.LastName,
                b.Employee.EmployeeCode,
                b.LeaveType.Name,
                b.Year,
                b.AllocatedDays,
                b.UsedDays,
                _db.LeaveRequests
                    .Where(r => r.EmployeeId == b.EmployeeId
                        && r.LeaveTypeId == b.LeaveTypeId
                        && r.Status == LeaveStatus.Pending
                        && r.StartDate >= yearStart && r.StartDate <= yearEnd)
                    .Sum(r => (decimal?)r.TotalDays) ?? 0))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task UpdateAllocationAsync(int id, UpdateLeaveBalanceRequest request, CancellationToken cancellationToken)
    {
        var balance = await _db.LeaveBalances.Include(b => b.LeaveType).FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveBalance), id);

        var (yearStart, yearEnd) = (new DateOnly(balance.Year, 1, 1), new DateOnly(balance.Year, 12, 31));
        var pending = await _db.LeaveRequests
            .Where(r => r.EmployeeId == balance.EmployeeId
                && r.LeaveTypeId == balance.LeaveTypeId
                && r.Status == LeaveStatus.Pending
                && r.StartDate >= yearStart && r.StartDate <= yearEnd)
            .SumAsync(r => (decimal?)r.TotalDays, cancellationToken) ?? 0;

        if (request.AllocatedDays < balance.UsedDays + pending)
            throw new BusinessRuleException(
                $"The allocation can't be lower than the {balance.UsedDays + pending:0.#} day(s) already used or pending.");

        _auditTrail.Record(AuditActions.LeaveBalanceAdjusted, nameof(LeaveBalance), id,
            new { balance.EmployeeId, balance.LeaveType.Code, From = balance.AllocatedDays, To = request.AllocatedDays });
        balance.AllocatedDays = request.AllocatedDays;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the missing balances for a year (every current employee × every active leave type) using each
    /// type's default allocation. Safe to run more than once; existing balances are left alone.
    /// </summary>
    public async Task<GenerateLeaveBalancesResult> GenerateAsync(GenerateLeaveBalancesRequest request, CancellationToken cancellationToken)
    {
        var employeeIds = await _db.Employees.WhereCurrent().Select(e => e.Id).ToListAsync(cancellationToken);
        var leaveTypes = await _db.LeaveTypes.Where(t => t.IsActive).ToListAsync(cancellationToken);

        var existing = (await _db.LeaveBalances
                .Where(b => b.Year == request.Year)
                .Select(b => new { b.EmployeeId, b.LeaveTypeId })
                .ToListAsync(cancellationToken))
            .Select(b => (b.EmployeeId, b.LeaveTypeId))
            .ToHashSet();

        var created = 0;
        foreach (var employeeId in employeeIds)
        {
            foreach (var leaveType in leaveTypes.Where(t => !existing.Contains((employeeId, t.Id))))
            {
                _db.LeaveBalances.Add(new LeaveBalance
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = leaveType.Id,
                    Year = request.Year,
                    AllocatedDays = leaveType.DefaultDaysPerYear
                });
                created++;
            }
        }

        _auditTrail.Record(AuditActions.LeaveBalancesGenerated, nameof(LeaveBalance), null, new { request.Year, created });
        await _db.SaveChangesAsync(cancellationToken);

        return new GenerateLeaveBalancesResult(request.Year, created);
    }
}
