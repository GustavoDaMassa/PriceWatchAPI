using PriceWatch.Application.Interfaces;
using StackExchange.Redis;

namespace PriceWatch.Infrastructure.Messaging;

/// <summary>
/// Janela de descoberta por produto no Redis: <c>SET key NX EX</c> é atômico, então duas
/// requisições simultâneas nunca disparam a busca duas vezes.
/// </summary>
public class RedisDiscoveryGate : IDiscoveryGate
{
    private readonly IConnectionMultiplexer _redis;

    public RedisDiscoveryGate(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task<bool> TryAcquireAsync(string productId, TimeSpan window) =>
        _redis.GetDatabase().StringSetAsync($"discovery:{productId}", "1", window, When.NotExists);
}
