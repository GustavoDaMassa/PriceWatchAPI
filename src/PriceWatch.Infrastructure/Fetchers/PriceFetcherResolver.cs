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
        var fetcher = _fetchers.FirstOrDefault(f => f.ProductSource == source);
        if (fetcher is null)
            throw new BusinessException($"No price fetcher registered for source '{source}'.");
        return fetcher;
    }

    public IPriceFetcher Resolve(string url)
    {
        var fetcher = _fetchers.FirstOrDefault(f => f.CanHandle(url));
        if (fetcher is null)
            throw new BusinessException("No price fetcher found for the provided URL.");
        return fetcher;
    }
}
