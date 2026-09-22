using WorkFlow360.Application.Common;

namespace WorkFlow360.Application.Attendance;

public class AttendanceOptions
{
    public const string SectionName = "Attendance";

    /// <summary>Company-local time the working day starts.</summary>
    public TimeOnly OfficeStartTime { get; set; } = new(9, 0);

    /// <summary>A check-in after start time + grace is marked late.</summary>
    public int GraceMinutes { get; set; } = 15;
}

public enum AttendanceDayStatus
{
    Present,
    Late,
    OnLeave,
    Holiday,
    Weekend,
    Absent,
    Upcoming,
    NotEmployed
}

public record TodayAttendanceDto(
    DateOnly Date,
    bool IsWorkingDay,
    string? HolidayName,
    string? LeaveTypeName,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    bool IsLate,
    int? WorkedMinutes,
    TimeOnly OfficeStartTime,
    int GraceMinutes);

public record AttendanceDayDto(
    DateOnly Date,
    AttendanceDayStatus Status,
    bool IsWorkingDay,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    int? WorkedMinutes,
    string? Note);

public record AttendanceSummaryDto(
    int WorkingDays,
    int Present,
    int Late,
    int OnLeave,
    int Absent,
    int Holidays,
    int TotalWorkedMinutes);

public record EmployeeMonthDto(
    int EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    int Year,
    int Month,
    AttendanceSummaryDto Summary,
    IReadOnlyList<AttendanceDayDto> Days);

public record TeamAttendanceRowDto(
    int EmployeeId,
    string EmployeeName,
    string EmployeeCode,
    string DepartmentName,
    AttendanceSummaryDto Summary);

public class TeamAttendanceQuery : PagedQuery
{
    public int? Year { get; set; }
    public int? Month { get; set; }
}
