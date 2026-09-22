using Microsoft.Extensions.Options;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Dashboard;
using WorkFlow360.Application.Leave;
using WorkFlow360.Application.Notifications;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Tasks;

public class DashboardServiceTests : ProjectTestContext
{
    private DashboardService DashboardFor(TestCurrentUser user)
    {
        var db = Database.Context;
        var companyTime = new CompanyTime(Clock, Options.Create(new CompanyOptions { TimeZone = "UTC" }));
        var leaveService = new LeaveService(
            db, user, new AuditTrail(db, user, Clock), new NotificationSender(db, user, Clock), companyTime, new BusinessCalendar(db, companyTime));

        return new DashboardService(db, user, companyTime, leaveService, TaskServiceFor(user));
    }

    private async Task AddTaskForDeveloperAsync(string title, DateOnly dueDate)
    {
        await TaskServiceFor(ActingAs(Manager, RoleNames.Manager))
            .CreateAsync(new CreateTaskRequest(Project.Id, title, null, Developer.Id, TaskPriority.High, dueDate), default);
    }

    [Fact]
    public async Task Employee_GetsTheirOwnSectionButNoCompanyOrTeamFigures()
    {
        await AddTaskForDeveloperAsync("Due soon", new DateOnly(2026, 3, 5));

        var dashboard = await DashboardFor(ActingAs(Developer)).GetAsync(default);

        Assert.Null(dashboard.Company);
        Assert.Null(dashboard.Team);
        Assert.NotNull(dashboard.Me);
        Assert.Equal(1, dashboard.Me.OpenTasks);
        Assert.Equal("Due soon", Assert.Single(dashboard.Me.DueSoon).Title);
    }

    [Fact]
    public async Task Manager_GetsTeamSectionWithProjectProgressAndOverdueWork()
    {
        await AddTaskForDeveloperAsync("Will be overdue", new DateOnly(2026, 3, 5));
        Clock.Advance(TimeSpan.FromDays(10));

        var dashboard = await DashboardFor(ActingAs(Manager, RoleNames.Manager)).GetAsync(default);

        Assert.Null(dashboard.Company);
        Assert.NotNull(dashboard.Team);
        Assert.Equal(1, dashboard.Team.DirectReports);
        var project = Assert.Single(dashboard.Team.Projects);
        Assert.Equal(1, project.TotalTasks);
        Assert.Equal(1, project.OverdueTasks);
    }

    [Fact]
    public async Task Hr_GetsCompanyHeadcountByDepartment()
    {
        var dashboard = await DashboardFor(ActingAs(Outsider, RoleNames.HR)).GetAsync(default);

        Assert.NotNull(dashboard.Company);
        Assert.Equal(3, dashboard.Company.ActiveEmployees);
        Assert.Equal(3, Assert.Single(dashboard.Company.Headcount).Employees);
    }
}
