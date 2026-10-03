using PriceWatch.Application.DTOs.Devices;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Devices;

/// <summary>
/// Registra (ou renova) o token de push de um dispositivo. Idempotente: o app pode chamar a cada
/// abertura. Se o token já estava registrado em outra conta, ele passa para quem chamou.
/// </summary>
public class RegisterDeviceUseCase
{
    private static readonly HashSet<string> Platforms = new() { "android", "ios", "web" };
    private const int MinTokenLength = 20;
    private const int MaxTokenLength = 4096;

    private readonly IDeviceTokenRepository _devices;

    public RegisterDeviceUseCase(IDeviceTokenRepository devices)
    {
        _devices = devices;
    }

    public async Task<RegisterDeviceResult> ExecuteAsync(string userId, RegisterDeviceRequest request)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        if (token.Length < MinTokenLength || token.Length > MaxTokenLength)
            throw new BusinessException("Invalid device token.");

        var platform = request.Platform?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!Platforms.Contains(platform))
            throw new BusinessException("Unsupported platform. Use android, ios or web.");

        var existing = await _devices.GetByTokenAsync(token);
        if (existing is null)
        {
            var created = DeviceToken.Create(userId, token, platform);
            await _devices.SaveAsync(created);
            return new RegisterDeviceResult(new DeviceResponse(created.Id, created.Platform), Created: true);
        }

        if (existing.UserId == userId)
            existing.Touch();
        else
            existing.ReassignTo(userId);

        await _devices.SaveAsync(existing);
        return new RegisterDeviceResult(new DeviceResponse(existing.Id, existing.Platform), Created: false);
    }
}
