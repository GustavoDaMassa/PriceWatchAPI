using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Devices;
using PriceWatch.Application.UseCases.Devices;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Dispositivos do usuário para receber alertas por push.</summary>
[ApiController]
[Route("api/devices")]
[Authorize]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly RegisterDeviceUseCase _register;
    private readonly UnregisterDeviceUseCase _unregister;

    public DevicesController(RegisterDeviceUseCase register, UnregisterDeviceUseCase unregister)
    {
        _register = register;
        _unregister = unregister;
    }

    /// <summary>Registra (ou renova) o token de push do dispositivo.</summary>
    /// <remarks>
    /// Idempotente: o app pode chamar a cada abertura. Se o token estava em outra conta, passa para esta.
    /// Plataformas: android, ios, web.
    /// </remarks>
    /// <response code="201">Dispositivo registrado.</response>
    /// <response code="200">Dispositivo já registrado (renovado).</response>
    /// <response code="400">Token ou plataforma inválidos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        var result = await _register.ExecuteAsync(GetUserId(), request);
        return StatusCode(result.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK, result.Device);
    }

    /// <summary>Remove o dispositivo (ex.: ao sair da conta no app).</summary>
    /// <param name="id">ID devolvido no registro.</param>
    /// <response code="204">Removido.</response>
    /// <response code="404">Dispositivo não encontrado ou de outro usuário.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unregister(string id)
    {
        await _unregister.ExecuteAsync(GetUserId(), id);
        return NoContent();
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
