using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Leave;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Leave;

public class LeaveBalanceServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 12, 20, 9, 0, 0, TimeSpan.Zero));
    private readonly LeaveBalanceService _service;
    private readonly Employee _employee;

    public LeaveBalanceServiceTests()
    {
        var db = _database.Context;
        var hr = TestData.AddUser(db, "hr@test.local", TestData.HrRoleId);
        var department = TestData.AddDepartment(db);
        var designation = TestData.AddDesignation(db);
        _employee = TestData.AddEmployee(db, department, designation, "E1");

        var leaver = TestData.AddEmployee(db, department, designation, "E2");
        leaver.Status = EmploymentStatus.Resigned;
        db.SaveChanges();

        var currentUser = new TestCurrentUser { Id = hr.Id, Role = RoleNames.HR };
        _service = new LeaveBalanceService(
            db,
            new AuditTrail(db, currentUser, _clock),
            new CompanyTime(_clock, Options.Create(new CompanyOptions { TimeZone = "UTC" })));
    }

    [Fact]
    public async Task Generate_CreatesOneBalancePerCurrentEmployeeAndActiveLeaveType_AndIsSafeToRerun()
    {
        var activeTypes = await _database.Context.LeaveTypes.CountAsync(t => t.IsActive);

        var first = await _service.GenerateAsync(new GenerateLeaveBalancesRequest(2027), default);
        var second = await _service.GenerateAsync(new GenerateLeaveBalancesRequest(2027), default);

        Assert.Equal(activeTypes, first.Created);
        Assert.Equal(0, second.Created);

        using var verify = _database.CreateVerificationContext();
        Assert.All(await verify.LeaveBalances.Where(b => b.Year == 2027).ToListAsync(), b => Assert.Equal(_employee.Id, b.EmployeeId));
    }

    [Fact]
    public async Task UpdateAllocation_BelowDaysAlreadyUsed_ThrowsBusinessRule()
    {
        var balance = new LeaveBalance { EmployeeId = _employee.Id, LeaveTypeId = 1, Year = 2026, AllocatedDays = 18, UsedDays = 6 };
        _database.Context.LeaveBalances.Add(balance);
        await _database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateAllocationAsync(balance.Id, new UpdateLeaveBalanceRequest(5), default));
    }

    public void Dispose() => _database.Dispose();
}
