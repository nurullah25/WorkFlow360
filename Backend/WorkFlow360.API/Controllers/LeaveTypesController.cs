using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Leave;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/leave-types")]
public class LeaveTypesController : ControllerBase
{
    private readonly LeaveTypeService _leaveTypeService;

    public LeaveTypesController(LeaveTypeService leaveTypeService)
    {
        _leaveTypeService = leaveTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeDto>>> List(bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _leaveTypeService.ListAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<LeaveTypeDto>> Create(SaveLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        var leaveType = await _leaveTypeService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, leaveType);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<LeaveTypeDto>> Update(int id, SaveLeaveTypeRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _leaveTypeService.UpdateAsync(id, request, cancellationToken));
    }
}
