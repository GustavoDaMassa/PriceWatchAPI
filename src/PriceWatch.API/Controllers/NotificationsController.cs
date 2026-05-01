using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Alertas e notificações de preço do usuário.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
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

    /// <summary>Retorna as notificações do usuário.</summary>
    /// <remarks>
    /// Notificações são geradas quando:
    /// - O preço de um produto atinge ou fica abaixo do preço-alvo (<c>type = 0</c>)
    /// - Um produto registra um novo menor preço histórico (<c>type = 1</c>)
    ///
    /// Use o query param <c>isRead</c> para filtrar: <c>?isRead=false</c> retorna apenas as não lidas.
    /// </remarks>
    /// <param name="isRead">Filtro opcional: <c>true</c> = lidas, <c>false</c> = não lidas. Omitir retorna todas.</param>
    /// <response code="200">Lista de notificações.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications([FromQuery] bool? isRead)
    {
        var result = await _getNotifications.ExecuteAsync(GetUserId(), isRead);
        return Ok(result);
    }

    /// <summary>Marca uma notificação específica como lida.</summary>
    /// <param name="id">ID da notificação.</param>
    /// <response code="204">Marcada como lida.</response>
    /// <response code="404">Notificação não encontrada ou não pertence ao usuário.</response>
    [HttpPatch("{id}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        await _markAsRead.ExecuteAsync(id, GetUserId());
        return NoContent();
    }

    /// <summary>Marca todas as notificações do usuário como lidas.</summary>
    /// <response code="204">Todas marcadas como lidas.</response>
    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _markAllAsRead.ExecuteAsync(GetUserId());
        return NoContent();
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
