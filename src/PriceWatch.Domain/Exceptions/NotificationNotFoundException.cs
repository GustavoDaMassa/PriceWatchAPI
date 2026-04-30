namespace PriceWatch.Domain.Exceptions;

public class NotificationNotFoundException : NotFoundException
{
    public NotificationNotFoundException(string identifier)
        : base($"Notification '{identifier}' not found.") { }
}
