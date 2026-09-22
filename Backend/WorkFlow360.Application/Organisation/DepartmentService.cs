using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Organisation;

public class DepartmentService
{
    private readonly IAppDbContext _db;
    private readonly AuditTrail _auditTrail;

    public DepartmentService(IAppDbContext db, AuditTrail auditTrail)
    {
        _db = db;
        _auditTrail = auditTrail;
    }

    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return await _db.Departments
            .AsNoTracking()
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.Name)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<DepartmentDto> CreateAsync(SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = new Department();
        await ApplyAsync(department, request, cancellationToken);
        _db.Departments.Add(department);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _auditTrail.Record(AuditActions.DepartmentCreated, nameof(Department), department.Id, new { department.Code, department.Name });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(department.Id, cancellationToken);
    }

    public async Task<DepartmentDto> UpdateAsync(int id, SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Department), id);

        if (department.IsActive && !request.IsActive)
        {
            var employeeCount = await _db.Employees.WhereCurrent().CountAsync(e => e.DepartmentId == id, cancellationToken);
            if (employeeCount > 0)
                throw new BusinessRuleException($"Move the {employeeCount} employee(s) in {department.Name} to another department before deactivating it.");
        }

        await ApplyAsync(department, request, cancellationToken);
        _auditTrail.Record(AuditActions.DepartmentUpdated, nameof(Department), id, new { department.Code, department.Name, department.IsActive });
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task ApplyAsync(Department department, SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (await _db.Departments.AnyAsync(d => d.Id != department.Id && d.Code == code, cancellationToken))
            throw new ConflictException($"A department with code '{code}' already exists.");

        if (await _db.Departments.AnyAsync(d => d.Id != department.Id && d.Name == name, cancellationToken))
            throw new ConflictException($"A department named '{name}' already exists.");

        department.Code = code;
        department.Name = name;
        department.IsActive = request.IsActive;
    }

    private async Task<DepartmentDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await _db.Departments.AsNoTracking().Where(d => d.Id == id).Select(ToDto()).FirstAsync(cancellationToken);

    private static Expression<Func<Department, DepartmentDto>> ToDto() =>
        d => new DepartmentDto(
            d.Id,
            d.Code,
            d.Name,
            d.IsActive,
            d.Employees.Count(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated));
}
