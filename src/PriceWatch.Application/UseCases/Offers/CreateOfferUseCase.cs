using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Offers;

public class CreateOfferUseCase
{
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IPriceFetcherResolver _fetcherResolver;
    private readonly IProductMatchingEventPublisher _matchingEventPublisher;
    private readonly SyncOfferActivityUseCase _syncActivity;

    public CreateOfferUseCase(
        IProductRepository products,
        IStoreOfferRepository offers,
        IUserProductSubscriptionRepository subscriptions,
        IPriceFetcherResolver fetcherResolver,
        IProductMatchingEventPublisher matchingEventPublisher,
        SyncOfferActivityUseCase syncActivity)
    {
        _products = products;
        _offers = offers;
        _subscriptions = subscriptions;
        _fetcherResolver = fetcherResolver;
        _matchingEventPublisher = matchingEventPublisher;
        _syncActivity = syncActivity;
    }

    public async Task<OfferResponse> ExecuteAsync(string userId, CreateOfferRequest request)
    {
        var fetcher = _fetcherResolver.Resolve(request.Url);
        var existingOffer = await _offers.GetBySourceAndUrlAsync(fetcher.ProductSource, request.Url);

        StoreOffer offer;
        Product product;

        if (existingOffer is not null)
        {
            offer = existingOffer;
            product = await _products.GetByIdAsync(offer.ProductId)
                ?? throw new ProductNotFoundException(offer.ProductId);
        }
        else
        {
            var fetchResult = await fetcher.FetchAsync(request.Url);

            product = Product.Create(
                fetchResult.Name, fetchResult.Gtin, fetchResult.Brand, fetchResult.Model, fetchResult.ImageUrl);
            await _products.CreateAsync(product);

            offer = StoreOffer.Create(product.Id, fetcher.ProductSource, request.Url, fetchResult.Gtin);
            offer.RecordPrice(fetchResult.Price);
            await _offers.CreateAsync(offer);

            // dispara o motor de matching assíncrono (Fase 5) — não bloqueia esta resposta
            await _matchingEventPublisher.PublishAsync(offer.Id, product.Id);
        }

        var subscription = await _subscriptions.GetByUserIdAndProductIdAsync(userId, product.Id);

        if (subscription is null)
        {
            subscription = UserProductSubscription.Create(userId, product.Id, request.TargetPrice, request.ListId);
            await _subscriptions.CreateAsync(subscription);
        }
        else
        {
            if (!subscription.IsActive)
                subscription.Activate();

            subscription.SetTargetPrice(request.TargetPrice);

            if (request.ListId is not null)
                subscription.AssignToList(request.ListId);

            await _subscriptions.UpdateAsync(subscription);
        }

        // uma oferta só é monitorada se tiver assinante ativo (reativa ofertas pausadas)
        await _syncActivity.ExecuteAsync(product.Id);

        return ToResponse(subscription, product, offer);
    }

    private static OfferResponse ToResponse(UserProductSubscription subscription, Product product, StoreOffer offer) =>
        new(
            subscription.Id,
            product.Id,
            offer.Id,
            product.Name,
            offer.Url,
            product.ImageUrl,
            offer.Source,
            subscription.TargetPrice,
            offer.CurrentPrice,
            offer.LowestPrice,
            subscription.IsActive,
            offer.NextCheckAt,
            subscription.ListId);
}
