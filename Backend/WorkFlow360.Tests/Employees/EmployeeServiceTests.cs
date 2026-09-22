using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Employees;

public class EmployeeServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly EmployeeService _service;
    private readonly Department _department;
    private readonly Designation _designation;

    public EmployeeServiceTests()
    {
        var db = _database.Context;
        var hrUser = TestData.AddUser(db, "hr@test.local", TestData.HrRoleId);
        var currentUser = new TestCurrentUser { Id = hrUser.Id, Role = RoleNames.HR };

        _department = TestData.AddDepartment(db);
        _designation = TestData.AddDesignation(db);
        _service = new EmployeeService(db, new AuditTrail(db, currentUser, _clock), _clock);
    }

    private SaveEmployeeRequest Request(
        string code,
        int? managerId = null,
        EmploymentStatus status = EmploymentStatus.Active) =>
        new(code, "Test", code, $"{code.ToLowerInvariant()}@test.local", null,
            _department.Id, _designation.Id, managerId, new DateOnly(2025, 6, 1), status);

    [Fact]
    public async Task Create_AddsCurrentYearLeaveBalanceForEveryActiveLeaveType()
    {
        var created = await _service.CreateAsync(Request("E2001"), default);

        using var verify = _database.CreateVerificationContext();
        var balances = await verify.LeaveBalances.Where(b => b.EmployeeId == created.Id).ToListAsync();
        var leaveTypeCount = await verify.LeaveTypes.CountAsync(t => t.IsActive);

        Assert.Equal(leaveTypeCount, balances.Count);
        Assert.All(balances, b => Assert.Equal(2026, b.Year));
        Assert.Contains(await verify.AuditLogs.ToListAsync(), a => a.Action == AuditActions.EmployeeCreated && a.EntityId == created.Id);
    }

    [Fact]
    public async Task Create_WithEmployeeCodeInDifferentCase_ThrowsConflict()
    {
        await _service.CreateAsync(Request("E2001"), default);

        var duplicate = Request("e2001") with { Email = "someone.else@test.local" };

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(duplicate, default));
    }

    [Fact]
    public async Task Update_WhenNewManagerReportsToTheEmployee_ThrowsBusinessRule()
    {
        var lead = await _service.CreateAsync(Request("E2001"), default);
        var developer = await _service.CreateAsync(Request("E2002", managerId: lead.Id), default);
        var intern = await _service.CreateAsync(Request("E2003", managerId: developer.Id), default);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateAsync(lead.Id, Request("E2001", managerId: intern.Id), default));

        Assert.Contains("reporting loop", exception.Message);
    }

    [Fact]
    public async Task Update_EndingEmploymentOfSomeoneWithDirectReports_ThrowsBusinessRule()
    {
        var lead = await _service.CreateAsync(Request("E2001"), default);
        await _service.CreateAsync(Request("E2002", managerId: lead.Id), default);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateAsync(lead.Id, Request("E2001", status: EmploymentStatus.Resigned), default));
    }

    [Fact]
    public async Task Update_EndingEmployment_DeactivatesTheUserAccountAndSignsThemOut()
    {
        var db = _database.Context;
        var user = TestData.AddUser(db, "leaver@test.local");
        var employee = TestData.AddEmployee(db, _department, _designation, "E2001", user: user);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash",
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            ExpiresAt = _clock.GetUtcNow().UtcDateTime.AddDays(7)
        });
        await db.SaveChangesAsync();

        await _service.UpdateAsync(employee.Id, Request("E2001", status: EmploymentStatus.Terminated), default);

        using var verify = _database.CreateVerificationContext();
        Assert.False((await verify.Users.SingleAsync(u => u.Id == user.Id)).IsActive);
        Assert.All(await verify.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
    }

    public void Dispose() => _database.Dispose();
}
