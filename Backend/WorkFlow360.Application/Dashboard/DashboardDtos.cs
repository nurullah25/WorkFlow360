using WorkFlow360.Application.Leave;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Application.Dashboard;

/// <summary>Each section is null when it doesn't apply to the signed-in user.</summary>
public record DashboardDto(
    CompanyOverviewDto? Company,
    TeamOverviewDto? Team,
    MyOverviewDto? Me,
    IReadOnlyList<HolidayDto> UpcomingHolidays);

/// <summary>Admin and HR.</summary>
public record CompanyOverviewDto(
    int ActiveEmployees,
    int CheckedInToday,
    int LateToday,
    int OnLeaveToday,
    int PendingLeaveRequests,
    int ActiveProjects,
    int OpenTasks,
    int OverdueTasks,
    IReadOnlyList<DepartmentHeadcountDto> Headcount);

public record DepartmentHeadcountDto(string Department, int Employees);

/// <summary>Anyone with direct reports or projects they manage.</summary>
public record TeamOverviewDto(
    int DirectReports,
    int CheckedInToday,
    int OnLeaveToday,
    int PendingApprovals,
    IReadOnlyList<ProjectProgressDto> Projects);

public record ProjectProgressDto(int Id, string Code, string Name, int DoneTasks, int TotalTasks, int OverdueTasks);

/// <summary>Anyone with an employee record.</summary>
public record MyOverviewDto(
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    bool IsLate,
    bool IsWorkingDay,
    string? OnLeaveToday,
    int OpenTasks,
    int OverdueTasks,
    IReadOnlyList<TaskListItemDto> DueSoon,
    IReadOnlyList<LeaveBalanceDto> LeaveBalances,
    IReadOnlyList<UpcomingLeaveDto> UpcomingLeave);

public record UpcomingLeaveDto(int Id, string LeaveTypeName, DateOnly StartDate, DateOnly EndDate, decimal TotalDays, LeaveStatus Status);
