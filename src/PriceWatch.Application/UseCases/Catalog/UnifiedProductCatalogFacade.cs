using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Catalog;

/// <summary>
/// Os endpoints legados sobre o catálogo unificado. Um "item" legado é a assinatura do usuário
/// (<see cref="UserProductSubscription"/>): o Id exposto é o id da assinatura (o backfill preserva o
/// id do TrackedProduct), e preço/URL/loja vêm da melhor oferta ativa do produto (a mais barata).
/// Os DTOs de resposta são exatamente os do modelo legado.
/// </summary>
public class UnifiedProductCatalogFacade : IProductCatalogFacade
{
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IPriceSnapshotRepository _snapshots;
    private readonly IProductListRepository _lists;
    private readonly IPriceFetcherResolver _fetchers;
    private readonly CreateOfferUseCase _createOffer;
    private readonly SyncOfferActivityUseCase _syncActivity;
    private readonly RedirectOfferUseCase _redirectOffer;

    public UnifiedProductCatalogFacade(
        IUserProductSubscriptionRepository subscriptions,
        IProductRepository products,
        IStoreOfferRepository offers,
        IPriceSnapshotRepository snapshots,
        IProductListRepository lists,
        IPriceFetcherResolver fetchers,
        CreateOfferUseCase createOffer,
        SyncOfferActivityUseCase syncActivity,
        RedirectOfferUseCase redirectOffer)
    {
        _subscriptions = subscriptions;
        _products = products;
        _offers = offers;
        _snapshots = snapshots;
        _lists = lists;
        _fetchers = fetchers;
        _createOffer = createOffer;
        _syncActivity = syncActivity;
        _redirectOffer = redirectOffer;
    }

    public async Task<IEnumerable<TrackedProductResponse>> GetUserProductsAsync(string userId, string? listId)
    {
        var subscriptions = (await _subscriptions.GetByUserIdAsync(userId, listId)).ToList();
        if (subscriptions.Count == 0)
            return Array.Empty<TrackedProductResponse>();

        var productIds = subscriptions.Select(s => s.ProductId).Distinct().ToList();
        var products = (await _products.GetByIdsAsync(productIds)).ToDictionary(p => p.Id);
        var offersByProduct = (await _offers.GetByProductIdsAsync(productIds))
            .GroupBy(o => o.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // os itens mais recentes primeiro
        var items = new List<TrackedProductResponse>();
        foreach (var subscription in subscriptions.OrderByDescending(s => s.CreatedAt))
        {
            if (products.TryGetValue(subscription.ProductId, out var product) &&
                offersByProduct.TryGetValue(subscription.ProductId, out var offers))
            {
                var item = ToResponse(subscription, product, offers);
                if (item is not null)
                    items.Add(item);
            }
        }

        return items;
    }

    public async Task<TrackedProductResponse> AddProductAsync(string userId, AddProductRequest request)
    {
        var fetcher = _fetchers.Resolve(request.Url);
        var existingOffer = await _offers.GetBySourceAndUrlAsync(fetcher.ProductSource, request.Url);

        if (existingOffer is not null)
        {
            var existing = await _subscriptions.GetByUserIdAndProductIdAsync(userId, existingOffer.ProductId);
            if (existing is not null)
            {
                // mesmo comportamento do modelo legado: item repetido só é reativado, o preço-alvo
                // não é sobrescrito (a extensão sempre envia targetPrice = 0)
                if (!existing.IsActive)
                {
                    existing.Activate();
                    await _subscriptions.UpdateAsync(existing);
                    await _syncActivity.ExecuteAsync(existing.ProductId);
                }

                return await ToResponseAsync(existing);
            }
        }

        var created = await _createOffer.ExecuteAsync(
            userId, new CreateOfferRequest(request.Url, request.TargetPrice, request.ListId));

        return new TrackedProductResponse(
            created.SubscriptionId, created.ListId, created.Name, created.Url, created.ImageUrl, created.Source,
            created.TargetPrice, created.CurrentPrice, created.LowestPrice, created.IsActive, created.NextCheckAt,
            created.ProductId);
    }

    public async Task UpdateProductAsync(string productId, string userId, UpdateProductRequest request)
    {
        var subscription = await GetOwnedAsync(productId, userId);

        subscription.SetTargetPrice(request.TargetPrice);
        if (request.IsActive)
            subscription.Activate();
        else
            subscription.Deactivate();

        await _subscriptions.UpdateAsync(subscription);
        await _syncActivity.ExecuteAsync(subscription.ProductId);
    }

    public async Task AssignToListAsync(string productId, string userId, AssignToListRequest request)
    {
        var subscription = await GetOwnedAsync(productId, userId);

        if (request.ListId is not null)
        {
            var list = await _lists.GetByIdAsync(request.ListId);
            if (list is null || list.UserId != userId)
                throw new ProductListNotFoundException(request.ListId);

            subscription.AssignToList(request.ListId);
        }
        else
            subscription.RemoveFromList();

        await _subscriptions.UpdateAsync(subscription);
    }

    public async Task RemoveProductAsync(string productId, string userId)
    {
        var subscription = await GetOwnedAsync(productId, userId);

        await _subscriptions.DeleteAsync(subscription.Id);
        // o catálogo é compartilhado: o produto e as ofertas ficam, mas sem assinante a oferta pausa
        await _syncActivity.ExecuteAsync(subscription.ProductId);
    }

    public async Task<IEnumerable<PriceSnapshotResponse>> GetPriceHistoryAsync(string productId, string userId)
    {
        var subscription = await GetOwnedAsync(productId, userId);

        var primary = PrimaryOffer(await _offers.GetByProductIdAsync(subscription.ProductId));
        if (primary is null)
            return Array.Empty<PriceSnapshotResponse>();

        var snapshots = await _snapshots.GetByProductIdAsync(primary.Id);
        return snapshots.Select(s => new PriceSnapshotResponse(s.Id, s.Price, s.CapturedAt));
    }

    public async Task<IEnumerable<AnalysisItemDto>> GetListAnalysisAsync(string listId, string userId)
    {
        var list = await _lists.GetByIdAsync(listId);
        if (list is null || list.UserId != userId)
            throw new ProductListNotFoundException(listId);

        var items = await GetUserProductsAsync(userId, listId);

        return items
            .Where(i => i.IsActive)
            .Select(i => new AnalysisItemDto(
                i.Id,
                i.Name,
                i.CurrentPrice,
                i.TargetPrice,
                i.TargetPrice == 0 ? 0 :
                    Math.Round((i.CurrentPrice - i.TargetPrice) / i.TargetPrice * 100, 2)))
            .OrderBy(a => a.DistancePercent);
    }

    public async Task<string> OpenProductAsync(string productId, string userId)
    {
        var subscription = await GetOwnedAsync(productId, userId);

        var primary = PrimaryOffer(await _offers.GetByProductIdAsync(subscription.ProductId))
            ?? throw new StoreOfferNotFoundException(subscription.ProductId);

        return (await _redirectOffer.ExecuteAsync(primary.Id, userId)).AffiliateUrl;
    }

    private async Task<UserProductSubscription> GetOwnedAsync(string subscriptionId, string userId)
    {
        var subscription = await _subscriptions.GetByIdAsync(subscriptionId);
        if (subscription is null || subscription.UserId != userId)
            throw new TrackedProductNotFoundException(subscriptionId);

        return subscription;
    }

    private async Task<TrackedProductResponse> ToResponseAsync(UserProductSubscription subscription)
    {
        var product = await _products.GetByIdAsync(subscription.ProductId)
            ?? throw new ProductNotFoundException(subscription.ProductId);
        var offers = (await _offers.GetByProductIdAsync(subscription.ProductId)).ToList();

        return ToResponse(subscription, product, offers)
            ?? throw new StoreOfferNotFoundException(subscription.ProductId);
    }

    private static TrackedProductResponse? ToResponse(
        UserProductSubscription subscription, Product product, IReadOnlyCollection<StoreOffer> offers)
    {
        var primary = PrimaryOffer(offers);
        if (primary is null)
            return null;

        var lowest = offers.Where(o => o.LowestPrice > 0).Select(o => o.LowestPrice).DefaultIfEmpty(primary.LowestPrice).Min();

        return new TrackedProductResponse(
            subscription.Id,
            subscription.ListId,
            product.Name,
            primary.Url,
            product.ImageUrl,
            primary.Source,
            subscription.TargetPrice,
            primary.CurrentPrice,
            lowest,
            subscription.IsActive,
            primary.NextCheckAt,
            subscription.ProductId);
    }

    // a oferta que representa o item: ativa, com preço conhecido, a mais barata
    private static StoreOffer? PrimaryOffer(IEnumerable<StoreOffer> offers) =>
        offers
            .OrderByDescending(o => o.IsActive)
            .ThenBy(o => o.CurrentPrice > 0 ? 0 : 1)
            .ThenBy(o => o.CurrentPrice)
            .ThenBy(o => o.CreatedAt)
            .FirstOrDefault();
}
