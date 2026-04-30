using System.Text.Json;
using PriceWatch.Application.DTOs.Notification;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using StackExchange.Redis;

namespace PriceWatch.Infrastructure.Messaging;

public class RedisStreamPublisher : IAlertPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStreamPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync(
        string productId,
        string userId,
        string productName,
        NotificationType type,
        decimal currentPrice)
    {
        var db = _redis.GetDatabase();

        var alertEvent = new AlertEvent(productId, userId, productName, type, currentPrice, string.Empty);
        var payload = JsonSerializer.Serialize(alertEvent);

        await db.StreamAddAsync("price-alerts", new NameValueEntry[]
        {
            new("payload", payload)
        });
    }
}
