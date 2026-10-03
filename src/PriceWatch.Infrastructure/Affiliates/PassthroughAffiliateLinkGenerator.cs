using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Infrastructure.Affiliates;

/// <summary>
/// Retorna a URL original sem transformação. Usado enquanto nenhuma rede de afiliados real
/// está integrada (Fase 6 do pivô) — qualquer loja cai aqui por padrão, então o endpoint de
/// redirect funciona (com tracking de clique) mesmo antes de haver comissão de fato.
/// </summary>
public class PassthroughAffiliateLinkGenerator : IAffiliateLinkGenerator
{
    public ProductSource ProductSource => ProductSource.Manual;

    // não é uma rede de afiliados: só devolve a URL original
    public bool IsAvailable => false;

    public bool CanHandle(string url) => true;

    public Task<string> GenerateAsync(string originalUrl) => Task.FromResult(originalUrl);
}
