using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace PriceWatch.API.Health;

/// <summary>Readiness: a API só está pronta se consegue falar com o MongoDB e o Redis.</summary>
public class DependenciesHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;
    private readonly IConnectionMultiplexer _redis;

    public DependenciesHealthCheck(IMongoDatabase database, IConnectionMultiplexer redis)
    {
        _database = database;
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB indisponível", ex);
        }

        try
        {
            await _redis.GetDatabase().PingAsync();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis indisponível", ex);
        }

        return HealthCheckResult.Healthy();
    }
}
