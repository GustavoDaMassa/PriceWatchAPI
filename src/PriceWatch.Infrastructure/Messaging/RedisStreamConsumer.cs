using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceWatch.Application.DTOs.Notification;
using PriceWatch.Application.UseCases.Notification;
using StackExchange.Redis;

namespace PriceWatch.Infrastructure.Messaging;

public class RedisStreamConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RedisStreamConsumer> _logger;
    private const string StreamKey = "price-alerts";
    private const string GroupName = "notification-group";
    private const string ConsumerName = "consumer-1";

    public RedisStreamConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<RedisStreamConsumer> logger)
    {
        _redis = redis;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.NewMessages);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // group already exists, ignore
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    StreamKey, GroupName, ConsumerName, ">", count: 10);

                foreach (var entry in entries)
                {
                    await ProcessEntryAsync(db, entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming Redis stream.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, StreamEntry entry)
    {
        try
        {
            var payloadField = entry.Values.FirstOrDefault(v => v.Name == "payload");
            if (payloadField.Value.IsNull) return;

            var alertEvent = JsonSerializer.Deserialize<AlertEvent>(payloadField.Value!);
            if (alertEvent is null) return;

            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<ProcessAlertUseCase>();

            await useCase.ExecuteAsync(
                alertEvent.UserId,
                alertEvent.ProductId,
                alertEvent.ProductName,
                alertEvent.Type,
                alertEvent.CurrentPrice);

            await db.StreamAcknowledgeAsync(StreamKey, GroupName, entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stream entry {EntryId}.", entry.Id);
        }
    }
}
