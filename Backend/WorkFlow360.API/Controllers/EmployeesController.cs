using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Employees;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

/// <summary>
/// There is no DELETE: employees are never removed because tasks, leave and attendance reference them.
/// Employment ends by setting the status to Resigned or Terminated.
/// </summary>
[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly EmployeeService _employeeService;

    public EmployeesController(EmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<EmployeeListItemDto>>> List([FromQuery] EmployeeQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.ListAsync(query, cancellationToken));
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<IReadOnlyList<EmployeeLookupDto>>> Lookup(string? search, bool withoutUserAccount, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.LookupAsync(search, withoutUserAccount, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDetailsDto>> Get(int id, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<EmployeeDetailsDto>> Create(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<EmployeeDetailsDto>> Update(int id, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.UpdateAsync(id, request, cancellationToken));
    }
}
