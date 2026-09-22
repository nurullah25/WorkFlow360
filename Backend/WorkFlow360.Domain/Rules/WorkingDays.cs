namespace WorkFlow360.Domain.Rules;

public static class WorkingDays
{
    /// <summary>Counts days in the inclusive range that are neither weekend days nor holidays.</summary>
    public static int Count(DateOnly start, DateOnly end, IReadOnlySet<DayOfWeek> weekendDays, IReadOnlySet<DateOnly> holidays)
    {
        var count = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (!weekendDays.Contains(day.DayOfWeek) && !holidays.Contains(day))
                count++;
        }
        return count;
    }
}
