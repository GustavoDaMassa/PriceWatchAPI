using PriceWatch.Domain.Enums;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Domain.Interfaces.Services;

public interface IPriceFetcher
{
    ProductSource ProductSource { get; }
    bool CanHandle(string url);
    Task<ProductFetchResult> FetchAsync(string url);
}
