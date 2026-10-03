using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Workers;

/// <summary>
/// Roda uma vez no startup: migra TrackedProduct legado para Product/StoreOffer/
/// UserProductSubscription (ver <see cref="BackfillTrackedProductsUseCase"/>). Idempotente —
/// seguro rodar a cada deploy, produtos já migrados são pulados. Só roda com
/// <c>Features:BackfillEnabled=true</c> (padrão: desligado).
/// </summary>
public class BackfillWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeatureFlags _flags;
    private readonly ILogger<BackfillWorker> _logger;

    public BackfillWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FeatureFlags> flags,
        ILogger<BackfillWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _flags = flags.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_flags.BackfillEnabled)
        {
            _logger.LogInformation("Backfill disabled (Features:BackfillEnabled=false); skipping.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<BackfillTrackedProductsUseCase>();
            var migratedCount = await useCase.ExecuteAsync();

            if (migratedCount > 0)
                _logger.LogInformation(
                    "Backfill: migrated {Count} legacy tracked products to Product/StoreOffer.", migratedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TrackedProduct backfill.");
        }
    }
}
