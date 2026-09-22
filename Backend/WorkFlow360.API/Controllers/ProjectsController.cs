using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Projects;
using WorkFlow360.Application.Tasks;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

/// <summary>Project-level permissions (only the project's manager can edit it) are enforced in ProjectService.</summary>
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;
    private readonly TaskService _taskService;

    public ProjectsController(ProjectService projectService, TaskService taskService)
    {
        _projectService = projectService;
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectListItemDto>>> List([FromQuery] ProjectQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDetailsDto>> Get(int id, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.GetAsync(id, cancellationToken));
    }

    [HttpGet("{id:int}/tasks")]
    public async Task<ActionResult<PagedResult<TaskListItemDto>>> ListTasks(int id, [FromQuery] TaskQuery query, CancellationToken cancellationToken)
    {
        query.ProjectId = id;
        return Ok(await _taskService.ListAsync(query, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<ProjectDetailsDto>> Create(SaveProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _projectService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProjectDetailsDto>> Update(int id, SaveProjectRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:int}/members")]
    public async Task<ActionResult<ProjectDetailsDto>> AddMember(int id, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.AddMemberAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}/members/{employeeId:int}")]
    public async Task<ActionResult<ProjectDetailsDto>> RemoveMember(int id, int employeeId, CancellationToken cancellationToken)
    {
        return Ok(await _projectService.RemoveMemberAsync(id, employeeId, cancellationToken));
    }
}
