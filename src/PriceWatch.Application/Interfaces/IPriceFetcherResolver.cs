using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.Interfaces;

public interface IPriceFetcherResolver
{
    IPriceFetcher Resolve(ProductSource source);
    IPriceFetcher Resolve(string url);
}
