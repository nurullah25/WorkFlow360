using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Organisation;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly DepartmentService _departmentService;

    public DepartmentsController(DepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List(bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _departmentService.ListAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<DepartmentDto>> Create(SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _departmentService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, department);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<DepartmentDto>> Update(int id, SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _departmentService.UpdateAsync(id, request, cancellationToken));
    }
}
