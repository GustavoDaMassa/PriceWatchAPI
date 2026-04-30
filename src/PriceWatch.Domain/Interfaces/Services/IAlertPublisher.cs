using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Interfaces.Services;

public interface IAlertPublisher
{
    Task PublishAsync(
        string productId,
        string userId,
        string productName,
        NotificationType type,
        decimal currentPrice);
}
