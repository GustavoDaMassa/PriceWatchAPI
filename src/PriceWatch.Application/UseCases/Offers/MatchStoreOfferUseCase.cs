using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Services;

namespace PriceWatch.Application.UseCases.Offers;

/// <summary>
/// Consumidor do evento <see cref="DTOs.Offer.StoreOfferCreatedEvent"/> (ver
/// <c>ProductMatchingConsumer</c>). Estratégia EAN-first: GTIN bate = match automático;
/// sem GTIN, tenta fuzzy por título/marca, mas o resultado nunca é publicado automaticamente
/// (vai para <see cref="MatchStatus.PendingReview"/>) — ver RF14/RF15 em docs/requisitos.md.
/// </summary>
public class MatchStoreOfferUseCase
{
    private const double FuzzyThresholdWithBrandMatch = 0.82;
    private const double FuzzyThresholdTitleOnly = 0.92;

    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IUserProductSubscriptionRepository _subscriptions;

    public MatchStoreOfferUseCase(
        IProductRepository products,
        IStoreOfferRepository offers,
        IUserProductSubscriptionRepository subscriptions)
    {
        _products = products;
        _offers = offers;
        _subscriptions = subscriptions;
    }

    public async Task ExecuteAsync(string offerId, string productId)
    {
        var offer = await _offers.GetByIdAsync(offerId);
        if (offer is null)
            return;

        // idempotência: se a oferta já foi reatribuída (evento reprocessado após o merge), não repetir
        if (offer.ProductId != productId)
            return;

        var product = await _products.GetByIdAsync(productId);
        if (product is null)
            return;

        if (product.Gtin is not null)
        {
            var target = await _products.GetByGtinAsync(product.Gtin, excludeId: product.Id);
            if (target is not null)
                await MergeAsync(offer, product, target);
            return;
        }

        // sem GTIN: nunca funde automaticamente (RF15/RI07) — só sugere. O produto continua
        // com sua própria oferta, funcionando normalmente pro usuário que a criou, até que
        // alguém confirme o match (confirmação em si é trabalho futuro, fora da Fase 5)
        var candidates = await _products.GetWithoutGtinAsync(product.Id);
        var suggestion = FindBestFuzzyMatch(product, candidates);
        if (suggestion is null)
            return;

        product.SuggestMatch(suggestion.Id);
        await _products.UpdateAsync(product);
    }

    private static Product? FindBestFuzzyMatch(Product product, IEnumerable<Product> candidates)
    {
        Product? best = null;
        var bestScore = 0d;

        foreach (var candidate in candidates)
        {
            var score = TitleSimilarity.Ratio(product.Name, candidate.Name);

            var sameBrand = product.Brand is not null && candidate.Brand is not null &&
                string.Equals(product.Brand, candidate.Brand, StringComparison.OrdinalIgnoreCase);
            var threshold = sameBrand ? FuzzyThresholdWithBrandMatch : FuzzyThresholdTitleOnly;

            if (score >= threshold && score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private async Task MergeAsync(StoreOffer offer, Product source, Product target)
    {
        offer.ReassignToProduct(target.Id);
        await _offers.UpdateAsync(offer);

        // o produto "source" (recém-criado, 1 oferta só) fica órfão após o merge — não é
        // deletado nesta fase (sem cascata implementada), só deixa de ser referenciado
        var subscriptions = await _subscriptions.GetByProductIdAsync(source.Id);
        foreach (var subscription in subscriptions)
        {
            var existingOnTarget = await _subscriptions.GetByUserIdAndProductIdAsync(subscription.UserId, target.Id);
            if (existingOnTarget is not null)
            {
                // usuário já rastreava o produto-alvo por outra loja — evita duplicar
                // subscrição (violaria o índice único userId+productId)
                await _subscriptions.DeleteAsync(subscription.Id);
                continue;
            }

            subscription.ReassignToProduct(target.Id);
            await _subscriptions.UpdateAsync(subscription);
        }

        if (target.MatchStatus == MatchStatus.Confirmed)
            return; // não rebaixa um match já confirmado manualmente

        target.MarkAutoMatched();
        await _products.UpdateAsync(target);
    }
}
