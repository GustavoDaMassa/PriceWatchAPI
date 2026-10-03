using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Infrastructure.Affiliates;

public class AffiliateLinkResolver : IAffiliateLinkResolver
{
    private readonly IEnumerable<IAffiliateLinkGenerator> _generators;
    private readonly IAffiliateLinkGenerator _fallback = new PassthroughAffiliateLinkGenerator();

    public AffiliateLinkResolver(IEnumerable<IAffiliateLinkGenerator> generators)
    {
        _generators = generators;
    }

    // diferente do IPriceFetcherResolver: nunca lança se não achar rede específica —
    // degrada pro passthrough, porque redirecionar sem link de afiliado é melhor do que
    // quebrar o redirect por completo
    public IAffiliateLinkGenerator Resolve(ProductSource source) =>
        _generators.FirstOrDefault(g => g.ProductSource == source) ?? _fallback;

    public IAffiliateLinkGenerator Resolve(string url) =>
        _generators.FirstOrDefault(g => g.CanHandle(url)) ?? _fallback;
}
