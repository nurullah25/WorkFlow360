using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Tasks;

namespace WorkFlow360.API.Controllers;

/// <summary>
/// Who may do what depends on the task's project (manager vs assignee), so the checks live in TaskService.
/// </summary>
[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TaskService _taskService;

    public TasksController(TaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskListItemDto>>> List([FromQuery] TaskQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDetailsDto>> Get(int id, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TaskDetailsDto>> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await _taskService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TaskDetailsDto>> Update(int id, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<TaskDetailsDto>> ChangeStatus(int id, ChangeTaskStatusRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.ChangeStatusAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<TaskCommentDto>>> ListComments(int id, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.ListCommentsAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<TaskCommentDto>> AddComment(int id, AddTaskCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await _taskService.AddCommentAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, comment);
    }

    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<IReadOnlyList<TaskHistoryDto>>> ListHistory(int id, CancellationToken cancellationToken)
    {
        return Ok(await _taskService.ListHistoryAsync(id, cancellationToken));
    }
}
