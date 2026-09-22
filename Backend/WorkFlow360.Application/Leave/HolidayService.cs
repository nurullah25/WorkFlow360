using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Leave;

/// <summary>
/// Holidays can be hard-deleted: nothing references them. Leave already approved keeps the day count
/// it was approved with; only new requests use the updated calendar.
/// </summary>
public class HolidayService
{
    private readonly IAppDbContext _db;
    private readonly AuditTrail _auditTrail;

    public HolidayService(IAppDbContext db, AuditTrail auditTrail)
    {
        _db = db;
        _auditTrail = auditTrail;
    }

    public async Task<IReadOnlyList<HolidayDto>> ListAsync(int year, CancellationToken cancellationToken)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return await _db.Holidays
            .AsNoTracking()
            .Where(h => h.Date >= start && h.Date <= end)
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<HolidayDto> CreateAsync(CreateHolidayRequest request, CancellationToken cancellationToken)
    {
        if (await _db.Holidays.AnyAsync(h => h.Date == request.Date, cancellationToken))
            throw new ConflictException($"{request.Date:d MMM yyyy} is already a holiday.");

        var holiday = new Holiday { Date = request.Date, Name = request.Name.Trim() };
        _db.Holidays.Add(holiday);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _auditTrail.Record(AuditActions.HolidayCreated, nameof(Holiday), holiday.Id, new { holiday.Date, holiday.Name });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new HolidayDto(holiday.Id, holiday.Date, holiday.Name);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var holiday = await _db.Holidays.FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Holiday), id);

        _db.Holidays.Remove(holiday);
        _auditTrail.Record(AuditActions.HolidayDeleted, nameof(Holiday), id, new { holiday.Date, holiday.Name });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
