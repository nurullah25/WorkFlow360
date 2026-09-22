using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Leave;

public class LeaveTypeService
{
    private readonly IAppDbContext _db;
    private readonly AuditTrail _auditTrail;
    private readonly CompanyTime _companyTime;

    public LeaveTypeService(IAppDbContext db, AuditTrail auditTrail, CompanyTime companyTime)
    {
        _db = db;
        _auditTrail = auditTrail;
        _companyTime = companyTime;
    }

    public async Task<IReadOnlyList<LeaveTypeDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return await _db.LeaveTypes
            .AsNoTracking()
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new LeaveTypeDto(t.Id, t.Code, t.Name, t.DefaultDaysPerYear, t.IsPaid, t.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>A new leave type is allocated to every current employee for the current year straight away.</summary>
    public async Task<LeaveTypeDto> CreateAsync(SaveLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        var leaveType = new LeaveType();
        await ApplyAsync(leaveType, request, cancellationToken);
        _db.LeaveTypes.Add(leaveType);

        var year = _companyTime.Today.Year;
        var employeeIds = await _db.Employees.WhereCurrent().Select(e => e.Id).ToListAsync(cancellationToken);
        foreach (var employeeId in employeeIds)
        {
            _db.LeaveBalances.Add(new LeaveBalance
            {
                EmployeeId = employeeId,
                LeaveType = leaveType,
                Year = year,
                AllocatedDays = leaveType.DefaultDaysPerYear
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _auditTrail.Record(AuditActions.LeaveTypeCreated, nameof(LeaveType), leaveType.Id,
            new { leaveType.Code, leaveType.DefaultDaysPerYear, allocatedTo = employeeIds.Count });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToDto(leaveType);
    }

    /// <summary>Changing the default only affects balances generated later; existing balances are adjusted individually.</summary>
    public async Task<LeaveTypeDto> UpdateAsync(int id, SaveLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(LeaveType), id);

        await ApplyAsync(leaveType, request, cancellationToken);
        _auditTrail.Record(AuditActions.LeaveTypeUpdated, nameof(LeaveType), id,
            new { leaveType.Code, leaveType.DefaultDaysPerYear, leaveType.IsActive });
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(leaveType);
    }

    private async Task ApplyAsync(LeaveType leaveType, SaveLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.LeaveTypes.AnyAsync(t => t.Id != leaveType.Id && t.Code == code, cancellationToken))
            throw new ConflictException($"Leave type code {code} is already in use.");

        leaveType.Code = code;
        leaveType.Name = request.Name.Trim();
        leaveType.DefaultDaysPerYear = request.DefaultDaysPerYear;
        leaveType.IsPaid = request.IsPaid;
        leaveType.IsActive = request.IsActive;
    }

    private static LeaveTypeDto ToDto(LeaveType t) =>
        new(t.Id, t.Code, t.Name, t.DefaultDaysPerYear, t.IsPaid, t.IsActive);
}
