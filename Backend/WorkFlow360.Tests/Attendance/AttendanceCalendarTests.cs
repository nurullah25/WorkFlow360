using WorkFlow360.Application.Attendance;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.Tests.Attendance;

/// <summary>
/// March 2026 with a Friday/Saturday weekend. The employee joined on Tuesday 3 March and "today" is Tuesday 10 March.
/// </summary>
public class AttendanceCalendarTests
{
    private static readonly HashSet<DayOfWeek> Weekend = [DayOfWeek.Friday, DayOfWeek.Saturday];
    private static readonly DateOnly Today = new(2026, 3, 10);
    private static readonly DateOnly JoiningDate = new(2026, 3, 3);

    private static IReadOnlyList<AttendanceDayDto> BuildMarch()
    {
        var records = new Dictionary<DateOnly, AttendanceRecord>
        {
            [new DateOnly(2026, 3, 3)] = new() { IsLate = false, WorkedMinutes = 480 },
            [new DateOnly(2026, 3, 4)] = new() { IsLate = true, WorkedMinutes = 450 },
        };
        var leave = new Dictionary<DateOnly, string> { [new DateOnly(2026, 3, 8)] = "Sick Leave" };
        var holidays = new Dictionary<DateOnly, string> { [new DateOnly(2026, 3, 26)] = "Independence Day" };

        return AttendanceCalendar.BuildMonth(2026, 3, Today, JoiningDate, records, leave, holidays, Weekend);
    }

    private static AttendanceDayStatus StatusOn(int day) => BuildMarch().Single(d => d.Date.Day == day).Status;

    [Theory]
    [InlineData(2, AttendanceDayStatus.NotEmployed)]
    [InlineData(3, AttendanceDayStatus.Present)]
    [InlineData(4, AttendanceDayStatus.Late)]
    [InlineData(5, AttendanceDayStatus.Absent)]
    [InlineData(6, AttendanceDayStatus.Weekend)]
    [InlineData(8, AttendanceDayStatus.OnLeave)]
    [InlineData(10, AttendanceDayStatus.Upcoming)]
    [InlineData(26, AttendanceDayStatus.Holiday)]
    public void BuildMonth_GivesEachDayTheExpectedStatus(int day, AttendanceDayStatus expected)
    {
        Assert.Equal(expected, StatusOn(day));
    }

    [Fact]
    public void BuildMonth_CoversEveryDayOfTheMonth()
    {
        Assert.Equal(31, BuildMarch().Count);
    }

    [Fact]
    public void Summarize_CountsOnlyPastWorkingDaysSinceJoining()
    {
        var summary = AttendanceCalendar.Summarize(BuildMarch());

        // Working days so far: 3, 4, 5, 8 and 9 March (6–7 are the weekend, 10 is today).
        Assert.Equal(5, summary.WorkingDays);
        Assert.Equal(2, summary.Present);
        Assert.Equal(1, summary.Late);
        Assert.Equal(1, summary.OnLeave);
        Assert.Equal(2, summary.Absent);
        Assert.Equal(930, summary.TotalWorkedMinutes);
    }

    [Fact]
    public void ExpandLeaveDays_ClipsLeaveToTheMonth()
    {
        var days = AttendanceCalendar.ExpandLeaveDays(
            [(new DateOnly(2026, 2, 26), new DateOnly(2026, 3, 2), "Annual Leave")], 2026, 3);

        Assert.Equal([new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2)], days.Keys.Order());
    }
}
