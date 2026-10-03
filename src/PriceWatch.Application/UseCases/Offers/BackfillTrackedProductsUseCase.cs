using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.Application.UseCases.Offers;

/// <summary>
/// Migração aditiva do modelo legado (1 <see cref="DomainTrackedProduct"/> = 1 URL = 1 fonte) para o
/// modelo novo (<see cref="Product"/> compartilhado + <see cref="StoreOffer"/> por loja +
/// <see cref="UserProductSubscription"/> por usuário). Não remove nem altera o TrackedProduct
/// legado — só marca cada um como migrado. Ver docs/arquitetura.md, seção "Pivô Multi-Loja".
///
/// Preserva os ids legados para o cutover não quebrar clientes: a assinatura recebe o id do
/// TrackedProduct (os links da SPA e as notificações continuam válidos) e, quando é a primeira a
/// criar a oferta, a oferta também (o histórico em price_snapshots é chaveado por esse id). Itens
/// processados do mais antigo para o mais novo: se vários usuários acompanham a mesma URL, a oferta
/// compartilhada herda o histórico mais longo.
/// </summary>
public class BackfillTrackedProductsUseCase
{
    private readonly ITrackedProductRepository _trackedProducts;
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductMatchingEventPublisher _matchingEventPublisher;

    public BackfillTrackedProductsUseCase(
        ITrackedProductRepository trackedProducts,
        IProductRepository products,
        IStoreOfferRepository offers,
        IUserProductSubscriptionRepository subscriptions,
        IProductMatchingEventPublisher matchingEventPublisher)
    {
        _trackedProducts = trackedProducts;
        _products = products;
        _offers = offers;
        _subscriptions = subscriptions;
        _matchingEventPublisher = matchingEventPublisher;
    }

    public async Task<int> ExecuteAsync()
    {
        var legacyProducts = (await _trackedProducts.GetAllAsync()).OrderBy(p => p.CreatedAt).ToList();
        var migratedCount = 0;

        foreach (var legacy in legacyProducts)
        {
            if (legacy.IsMigratedToProduct())
                continue;

            var (product, offer) = await GetOrCreateProductAndOfferAsync(legacy);
            await EnsureSubscriptionAsync(legacy, product.Id);

            legacy.MarkMigratedToProduct(product.Id);
            await _trackedProducts.UpdateAsync(legacy);

            migratedCount++;
        }

        return migratedCount;
    }

    private async Task<(Product Product, StoreOffer Offer)> GetOrCreateProductAndOfferAsync(DomainTrackedProduct legacy)
    {
        var existingOffer = await _offers.GetBySourceAndUrlAsync(legacy.Source, legacy.Url);

        if (existingOffer is not null)
        {
            var existingProduct = await _products.GetByIdAsync(existingOffer.ProductId)
                ?? throw new ProductNotFoundException(existingOffer.ProductId);

            // a oferta compartilhada só fica pausada se todos os itens legados estiverem pausados
            if (legacy.IsActive && !existingOffer.IsActive)
            {
                existingOffer.Activate();
                await _offers.UpdateAsync(existingOffer);
            }

            return (existingProduct, existingOffer);
        }

        var product = Product.Create(legacy.Name, imageUrl: legacy.ImageUrl);
        await _products.CreateAsync(product);

        var offer = StoreOffer.Create(product.Id, legacy.Source, legacy.Url, id: legacy.Id);
        // espelha o estado atual do item legado; quem passa a verificar o preço é o
        // StoreOfferPriceCheckWorker (Features:UnifiedCatalogEnabled)
        offer.RecordPrice(legacy.CurrentPrice);
        offer.RestoreLowestPrice(legacy.LowestPrice);
        if (!legacy.IsActive)
            offer.Deactivate();
        await _offers.CreateAsync(offer);

        // dispara o matching pra este produto migrado também poder se unificar com
        // ofertas de outras lojas já existentes (ou futuras) do mesmo item
        await _matchingEventPublisher.PublishAsync(offer.Id, product.Id);

        return (product, offer);
    }

    private async Task EnsureSubscriptionAsync(DomainTrackedProduct legacy, string productId)
    {
        var subscription = await _subscriptions.GetByUserIdAndProductIdAsync(legacy.UserId, productId);
        if (subscription is not null)
            return;

        subscription = UserProductSubscription.Create(
            legacy.UserId, productId, legacy.TargetPrice, legacy.ListId, id: legacy.Id);
        if (!legacy.IsActive)
            subscription.Deactivate();

        // quem já recebeu o alerta de preço-alvo não é alertado de novo logo após o cutover
        if (legacy.TargetAlertSent)
            subscription.MarkTargetAlertSent();

        await _subscriptions.CreateAsync(subscription);
    }
}
