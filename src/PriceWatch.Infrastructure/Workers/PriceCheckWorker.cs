using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Infrastructure.Workers;

public class PriceCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceCheckWorker> _logger;

    public PriceCheckWorker(IServiceScopeFactory scopeFactory, ILogger<PriceCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPricesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during price check cycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CheckPricesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var productRepo = scope.ServiceProvider.GetRequiredService<ITrackedProductRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IPriceSnapshotRepository>();
        var fetchers = scope.ServiceProvider.GetRequiredService<IEnumerable<IPriceFetcher>>();
        var alertPublisher = scope.ServiceProvider.GetRequiredService<IAlertPublisher>();

        var dueProducts = await productRepo.GetDueForCheckAsync();

        foreach (var product in dueProducts)
        {
            try
            {
                var fetcher = fetchers.FirstOrDefault(f => f.ProductSource == product.Source);
                if (fetcher is null)
                {
                    _logger.LogWarning("No fetcher for source {Source}, skipping product {Id}.", product.Source, product.Id);
                    continue;
                }

                var previousLowest = product.LowestPrice;
                var fetchResult = await fetcher.FetchAsync(product.Url);
                var snapshot = product.RecordPrice(fetchResult.Price);

                await snapshotRepo.CreateAsync(snapshot);
                await productRepo.UpdateAsync(product);

                if (product.ShouldTriggerTargetAlert())
                    await alertPublisher.PublishAsync(product.Id, product.UserId, product.Name, NotificationType.TargetPriceReached, fetchResult.Price);
                else if (product.ShouldTriggerLowestAlert(previousLowest))
                    await alertPublisher.PublishAsync(product.Id, product.UserId, product.Name, NotificationType.NewLowestPrice, fetchResult.Price);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking price for product {Id}.", product.Id);
            }
        }
    }
}
