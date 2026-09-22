using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Application.Projects;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Tasks;

/// <summary>
/// One project managed by <see cref="Manager"/> with <see cref="Developer"/> as a member,
/// plus <see cref="Outsider"/> who is not on the project.
/// </summary>
public abstract class ProjectTestContext : IDisposable
{
    protected readonly TestDatabase Database = new();
    protected readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));
    protected readonly Employee Manager;
    protected readonly Employee Developer;
    protected readonly Employee Outsider;
    protected readonly Project Project;

    protected ProjectTestContext()
    {
        var db = Database.Context;
        var department = TestData.AddDepartment(db);
        var designation = TestData.AddDesignation(db);

        Manager = TestData.AddEmployee(db, department, designation, "M1", user: TestData.AddUser(db, "manager@test.local", TestData.ManagerRoleId));
        Developer = TestData.AddEmployee(db, department, designation, "D1", Manager, TestData.AddUser(db, "dev@test.local"));
        Outsider = TestData.AddEmployee(db, department, designation, "O1", user: TestData.AddUser(db, "outsider@test.local"));

        Project = new Project
        {
            Code = "HRP",
            Name = "HR Portal",
            ManagerId = Manager.Id,
            StartDate = new DateOnly(2026, 1, 1),
            Status = ProjectStatus.Active,
            CreatedByUserId = Manager.UserId!.Value
        };
        Project.Members.Add(new ProjectMember { EmployeeId = Developer.Id, AddedAt = DateTime.UtcNow });
        db.Projects.Add(Project);
        db.SaveChanges();
    }

    protected static TestCurrentUser ActingAs(Employee employee, string role = RoleNames.Employee) =>
        new() { Id = employee.UserId, EmployeeId = employee.Id, Role = role };

    protected TaskService TaskServiceFor(TestCurrentUser user)
    {
        var (access, audit, notifications, companyTime) = Dependencies(user);
        return new TaskService(Database.Context, access, user, audit, notifications, companyTime);
    }

    protected ProjectService ProjectServiceFor(TestCurrentUser user)
    {
        var (access, audit, notifications, companyTime) = Dependencies(user);
        return new ProjectService(Database.Context, access, user, audit, notifications, companyTime);
    }

    private (ProjectAccess, AuditTrail, NotificationSender, CompanyTime) Dependencies(TestCurrentUser user)
    {
        var db = Database.Context;
        return (
            new ProjectAccess(db, user),
            new AuditTrail(db, user, Clock),
            new NotificationSender(db, user, Clock),
            new CompanyTime(Clock, Options.Create(new CompanyOptions { TimeZone = "UTC" })));
    }

    public void Dispose() => Database.Dispose();
}
