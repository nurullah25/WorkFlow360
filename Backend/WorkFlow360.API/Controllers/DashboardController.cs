using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Dashboard;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _dashboardService.GetAsync(cancellationToken));
    }
}
