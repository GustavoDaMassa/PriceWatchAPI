using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Offers;

public class RedirectOfferUseCase
{
    private readonly IStoreOfferRepository _offers;
    private readonly IAffiliateLinkResolver _affiliateResolver;

    public RedirectOfferUseCase(IStoreOfferRepository offers, IAffiliateLinkResolver affiliateResolver)
    {
        _offers = offers;
        _affiliateResolver = affiliateResolver;
    }

    public async Task<StoreOfferRedirectResult> ExecuteAsync(string offerId)
    {
        var offer = await _offers.GetByIdAsync(offerId)
            ?? throw new StoreOfferNotFoundException(offerId);

        if (string.IsNullOrEmpty(offer.AffiliateUrl))
        {
            var generator = _affiliateResolver.Resolve(offer.Source);
            var affiliateUrl = await generator.GenerateAsync(offer.Url);
            offer.SetAffiliateUrl(affiliateUrl);
            await _offers.UpdateAsync(offer);
        }

        return new StoreOfferRedirectResult(offer.Id, offer.Source, offer.AffiliateUrl!);
    }
}

public record StoreOfferRedirectResult(string OfferId, Domain.Enums.ProductSource Source, string AffiliateUrl);
