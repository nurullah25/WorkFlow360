using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Users;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Infrastructure.Auth;

namespace WorkFlow360.Tests.Users;

public class UserServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly UserService _service;
    private readonly User _admin;

    public UserServiceTests()
    {
        var db = _database.Context;
        _admin = TestData.AddUser(db, "admin@test.local", TestData.AdminRoleId);
        var currentUser = new TestCurrentUser { Id = _admin.Id, Role = RoleNames.Admin };

        _service = new UserService(
            db,
            new IdentityPasswordHasher(),
            new AuditTrail(db, currentUser, _clock),
            currentUser,
            _clock);
    }

    [Fact]
    public async Task Update_AdminRemovingTheirOwnAdminRole_ThrowsBusinessRule()
    {
        var request = new UpdateUserRequest("Admin", RoleNames.Manager, IsActive: true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateAsync(_admin.Id, request, default));
    }

    [Fact]
    public async Task Update_DeactivatingAnotherUser_RevokesTheirSessions()
    {
        var db = _database.Context;
        var user = TestData.AddUser(db, "rafiq@test.local");
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = "hash", ExpiresAt = DateTime.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();

        await _service.UpdateAsync(user.Id, new UpdateUserRequest("Rafiq", RoleNames.Employee, IsActive: false), default);

        using var verify = _database.CreateVerificationContext();
        Assert.NotNull((await verify.RefreshTokens.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Create_WithEmployeeId_LinksTheAccountToTheEmployee()
    {
        var db = _database.Context;
        var employee = TestData.AddEmployee(db, TestData.AddDepartment(db), TestData.AddDesignation(db), "E3001");

        var created = await _service.CreateAsync(
            new CreateUserRequest("E3001@Test.local", "Test E3001", RoleNames.Employee, "Welcome123", employee.Id), default);

        using var verify = _database.CreateVerificationContext();
        Assert.Equal(created.Id, (await verify.Employees.SingleAsync(e => e.Id == employee.Id)).UserId);
        Assert.Equal("e3001@test.local", created.Email);
        Assert.Contains(await verify.AuditLogs.ToListAsync(), a => a.Action == AuditActions.UserCreated);
    }

    [Fact]
    public async Task Create_ForEmployeeWhoAlreadyHasAnAccount_ThrowsConflict()
    {
        var db = _database.Context;
        var existing = TestData.AddUser(db, "existing@test.local");
        var employee = TestData.AddEmployee(db, TestData.AddDepartment(db), TestData.AddDesignation(db), "E3001", user: existing);

        var request = new CreateUserRequest("second@test.local", "Second", RoleNames.Employee, "Welcome123", employee.Id);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(request, default));
    }

    public void Dispose() => _database.Dispose();
}
