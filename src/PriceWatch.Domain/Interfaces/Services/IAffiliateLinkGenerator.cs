using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Interfaces.Services;

public interface IAffiliateLinkGenerator
{
    ProductSource ProductSource { get; }
    bool CanHandle(string url);
    Task<string> GenerateAsync(string originalUrl);
}
