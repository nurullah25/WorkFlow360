using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Organisation;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/designations")]
public class DesignationsController : ControllerBase
{
    private readonly DesignationService _designationService;

    public DesignationsController(DesignationService designationService)
    {
        _designationService = designationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DesignationDto>>> List(bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _designationService.ListAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<DesignationDto>> Create(SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        var designation = await _designationService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, designation);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.AdminOrHR)]
    public async Task<ActionResult<DesignationDto>> Update(int id, SaveDesignationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _designationService.UpdateAsync(id, request, cancellationToken));
    }
}
