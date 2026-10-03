namespace PriceWatch.Domain.Interfaces.Services;

public interface IProductMatchingEventPublisher
{
    Task PublishAsync(string offerId, string productId);
}
