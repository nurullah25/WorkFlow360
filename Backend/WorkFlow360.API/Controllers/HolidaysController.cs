using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Leave;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/holidays")]
public class HolidaysController : ControllerBase
{
    private readonly HolidayService _holidayService;
    private readonly CompanyTime _companyTime;

    public HolidaysController(HolidayService holidayService, CompanyTime companyTime)
    {
        _holidayService = holidayService;
        _companyTime = companyTime;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> List(int? year, CancellationToken cancellationToken)
    {
        return Ok(await _holidayService.ListAsync(year ?? _companyTime.Today.Year, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<HolidayDto>> Create(CreateHolidayRequest request, CancellationToken cancellationToken)
    {
        var holiday = await _holidayService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, holiday);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _holidayService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
