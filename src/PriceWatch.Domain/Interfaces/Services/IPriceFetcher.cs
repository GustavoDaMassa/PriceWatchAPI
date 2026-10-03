using PriceWatch.Domain.Enums;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Domain.Interfaces.Services;

public interface IPriceFetcher
{
    ProductSource ProductSource { get; }

    /// <summary>False quando a integração está desligada (ex.: credenciais ausentes).</summary>
    bool IsAvailable => true;

    bool CanHandle(string url);
    Task<ProductFetchResult> FetchAsync(string url);
}
