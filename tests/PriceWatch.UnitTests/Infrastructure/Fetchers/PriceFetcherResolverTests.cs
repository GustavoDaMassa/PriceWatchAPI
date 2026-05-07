using FluentAssertions;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Fetchers;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Fetchers;

public class PriceFetcherResolverTests
{
    [Fact]
    public void Resolve_BySource_WithMercadoLivreSource_ShouldReturnMercadoLivreFetcher()
    {
        var fetchers = new[] { new MercadoLivreFetcher(null!, null!) };
        var resolver = new PriceFetcherResolver(fetchers);

        var result = resolver.Resolve(ProductSource.MercadoLivre);

        result.Should().BeOfType<MercadoLivreFetcher>();
    }

    [Fact]
    public void Resolve_BySource_WithUnknownSource_ShouldThrowBusinessException()
    {
        var resolver = new PriceFetcherResolver(Array.Empty<PriceWatch.Domain.Interfaces.Services.IPriceFetcher>());

        var act = () => resolver.Resolve(ProductSource.Kabum);

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Resolve_ByUrl_WithMercadoLivreUrl_ShouldReturnMercadoLivreFetcher()
    {
        var fetchers = new[] { new MercadoLivreFetcher(null!, null!) };
        var resolver = new PriceFetcherResolver(fetchers);

        var result = resolver.Resolve("https://produto.mercadolivre.com.br/MLB-123-titulo-_JM");

        result.Should().BeOfType<MercadoLivreFetcher>();
    }

    [Fact]
    public void Resolve_ByUrl_WithUnsupportedUrl_ShouldThrowBusinessException()
    {
        var resolver = new PriceFetcherResolver(Array.Empty<PriceWatch.Domain.Interfaces.Services.IPriceFetcher>());

        var act = () => resolver.Resolve("https://www.amazon.com.br/produto/123");

        act.Should().Throw<BusinessException>();
    }
}
