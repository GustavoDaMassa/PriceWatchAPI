using System.Text.Json;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Domain.Interfaces.Services;
using StackExchange.Redis;

namespace PriceWatch.Infrastructure.Messaging;

public class RedisStoreOfferEventPublisher : IProductMatchingEventPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStoreOfferEventPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync(string offerId, string productId)
    {
        var db = _redis.GetDatabase();

        var storeOfferEvent = new StoreOfferCreatedEvent(offerId, productId);
        var payload = JsonSerializer.Serialize(storeOfferEvent);

        await db.StreamAddAsync("store-offer-events", new NameValueEntry[]
        {
            new("payload", payload)
        });
    }
}
