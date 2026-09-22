using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.Attendance;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Attendance;

/// <summary>Company time zone is UTC here, office starts at 09:00 with 15 minutes' grace.</summary>
public class AttendanceServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero));
    private readonly Employee _manager;
    private readonly Employee _developer;
    private readonly Employee _otherTeamMember;

    public AttendanceServiceTests()
    {
        var db = _database.Context;
        var department = TestData.AddDepartment(db);
        var designation = TestData.AddDesignation(db);

        _manager = TestData.AddEmployee(db, department, designation, "M1", user: TestData.AddUser(db, "m1@test.local", TestData.ManagerRoleId));
        _developer = TestData.AddEmployee(db, department, designation, "D1", _manager, TestData.AddUser(db, "d1@test.local"));
        _otherTeamMember = TestData.AddEmployee(db, department, designation, "X1", user: TestData.AddUser(db, "x1@test.local"));
    }

    private AttendanceService ServiceFor(Employee employee, string role = RoleNames.Employee)
    {
        var user = new TestCurrentUser { Id = employee.UserId, EmployeeId = employee.Id, Role = role };
        var companyTime = new CompanyTime(_clock, Options.Create(new CompanyOptions { TimeZone = "UTC" }));
        var options = Options.Create(new AttendanceOptions { OfficeStartTime = new TimeOnly(9, 0), GraceMinutes = 15 });
        return new AttendanceService(_database.Context, user, companyTime, options);
    }

    private void SetTime(int hour, int minute) => _clock.SetUtcNow(new DateTimeOffset(2026, 3, 3, hour, minute, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(9, 10, false)]
    [InlineData(9, 15, false)]
    [InlineData(9, 16, true)]
    public async Task CheckIn_IsLateOnlyAfterTheGracePeriod(int hour, int minute, bool expectedLate)
    {
        SetTime(hour, minute);

        var today = await ServiceFor(_developer).CheckInAsync(default);

        Assert.Equal(expectedLate, today.IsLate);
    }

    [Fact]
    public async Task CheckIn_Twice_ThrowsBusinessRule()
    {
        var service = ServiceFor(_developer);
        await service.CheckInAsync(default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CheckInAsync(default));
    }

    [Fact]
    public async Task CheckOut_RecordsTheMinutesWorked()
    {
        var service = ServiceFor(_developer);
        await service.CheckInAsync(default);

        _clock.Advance(TimeSpan.FromHours(8.5));
        var today = await service.CheckOutAsync(default);

        Assert.Equal(510, today.WorkedMinutes);
    }

    [Fact]
    public async Task CheckOut_WithoutCheckingIn_ThrowsBusinessRule()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => ServiceFor(_developer).CheckOutAsync(default));
    }

    [Fact]
    public async Task CheckIn_OnApprovedLeave_ThrowsBusinessRule()
    {
        _database.Context.LeaveRequests.Add(new LeaveRequest
        {
            EmployeeId = _developer.Id,
            LeaveTypeId = 1,
            StartDate = new DateOnly(2026, 3, 3),
            EndDate = new DateOnly(2026, 3, 3),
            TotalDays = 1,
            Reason = "Doctor",
            Status = LeaveStatus.Approved
        });
        await _database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => ServiceFor(_developer).CheckInAsync(default));
    }

    [Fact]
    public async Task GetMonth_ManagerCanSeeDirectReportsButNotOtherEmployees()
    {
        await ServiceFor(_developer).CheckInAsync(default);
        var manager = ServiceFor(_manager, RoleNames.Manager);

        var developerMonth = await manager.GetMonthAsync(_developer.Id, 2026, 3, default);

        Assert.Equal(1, developerMonth.Summary.Present);
        await Assert.ThrowsAsync<NotFoundException>(() => manager.GetMonthAsync(_otherTeamMember.Id, 2026, 3, default));
    }

    [Fact]
    public async Task GetTeam_ForManager_ListsOnlyDirectReports()
    {
        var team = await ServiceFor(_manager, RoleNames.Manager).GetTeamAsync(new TeamAttendanceQuery { Year = 2026, Month = 3 }, default);

        Assert.Equal(_developer.Id, Assert.Single(team.Items).EmployeeId);
    }

    [Fact]
    public async Task GetTeam_ForEmployee_ThrowsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => ServiceFor(_developer).GetTeamAsync(new TeamAttendanceQuery(), default));
    }

    public void Dispose() => _database.Dispose();
}
