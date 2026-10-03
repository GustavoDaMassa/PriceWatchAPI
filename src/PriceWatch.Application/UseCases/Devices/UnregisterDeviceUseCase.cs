using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Devices;

public class UnregisterDeviceUseCase
{
    private readonly IDeviceTokenRepository _devices;

    public UnregisterDeviceUseCase(IDeviceTokenRepository devices)
    {
        _devices = devices;
    }

    public async Task ExecuteAsync(string userId, string deviceId)
    {
        var device = await _devices.GetByIdAsync(deviceId);
        if (device is null || device.UserId != userId)
            throw new DeviceTokenNotFoundException(deviceId);

        await _devices.DeleteAsync(deviceId);
    }
}
