using WorkFlow360.Application.Common.Exceptions;
using WorkFlow360.Application.Projects;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Tests.Tasks;

public class ProjectServiceTests : ProjectTestContext
{
    private SaveProjectRequest RequestWithStatus(ProjectStatus status) =>
        new(Project.Code, Project.Name, null, Manager.Id, Project.StartDate, null, status);

    private Task AddOpenTaskForDeveloperAsync() =>
        TaskServiceFor(ActingAs(Manager, RoleNames.Manager)).CreateAsync(
            new CreateTaskRequest(Project.Id, "Open task", null, Developer.Id, TaskPriority.Medium, null), default);

    [Fact]
    public async Task Update_CompletingAProjectWithOpenTasks_ThrowsBusinessRule()
    {
        await AddOpenTaskForDeveloperAsync();
        var service = ProjectServiceFor(ActingAs(Manager, RoleNames.Manager));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync(Project.Id, RequestWithStatus(ProjectStatus.Completed), default));
    }

    [Fact]
    public async Task Update_CancellingAProject_CancelsItsOpenTasks()
    {
        await AddOpenTaskForDeveloperAsync();
        var service = ProjectServiceFor(ActingAs(Manager, RoleNames.Manager));

        var project = await service.UpdateAsync(Project.Id, RequestWithStatus(ProjectStatus.Cancelled), default);

        Assert.Equal(1, project.Tasks.Cancelled);
        Assert.Equal(0, project.Tasks.Todo);
    }

    [Fact]
    public async Task RemoveMember_WhoStillHasOpenTasks_ThrowsBusinessRule()
    {
        await AddOpenTaskForDeveloperAsync();
        var service = ProjectServiceFor(ActingAs(Manager, RoleNames.Manager));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RemoveMemberAsync(Project.Id, Developer.Id, default));
    }

    [Fact]
    public async Task List_ForAnEmployee_OnlyIncludesProjectsTheyBelongTo()
    {
        var query = new ProjectQuery();

        var developerProjects = await ProjectServiceFor(ActingAs(Developer)).ListAsync(query, default);
        var outsiderProjects = await ProjectServiceFor(ActingAs(Outsider)).ListAsync(query, default);

        Assert.Single(developerProjects.Items);
        Assert.Empty(outsiderProjects.Items);
    }
}
