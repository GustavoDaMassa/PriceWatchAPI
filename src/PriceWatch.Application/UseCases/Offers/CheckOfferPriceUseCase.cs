using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Offers;

/// <summary>
/// Verifica o preço de UMA oferta (uma consulta à loja, independente de quantos usuários a
/// acompanham), grava o snapshot e dispara alertas por assinatura. Substitui, no catálogo
/// unificado, o loop do <c>PriceCheckWorker</c> legado — mesma semântica de alerta:
/// preço-alvo (uma vez, rearmado quando o preço volta a subir) ou novo menor preço histórico.
/// Com várias lojas para o mesmo produto, o alvo é avaliado contra o MELHOR preço entre as lojas.
/// </summary>
public class CheckOfferPriceUseCase
{
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(15);

    private readonly IStoreOfferRepository _offers;
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductRepository _products;
    private readonly IPriceSnapshotRepository _snapshots;
    private readonly IPriceFetcherResolver _fetchers;
    private readonly IAlertPublisher _alerts;

    public CheckOfferPriceUseCase(
        IStoreOfferRepository offers,
        IUserProductSubscriptionRepository subscriptions,
        IProductRepository products,
        IPriceSnapshotRepository snapshots,
        IPriceFetcherResolver fetchers,
        IAlertPublisher alerts)
    {
        _offers = offers;
        _subscriptions = subscriptions;
        _products = products;
        _snapshots = snapshots;
        _fetchers = fetchers;
        _alerts = alerts;
    }

    public async Task ExecuteAsync(StoreOffer offer)
    {
        var activeSubscriptions = (await _subscriptions.GetByProductIdAsync(offer.ProductId))
            .Where(s => s.IsActive)
            .ToList();

        if (activeSubscriptions.Count == 0)
        {
            offer.Deactivate();
            await _offers.UpdateAsync(offer);
            return;
        }

        var siblings = (await _offers.GetByProductIdAsync(offer.ProductId))
            .Where(o => o.Id != offer.Id)
            .ToList();
        var previousLowest = LowestPositive(siblings.Select(o => o.LowestPrice).Append(offer.LowestPrice));

        var result = await FetchOrPostponeAsync(offer);

        var snapshot = offer.RecordPrice(result.Price);
        await _snapshots.CreateAsync(snapshot);
        await _offers.UpdateAsync(offer);

        var bestPrice = LowestPositive(
            siblings.Where(o => o.IsActive).Select(o => o.CurrentPrice).Append(offer.CurrentPrice));
        var productName = (await _products.GetByIdAsync(offer.ProductId))?.Name ?? result.Name;

        foreach (var subscription in activeSubscriptions)
            await EvaluateAlertsAsync(subscription, productName, bestPrice, result.Price, previousLowest);
    }

    private async Task<Domain.ValueObjects.ProductFetchResult> FetchOrPostponeAsync(StoreOffer offer)
    {
        try
        {
            var fetcher = _fetchers.Resolve(offer.Source);
            return await fetcher.FetchAsync(offer.Url);
        }
        catch
        {
            // evita bater na API externa a cada ciclo do worker quando a loja está falhando
            offer.PostponeCheck(FailureRetryDelay);
            await _offers.UpdateAsync(offer);
            throw;
        }
    }

    private async Task EvaluateAlertsAsync(
        UserProductSubscription subscription,
        string productName,
        decimal bestPrice,
        decimal fetchedPrice,
        decimal previousLowest)
    {
        var alertSentBefore = subscription.TargetAlertSent;
        subscription.ResetTargetAlertIfAbove(bestPrice);

        if (subscription.ShouldTriggerTargetAlert(bestPrice))
        {
            subscription.MarkTargetAlertSent();
            await _alerts.PublishAsync(
                subscription.Id, subscription.UserId, productName, NotificationType.TargetPriceReached, bestPrice);
        }
        else if (previousLowest > 0 && fetchedPrice < previousLowest)
        {
            await _alerts.PublishAsync(
                subscription.Id, subscription.UserId, productName, NotificationType.NewLowestPrice, fetchedPrice);
        }

        if (subscription.TargetAlertSent != alertSentBefore)
            await _subscriptions.UpdateAsync(subscription);
    }

    private static decimal LowestPositive(IEnumerable<decimal> prices)
    {
        var positive = prices.Where(p => p > 0).ToList();
        return positive.Count == 0 ? 0m : positive.Min();
    }
}
