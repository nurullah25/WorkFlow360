using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Organisation;

public class DesignationService
{
    private readonly IAppDbContext _db;
    private readonly AuditTrail _auditTrail;

    public DesignationService(IAppDbContext db, AuditTrail auditTrail)
    {
        _db = db;
        _auditTrail = auditTrail;
    }

    public async Task<IReadOnlyList<DesignationDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        return await _db.Designations
            .AsNoTracking()
            .Where(d => includeInactive || d.IsActive)
            .OrderBy(d => d.Title)
            .Select(ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<DesignationDto> CreateAsync(SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        var designation = new Designation();
        await ApplyAsync(designation, request, cancellationToken);
        _db.Designations.Add(designation);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _auditTrail.Record(AuditActions.DesignationCreated, nameof(Designation), designation.Id, new { designation.Title });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(designation.Id, cancellationToken);
    }

    public async Task<DesignationDto> UpdateAsync(int id, SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        var designation = await _db.Designations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Designation), id);

        if (designation.IsActive && !request.IsActive)
        {
            var employeeCount = await _db.Employees.WhereCurrent().CountAsync(e => e.DesignationId == id, cancellationToken);
            if (employeeCount > 0)
                throw new BusinessRuleException($"{employeeCount} current employee(s) still hold the {designation.Title} designation.");
        }

        await ApplyAsync(designation, request, cancellationToken);
        _auditTrail.Record(AuditActions.DesignationUpdated, nameof(Designation), id, new { designation.Title, designation.IsActive });
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task ApplyAsync(Designation designation, SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();

        if (await _db.Designations.AnyAsync(d => d.Id != designation.Id && d.Title == title, cancellationToken))
            throw new ConflictException($"The designation '{title}' already exists.");

        designation.Title = title;
        designation.IsActive = request.IsActive;
    }

    private async Task<DesignationDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await _db.Designations.AsNoTracking().Where(d => d.Id == id).Select(ToDto()).FirstAsync(cancellationToken);

    private Expression<Func<Designation, DesignationDto>> ToDto() =>
        d => new DesignationDto(
            d.Id,
            d.Title,
            d.IsActive,
            _db.Employees.Count(e => e.DesignationId == d.Id
                && e.Status != EmploymentStatus.Resigned
                && e.Status != EmploymentStatus.Terminated));
}
