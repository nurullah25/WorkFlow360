using WorkFlow360.Domain.Rules;

namespace WorkFlow360.Tests.Leave;

public class WorkingDaysTests
{
    private static readonly HashSet<DayOfWeek> FridaySaturdayWeekend = [DayOfWeek.Friday, DayOfWeek.Saturday];

    [Fact]
    public void Count_SkipsWeekendDays()
    {
        // Sunday 22 March to Saturday 28 March 2026: Friday and Saturday are the weekend.
        var days = WorkingDays.Count(new DateOnly(2026, 3, 22), new DateOnly(2026, 3, 28), FridaySaturdayWeekend, new HashSet<DateOnly>());

        Assert.Equal(5, days);
    }

    [Fact]
    public void Count_SkipsHolidays()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 3, 26) };

        var days = WorkingDays.Count(new DateOnly(2026, 3, 22), new DateOnly(2026, 3, 28), FridaySaturdayWeekend, holidays);

        Assert.Equal(4, days);
    }

    [Fact]
    public void Count_SingleWorkingDay_IsOne()
    {
        Assert.Equal(1, WorkingDays.Count(new DateOnly(2026, 3, 23), new DateOnly(2026, 3, 23), FridaySaturdayWeekend, new HashSet<DateOnly>()));
    }

    [Fact]
    public void Count_EndBeforeStart_IsZero()
    {
        Assert.Equal(0, WorkingDays.Count(new DateOnly(2026, 3, 23), new DateOnly(2026, 3, 22), FridaySaturdayWeekend, new HashSet<DateOnly>()));
    }
}
