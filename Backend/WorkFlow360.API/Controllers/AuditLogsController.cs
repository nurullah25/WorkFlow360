using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.AuditLogs;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = RoleNames.Admin)]
public class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;

    public AuditLogsController(AuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List([FromQuery] AuditLogQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _auditLogService.ListAsync(query, cancellationToken));
    }

    [HttpGet("actions")]
    public ActionResult<IReadOnlyList<string>> Actions()
    {
        return Ok(_auditLogService.GetActions());
    }
}
