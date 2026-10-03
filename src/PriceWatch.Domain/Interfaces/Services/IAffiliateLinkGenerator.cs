using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Interfaces.Services;

public interface IAffiliateLinkGenerator
{
    ProductSource ProductSource { get; }

    /// <summary>
    /// True só se gera links de afiliado de verdade agora. O passthrough e integrações sem
    /// credenciais retornam false.
    /// </summary>
    bool IsAvailable => true;

    bool CanHandle(string url);
    Task<string> GenerateAsync(string originalUrl);
}
