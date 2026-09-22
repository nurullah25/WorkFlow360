using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Notifications;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List([FromQuery] NotificationQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.ListAsync(query, cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetUnreadCountAsync(cancellationToken));
    }

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkReadAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
