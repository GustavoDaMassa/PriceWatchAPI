using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Infrastructure.Fetchers;

public class PriceFetcherResolver : IPriceFetcherResolver
{
    private readonly IEnumerable<IPriceFetcher> _fetchers;

    public PriceFetcherResolver(IEnumerable<IPriceFetcher> fetchers)
    {
        _fetchers = fetchers;
    }

    public IPriceFetcher Resolve(ProductSource source)
    {
        var key = source.ToString().ToLower();
        var fetcher = _fetchers.FirstOrDefault(f => f.Source == key);
        if (fetcher is null)
            throw new BusinessException($"No price fetcher registered for source '{source}'.");
        return fetcher;
    }
}
