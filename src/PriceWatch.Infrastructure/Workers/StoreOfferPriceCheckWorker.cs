using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Workers;

/// <summary>
/// Monitoramento do catálogo unificado: verifica as <c>StoreOffer</c> vencidas (uma consulta por
/// oferta, não por usuário) e dispara alertas por assinatura — ver <see cref="CheckOfferPriceUseCase"/>.
/// Só roda com <c>Features:UnifiedCatalogEnabled=true</c>; nesse modo o <see cref="PriceCheckWorker"/>
/// legado fica ocioso, então nunca há dois workers monitorando (e alertando) o mesmo item.
/// </summary>
public class StoreOfferPriceCheckWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FeatureFlags _flags;
    private readonly ILogger<StoreOfferPriceCheckWorker> _logger;

    public StoreOfferPriceCheckWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<FeatureFlags> flags,
        ILogger<StoreOfferPriceCheckWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _flags = flags.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_flags.UnifiedCatalogEnabled)
        {
            _logger.LogInformation("Offer monitoring disabled (Features:UnifiedCatalogEnabled=false); worker idle.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDueOffersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during offer price check cycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CheckDueOffersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var offers = scope.ServiceProvider.GetRequiredService<IStoreOfferRepository>();
        var checkOffer = scope.ServiceProvider.GetRequiredService<CheckOfferPriceUseCase>();

        foreach (var offer in await offers.GetDueForCheckAsync())
        {
            try
            {
                await checkOffer.ExecuteAsync(offer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking price for offer {OfferId} ({Source}).", offer.Id, offer.Source);
            }
        }
    }
}
