namespace PriceWatch.Domain.Exceptions;

public class DeviceTokenNotFoundException : NotFoundException
{
    public DeviceTokenNotFoundException(string identifier)
        : base($"Device '{identifier}' not found.") { }
}
