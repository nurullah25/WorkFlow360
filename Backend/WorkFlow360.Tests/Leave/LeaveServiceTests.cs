using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Leave;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;
using WorkFlow360.Infrastructure.Persistence;

namespace WorkFlow360.Tests.Leave;

/// <summary>
/// Calendar used throughout: "today" is Monday 2 March 2026, the weekend is Friday + Saturday,
/// and Thursday 26 March is a public holiday. Everyone has 18 days of Annual Leave (seeded type id 1).
/// </summary>
public class LeaveServiceTests : IDisposable
{
    private const int AnnualLeaveTypeId = 1;

    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));

    private readonly Employee _manager;
    private readonly Employee _developer;
    private readonly Employee _otherManager;
    private readonly Employee _hrOfficer;
    private readonly Employee _accountant;

    public LeaveServiceTests()
    {
        var db = _database.Context;
        var department = TestData.AddDepartment(db);
        var designation = TestData.AddDesignation(db);

        _manager = TestData.AddEmployee(db, department, designation, "M1", user: TestData.AddUser(db, "m1@test.local", TestData.ManagerRoleId));
        _otherManager = TestData.AddEmployee(db, department, designation, "M2", user: TestData.AddUser(db, "m2@test.local", TestData.ManagerRoleId));
        _developer = TestData.AddEmployee(db, department, designation, "D1", _manager, TestData.AddUser(db, "d1@test.local"));
        _hrOfficer = TestData.AddEmployee(db, department, designation, "H1", user: TestData.AddUser(db, "h1@test.local", TestData.HrRoleId));
        // No manager: HR reviews their leave.
        _accountant = TestData.AddEmployee(db, department, designation, "F1", user: TestData.AddUser(db, "f1@test.local"));

        db.Holidays.Add(new Holiday { Date = new DateOnly(2026, 3, 26), Name = "Independence Day" });
        foreach (var employee in new[] { _manager, _otherManager, _developer, _hrOfficer, _accountant })
        {
            db.LeaveBalances.Add(new LeaveBalance { EmployeeId = employee.Id, LeaveTypeId = AnnualLeaveTypeId, Year = 2026, AllocatedDays = 18 });
        }
        db.SaveChanges();
    }

    private LeaveService ServiceFor(Employee employee, string role = RoleNames.Employee, AppDbContext? context = null)
    {
        var db = context ?? _database.Context;
        var user = new TestCurrentUser { Id = employee.UserId, EmployeeId = employee.Id, Role = role };
        var companyTime = new CompanyTime(_clock, Options.Create(new CompanyOptions
        {
            TimeZone = "UTC",
            WeekendDays = [DayOfWeek.Friday, DayOfWeek.Saturday]
        }));

        return new LeaveService(
            db,
            user,
            new AuditTrail(db, user, _clock),
            new NotificationSender(db, user, _clock),
            companyTime,
            new BusinessCalendar(db, companyTime));
    }

    private static SubmitLeaveRequest Request(DateOnly start, DateOnly end) =>
        new(AnnualLeaveTypeId, start, end, "Family event");

    private Task<LeaveRequestDto> SubmitAsDeveloperAsync(DateOnly start, DateOnly end) =>
        ServiceFor(_developer).SubmitAsync(Request(start, end), default);

    [Fact]
    public async Task Submit_CountsOnlyWorkingDays()
    {
        // Sun 22 – Sat 28 March: Thu 26 is a holiday, Fri/Sat are the weekend.
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 22), new DateOnly(2026, 3, 28));

        Assert.Equal(4, leave.TotalDays);
        Assert.Equal(LeaveStatus.Pending, leave.Status);
    }

    [Fact]
    public async Task Submit_NotifiesTheEmployeesManager()
    {
        await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9));

        using var verify = _database.CreateVerificationContext();
        var notification = await verify.Notifications.SingleAsync();
        Assert.Equal(_manager.UserId, notification.UserId);
        Assert.Equal(NotificationType.LeaveSubmitted, notification.Type);
    }

    [Fact]
    public async Task Submit_ForEmployeeWithoutManager_NotifiesHr()
    {
        await ServiceFor(_accountant).SubmitAsync(Request(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 9)), default);

        using var verify = _database.CreateVerificationContext();
        Assert.Equal(_hrOfficer.UserId, (await verify.Notifications.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Submit_BeyondBalanceIncludingPendingRequests_ThrowsBusinessRule()
    {
        // 10 working days pending: 8–12 March and 15–19 March (Sun–Thu weeks).
        await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 12));
        await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 19));

        // Another 10 days would take 20 of the 18 allocated.
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => SubmitAsDeveloperAsync(new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 16)));

        Assert.Contains("8 day(s) available", exception.Message);
    }

    [Fact]
    public async Task Submit_OverlappingAnExistingRequest_ThrowsBusinessRule()
    {
        await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => SubmitAsDeveloperAsync(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 11)));
    }

    [Fact]
    public async Task Submit_StartingInThePast_ThrowsBusinessRule()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => SubmitAsDeveloperAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3)));
    }

    [Fact]
    public async Task Approve_ByDirectManager_UsesTheBalanceAndNotifiesTheEmployee()
    {
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10));

        var approved = await ServiceFor(_manager, RoleNames.Manager).ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default);

        Assert.Equal(LeaveStatus.Approved, approved.Status);
        using var verify = _database.CreateVerificationContext();
        Assert.Equal(3, (await verify.LeaveBalances.SingleAsync(b => b.EmployeeId == _developer.Id)).UsedDays);
        Assert.Contains(await verify.Notifications.ToListAsync(),
            n => n.UserId == _developer.UserId && n.Type == NotificationType.LeaveApproved);
        Assert.Contains(await verify.AuditLogs.ToListAsync(), a => a.Action == AuditActions.LeaveApproved);
    }

    [Fact]
    public async Task Approve_ByManagerOfAnotherTeam_ThrowsForbidden()
    {
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8));

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => ServiceFor(_otherManager, RoleNames.Manager).ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default));
    }

    [Fact]
    public async Task Approve_OwnRequest_ThrowsForbiddenEvenForHr()
    {
        var hr = ServiceFor(_hrOfficer, RoleNames.HR);
        var leave = await hr.SubmitAsync(Request(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8)), default);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => hr.ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default));
    }

    [Fact]
    public async Task Approve_RequestAlreadyRejected_ThrowsBusinessRule()
    {
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8));
        var manager = ServiceFor(_manager, RoleNames.Manager);
        await manager.RejectAsync(leave.Id, new RejectLeaveRequest("Release week"), default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => manager.ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default));
    }

    [Fact]
    public async Task Approve_TwoReviewersAtTheSameTime_SecondSaveFails()
    {
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8));

        // Two separate DbContexts (two HTTP requests) both load the request while it is still Pending.
        using var managerContext = _database.CreateVerificationContext();
        using var adminContext = _database.CreateVerificationContext();
        var managerService = ServiceFor(_manager, RoleNames.Manager, managerContext);
        var adminService = ServiceFor(_otherManager, RoleNames.Admin, adminContext);

        var adminLoaded = await adminContext.LeaveRequests.Include(r => r.Employee).Include(r => r.LeaveType).SingleAsync(r => r.Id == leave.Id);
        await managerService.ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => adminService.RejectAsync(adminLoaded.Id, new RejectLeaveRequest("Too late"), default));
    }

    [Fact]
    public async Task Cancel_ApprovedLeaveInTheFuture_ReturnsTheDaysToTheBalance()
    {
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10));
        await ServiceFor(_manager, RoleNames.Manager).ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default);

        var cancelled = await ServiceFor(_developer).CancelAsync(leave.Id, default);

        Assert.Equal(LeaveStatus.Cancelled, cancelled.Status);
        using var verify = _database.CreateVerificationContext();
        Assert.Equal(0, (await verify.LeaveBalances.SingleAsync(b => b.EmployeeId == _developer.Id)).UsedDays);
    }

    [Fact]
    public async Task Cancel_ApprovedLeaveThatHasStarted_ThrowsBusinessRule()
    {
        var leave = await SubmitAsDeveloperAsync(new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 10));
        await ServiceFor(_manager, RoleNames.Manager).ApproveAsync(leave.Id, new ApproveLeaveRequest(null), default);

        _clock.SetUtcNow(new DateTimeOffset(2026, 3, 9, 9, 0, 0, TimeSpan.Zero));

        await Assert.ThrowsAsync<BusinessRuleException>(() => ServiceFor(_developer).CancelAsync(leave.Id, default));
    }

    public void Dispose() => _database.Dispose();
}
