using PriceWatch.Application.DTOs.Store;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.Services;

namespace PriceWatch.Application.UseCases.Stores;

public class GetSupportedStoresUseCase
{
    private readonly IEnumerable<IPriceFetcher> _fetchers;
    private readonly IEnumerable<IAffiliateLinkGenerator> _affiliateGenerators;

    public GetSupportedStoresUseCase(
        IEnumerable<IPriceFetcher> fetchers,
        IEnumerable<IAffiliateLinkGenerator> affiliateGenerators)
    {
        _fetchers = fetchers;
        _affiliateGenerators = affiliateGenerators;
    }

    public IReadOnlyList<StoreCapabilitiesResponse> Execute() =>
        StoreCatalog.All
            .Select(store => new StoreCapabilitiesResponse(
                store.Source,
                store.Name,
                store.HomeUrl,
                CanFetchByUrl: _fetchers.Any(f => f.ProductSource == store.Source && f.IsAvailable),
                CanSearch: false, // a descoberta por GTIN/palavra-chave chega no M3
                CanGenerateAffiliateLink: _affiliateGenerators.Any(g => g.ProductSource == store.Source && g.IsAvailable)))
            .ToList();
}
