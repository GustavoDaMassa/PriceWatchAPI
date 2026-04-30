using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly GetNotificationsUseCase _getNotifications;
    private readonly MarkAsReadUseCase _markAsRead;
    private readonly MarkAllAsReadUseCase _markAllAsRead;

    public NotificationsController(
        GetNotificationsUseCase getNotifications,
        MarkAsReadUseCase markAsRead,
        MarkAllAsReadUseCase markAllAsRead)
    {
        _getNotifications = getNotifications;
        _markAsRead = markAsRead;
        _markAllAsRead = markAllAsRead;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool? isRead)
    {
        var result = await _getNotifications.ExecuteAsync(GetUserId(), isRead);
        return Ok(result);
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        await _markAsRead.ExecuteAsync(id, GetUserId());
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _markAllAsRead.ExecuteAsync(GetUserId());
        return NoContent();
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
