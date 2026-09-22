using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Application.Attendance;

/// <summary>
/// Turns one employee's raw data for a month into a status per day, and totals those days.
/// Pure and database-free, so the precedence rules below are easy to test.
/// </summary>
public static class AttendanceCalendar
{
    /// <remarks>
    /// Precedence for a day: before the employee joined → a check-in record (present / late) → public holiday →
    /// weekend → approved leave → still to come (today or later) → absent. A check-in always wins, so someone who
    /// works on a holiday or during leave is shown as present.
    /// </remarks>
    public static IReadOnlyList<AttendanceDayDto> BuildMonth(
        int year,
        int month,
        DateOnly today,
        DateOnly joiningDate,
        IReadOnlyDictionary<DateOnly, AttendanceRecord> records,
        IReadOnlyDictionary<DateOnly, string> leaveDays,
        IReadOnlyDictionary<DateOnly, string> holidays,
        IReadOnlySet<DayOfWeek> weekendDays)
    {
        var days = new List<AttendanceDayDto>();
        var first = new DateOnly(year, month, 1);

        for (var date = first; date.Month == month; date = date.AddDays(1))
        {
            var isWorkingDay = !weekendDays.Contains(date.DayOfWeek) && !holidays.ContainsKey(date);
            records.TryGetValue(date, out var record);

            var (status, note) = date < joiningDate ? (AttendanceDayStatus.NotEmployed, null)
                : record != null ? (record.IsLate ? AttendanceDayStatus.Late : AttendanceDayStatus.Present, null)
                : holidays.TryGetValue(date, out var holidayName) ? (AttendanceDayStatus.Holiday, holidayName)
                : weekendDays.Contains(date.DayOfWeek) ? (AttendanceDayStatus.Weekend, null)
                : leaveDays.TryGetValue(date, out var leaveType) ? (AttendanceDayStatus.OnLeave, leaveType)
                : date >= today ? (AttendanceDayStatus.Upcoming, (string?)null)
                : (AttendanceDayStatus.Absent, null);

            days.Add(new AttendanceDayDto(date, status, isWorkingDay, record?.CheckInAt, record?.CheckOutAt, record?.WorkedMinutes, note));
        }

        return days;
    }

    public static AttendanceSummaryDto Summarize(IReadOnlyList<AttendanceDayDto> days)
    {
        int Count(params AttendanceDayStatus[] statuses) => days.Count(d => statuses.Contains(d.Status));

        return new AttendanceSummaryDto(
            WorkingDays: days.Count(d => d.IsWorkingDay && d.Status is not (AttendanceDayStatus.Upcoming or AttendanceDayStatus.NotEmployed)),
            Present: Count(AttendanceDayStatus.Present, AttendanceDayStatus.Late),
            Late: Count(AttendanceDayStatus.Late),
            OnLeave: Count(AttendanceDayStatus.OnLeave),
            Absent: Count(AttendanceDayStatus.Absent),
            Holidays: Count(AttendanceDayStatus.Holiday),
            TotalWorkedMinutes: days.Sum(d => d.WorkedMinutes ?? 0));
    }

    /// <summary>Expands approved leave into the individual dates it covers within the month.</summary>
    public static Dictionary<DateOnly, string> ExpandLeaveDays(
        IEnumerable<(DateOnly Start, DateOnly End, string LeaveTypeName)> leaves, int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var result = new Dictionary<DateOnly, string>();

        foreach (var (start, end, leaveTypeName) in leaves)
        {
            for (var date = Max(start, monthStart); date <= Min(end, monthEnd); date = date.AddDays(1))
                result[date] = leaveTypeName;
        }

        return result;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
