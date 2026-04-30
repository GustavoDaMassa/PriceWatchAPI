using FluentAssertions;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Fetchers;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Fetchers;

public class PriceFetcherResolverTests
{
    [Fact]
    public void Resolve_WithMercadoLivreSource_ShouldReturnMercadoLivreFetcher()
    {
        var fetchers = new[] { new MercadoLivreFetcher(null!) };
        var resolver = new PriceFetcherResolver(fetchers);

        var result = resolver.Resolve(ProductSource.MercadoLivre);

        result.Should().BeOfType<MercadoLivreFetcher>();
    }

    [Fact]
    public void Resolve_WithUnknownSource_ShouldThrowBusinessException()
    {
        var resolver = new PriceFetcherResolver(Array.Empty<PriceWatch.Domain.Interfaces.Services.IPriceFetcher>());

        var act = () => resolver.Resolve(ProductSource.Kabum);

        act.Should().Throw<BusinessException>();
    }
}
