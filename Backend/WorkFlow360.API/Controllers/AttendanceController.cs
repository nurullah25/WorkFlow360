using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Attendance;
using WorkFlow360.Application.Common;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendanceService;

    public AttendanceController(AttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet("today")]
    public async Task<ActionResult<TodayAttendanceDto>> Today(CancellationToken cancellationToken)
    {
        return Ok(await _attendanceService.GetTodayAsync(cancellationToken));
    }

    [HttpPost("check-in")]
    public async Task<ActionResult<TodayAttendanceDto>> CheckIn(CancellationToken cancellationToken)
    {
        return Ok(await _attendanceService.CheckInAsync(cancellationToken));
    }

    [HttpPost("check-out")]
    public async Task<ActionResult<TodayAttendanceDto>> CheckOut(CancellationToken cancellationToken)
    {
        return Ok(await _attendanceService.CheckOutAsync(cancellationToken));
    }

    /// <summary>The signed-in employee's month.</summary>
    [HttpGet("month")]
    public async Task<ActionResult<EmployeeMonthDto>> MyMonth(int? year, int? month, CancellationToken cancellationToken)
    {
        return Ok(await _attendanceService.GetMonthAsync(null, year, month, cancellationToken));
    }

    /// <summary>Another employee's month: HR/Admin for anyone, managers for their direct reports.</summary>
    [HttpGet("employees/{employeeId:int}/month")]
    public async Task<ActionResult<EmployeeMonthDto>> EmployeeMonth(int employeeId, int? year, int? month, CancellationToken cancellationToken)
    {
        return Ok(await _attendanceService.GetMonthAsync(employeeId, year, month, cancellationToken));
    }

    [HttpGet("team")]
    public async Task<ActionResult<PagedResult<TeamAttendanceRowDto>>> Team([FromQuery] TeamAttendanceQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _attendanceService.GetTeamAsync(query, cancellationToken));
    }
}
