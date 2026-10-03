using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.UseCases.Offers;
using StackExchange.Redis;

namespace PriceWatch.Infrastructure.Messaging;

public class ProductMatchingConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductMatchingConsumer> _logger;
    private const string StreamKey = "store-offer-events";
    private const string GroupName = "matching-group";
    private const string ConsumerName = "consumer-1";

    public ProductMatchingConsumer(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<ProductMatchingConsumer> logger)
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
                _logger.LogError(ex, "Error consuming store-offer-events stream.");
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

            var offerEvent = JsonSerializer.Deserialize<StoreOfferCreatedEvent>(payloadField.Value!);
            if (offerEvent is null) return;

            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<MatchStoreOfferUseCase>();

            await useCase.ExecuteAsync(offerEvent.OfferId, offerEvent.ProductId);

            await db.StreamAcknowledgeAsync(StreamKey, GroupName, entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing store-offer-events entry {EntryId}.", entry.Id);
        }
    }
}
