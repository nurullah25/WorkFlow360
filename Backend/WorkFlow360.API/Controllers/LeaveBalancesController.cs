using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Leave;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/leave-balances")]
[Authorize(Roles = RoleNames.AdminOrHR)]
public class LeaveBalancesController : ControllerBase
{
    private readonly LeaveBalanceService _balanceService;

    public LeaveBalancesController(LeaveBalanceService balanceService)
    {
        _balanceService = balanceService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeLeaveBalanceDto>>> List([FromQuery] LeaveBalanceQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _balanceService.ListAsync(query, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAllocation(int id, UpdateLeaveBalanceRequest request, CancellationToken cancellationToken)
    {
        await _balanceService.UpdateAllocationAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GenerateLeaveBalancesResult>> Generate(GenerateLeaveBalancesRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _balanceService.GenerateAsync(request, cancellationToken));
    }
}
