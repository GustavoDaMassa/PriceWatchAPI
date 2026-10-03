using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Application.UseCases.Offers;

public class CreateOfferUseCase : ICreateOfferUseCase
{
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IPriceFetcherResolver _fetcherResolver;
    private readonly IProductMatchingEventPublisher _matchingEventPublisher;
    private readonly SyncOfferActivityUseCase _syncActivity;
    private readonly IAffiliateLinkResolver _affiliateResolver;

    public CreateOfferUseCase(
        IProductRepository products,
        IStoreOfferRepository offers,
        IUserProductSubscriptionRepository subscriptions,
        IPriceFetcherResolver fetcherResolver,
        IProductMatchingEventPublisher matchingEventPublisher,
        SyncOfferActivityUseCase syncActivity,
        IAffiliateLinkResolver affiliateResolver)
    {
        _products = products;
        _offers = offers;
        _subscriptions = subscriptions;
        _fetcherResolver = fetcherResolver;
        _matchingEventPublisher = matchingEventPublisher;
        _syncActivity = syncActivity;
        _affiliateResolver = affiliateResolver;
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

            if (await TrySetAffiliateUrlAsync(offer))
                await _offers.UpdateAsync(offer);
        }
        else
        {
            var fetchResult = Enrich(await fetcher.FetchAsync(request.Url), request.ClientData);

            product = Product.Create(
                fetchResult.Name, fetchResult.Gtin, fetchResult.Brand, fetchResult.Model, fetchResult.ImageUrl);
            await _products.CreateAsync(product);

            offer = StoreOffer.Create(product.Id, fetcher.ProductSource, request.Url, fetchResult.Gtin);
            offer.RecordPrice(fetchResult.Price);
            await TrySetAffiliateUrlAsync(offer);
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

    // o link de afiliado é gerado ao começar o acompanhamento e guardado na oferta. Só é salvo se uma
    // rede está disponível E devolveu um link diferente da URL original (as redes devolvem a própria URL
    // quando falham — cachear isso esconderia a falha para sempre); sem link, o redirect tenta de novo.
    private async Task<bool> TrySetAffiliateUrlAsync(StoreOffer offer)
    {
        if (!string.IsNullOrEmpty(offer.AffiliateUrl))
            return false;

        var generator = _affiliateResolver.Resolve(offer.Source);
        if (!generator.IsAvailable)
            return false;

        var affiliateUrl = await generator.GenerateAsync(offer.Url);
        if (string.IsNullOrWhiteSpace(affiliateUrl) || string.Equals(affiliateUrl, offer.Url, StringComparison.Ordinal))
            return false;

        offer.SetAffiliateUrl(affiliateUrl);
        return true;
    }

    // dados do cliente só completam marca/modelo; preço, nome, GTIN e imagem vêm sempre da fonte oficial
    // (o catálogo é compartilhado: GTIN do cliente permitiria fundir produtos de propósito)
    private static ProductFetchResult Enrich(ProductFetchResult fetched, ClientProductData? client)
    {
        if (client is null)
            return fetched;

        return fetched with
        {
            Brand = fetched.Brand ?? Clean(client.Brand),
            Model = fetched.Model ?? Clean(client.Model)
        };
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > 200 ? null : trimmed;
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
