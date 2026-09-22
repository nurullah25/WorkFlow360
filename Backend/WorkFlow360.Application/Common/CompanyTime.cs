using Microsoft.Extensions.Options;

namespace WorkFlow360.Application.Common;

public class CompanyOptions
{
    public const string SectionName = "Company";

    /// <summary>IANA or Windows time zone id, e.g. "Asia/Dhaka".</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Non-working days of the week. Defaults to Saturday and Sunday when not configured.</summary>
    public DayOfWeek[]? WeekendDays { get; set; }
}

/// <summary>
/// "Today" for business rules (overdue tasks, leave dates, attendance) is the company's calendar date,
/// not the server's or UTC. Timestamps are still stored in UTC.
/// </summary>
public class CompanyTime
{
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CompanyTime(TimeProvider clock, IOptions<CompanyOptions> options)
    {
        _clock = clock;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

        var weekend = options.Value.WeekendDays;
        WeekendDays = weekend is { Length: > 0 }
            ? weekend.ToHashSet()
            : new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
    }

    public IReadOnlySet<DayOfWeek> WeekendDays { get; }

    public DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public DateTime LocalNow => TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), _timeZone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _timeZone);

    public DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), _timeZone);
}
