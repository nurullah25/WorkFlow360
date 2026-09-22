using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Leave;

namespace WorkFlow360.API.Controllers;

/// <summary>
/// Who can review a request depends on the employee's reporting line, so those checks live in LeaveService
/// rather than in [Authorize] attributes.
/// </summary>
[ApiController]
[Route("api/leaves")]
public class LeavesController : ControllerBase
{
    private readonly LeaveService _leaveService;

    public LeavesController(LeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    /// <summary>scope: "mine" (default), "approvals" or "team".</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<LeaveRequestDto>>> List([FromQuery] LeaveRequestQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _leaveService.ListAsync(query, cancellationToken));
    }

    [HttpGet("my-balances")]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceDto>>> MyBalances(int? year, CancellationToken cancellationToken)
    {
        return Ok(await _leaveService.GetMyBalancesAsync(year, cancellationToken));
    }

    [HttpGet("preview")]
    public async Task<ActionResult<LeavePreviewDto>> Preview(int leaveTypeId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        return Ok(await _leaveService.PreviewAsync(leaveTypeId, startDate, endDate, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<LeaveRequestDto>> Submit(SubmitLeaveRequest request, CancellationToken cancellationToken)
    {
        var leave = await _leaveService.SubmitAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, leave);
    }

    [HttpPut("{id:int}/approve")]
    public async Task<ActionResult<LeaveRequestDto>> Approve(int id, ApproveLeaveRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _leaveService.ApproveAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/reject")]
    public async Task<ActionResult<LeaveRequestDto>> Reject(int id, RejectLeaveRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _leaveService.RejectAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<LeaveRequestDto>> Cancel(int id, CancellationToken cancellationToken)
    {
        return Ok(await _leaveService.CancelAsync(id, cancellationToken));
    }
}
