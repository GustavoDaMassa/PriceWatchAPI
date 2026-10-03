using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.Interfaces;

public interface IPriceFetcherResolver
{
    IPriceFetcher Resolve(ProductSource source);
    IPriceFetcher Resolve(string url);

    /// <summary>True se alguma loja disponível sabe tratar a URL (não lança).</summary>
    bool CanHandle(string url);
}
