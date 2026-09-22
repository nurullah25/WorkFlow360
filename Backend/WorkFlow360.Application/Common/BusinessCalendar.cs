using Microsoft.EntityFrameworkCore;
using WorkFlow360.Domain.Rules;

namespace WorkFlow360.Application.Common;

/// <summary>Working-day calculations using the company's weekend days and the Holidays table.</summary>
public class BusinessCalendar
{
    private readonly IAppDbContext _db;
    private readonly CompanyTime _companyTime;

    public BusinessCalendar(IAppDbContext db, CompanyTime companyTime)
    {
        _db = db;
        _companyTime = companyTime;
    }

    public async Task<int> CountWorkingDaysAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        if (end < start)
            return 0;

        var holidays = await GetHolidaysAsync(start, end, cancellationToken);
        return WorkingDays.Count(start, end, _companyTime.WeekendDays, holidays);
    }

    public async Task<IReadOnlySet<DateOnly>> GetHolidaysAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var dates = await _db.Holidays
            .Where(h => h.Date >= start && h.Date <= end)
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);

        return dates.ToHashSet();
    }
}
