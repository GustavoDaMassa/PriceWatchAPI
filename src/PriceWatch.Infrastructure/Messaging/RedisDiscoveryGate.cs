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

    private static readonly TimeSpan EmptyRunsTimeToLive = TimeSpan.FromHours(24);

    public Task<bool> TryAcquireAsync(string productId, TimeSpan window) =>
        _redis.GetDatabase().StringSetAsync($"discovery:{productId}", "1", window, When.NotExists);

    public async Task<int> CountEmptyRunsAsync(string productId)
    {
        var value = await _redis.GetDatabase().StringGetAsync($"discovery:{productId}:empty");
        return value.TryParse(out int count) ? count : 0;
    }

    public async Task RecordNewSearchAsync(string productId, bool foundNew)
    {
        var db = _redis.GetDatabase();
        var key = $"discovery:{productId}:empty";

        if (foundNew)
        {
            await db.KeyDeleteAsync(key);
            return;
        }

        await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, EmptyRunsTimeToLive);
    }
}
