using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QueueLess.Api.Services;

namespace QueueLess.Api.Controllers;

[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userId = GetUserId();
        var notifications = await _notifications.GetUserNotificationsAsync(userId);
        return Ok(notifications);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = GetUserId();
        var success = await _notifications.MarkAsReadAsync(id, userId);
        return Ok(new { success });
    }
}
