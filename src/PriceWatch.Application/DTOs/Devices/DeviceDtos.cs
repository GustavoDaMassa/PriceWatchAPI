namespace PriceWatch.Application.DTOs.Devices;

public record RegisterDeviceRequest(string Token, string Platform);

public record DeviceResponse(string Id, string Platform);

public record RegisterDeviceResult(DeviceResponse Device, bool Created);
