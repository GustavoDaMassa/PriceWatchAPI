using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Stores;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Stores;

public class GetSupportedStoresUseCaseTests
{
    private static Mock<IPriceFetcher> Fetcher(ProductSource source, bool available)
    {
        var fetcher = new Mock<IPriceFetcher>();
        fetcher.Setup(f => f.ProductSource).Returns(source);
        fetcher.Setup(f => f.IsAvailable).Returns(available);
        return fetcher;
    }

    private static Mock<IAffiliateLinkGenerator> Generator(ProductSource source, bool available)
    {
        var generator = new Mock<IAffiliateLinkGenerator>();
        generator.Setup(g => g.ProductSource).Returns(source);
        generator.Setup(g => g.IsAvailable).Returns(available);
        return generator;
    }

    private static Mock<IOfferSearcher> Searcher(ProductSource source, bool available)
    {
        var searcher = new Mock<IOfferSearcher>();
        searcher.Setup(s => s.ProductSource).Returns(source);
        searcher.Setup(s => s.IsAvailable).Returns(available);
        return searcher;
    }

    private static GetSupportedStoresUseCase Build(
        IEnumerable<Mock<IPriceFetcher>> fetchers,
        IEnumerable<Mock<IAffiliateLinkGenerator>> generators,
        IEnumerable<Mock<IOfferSearcher>>? searchers = null) =>
        new(fetchers.Select(f => f.Object), generators.Select(g => g.Object),
            (searchers ?? []).Select(x => x.Object));

    [Fact]
    public void Execute_ListsEveryKnownStoreWithNameAndHomeUrl_ExceptManual()
    {
        var useCase = Build([], []);

        var stores = useCase.Execute();

        stores.Select(s => s.Source).Should().BeEquivalentTo(new[] { ProductSource.MercadoLivre, ProductSource.Kabum });
        stores.Single(s => s.Source == ProductSource.MercadoLivre).Name.Should().Be("Mercado Livre");
        stores.Single(s => s.Source == ProductSource.MercadoLivre).HomeUrl.Should().Be("https://www.mercadolivre.com.br");
        stores.Single(s => s.Source == ProductSource.Kabum).Name.Should().Be("KaBuM!");
    }

    [Fact]
    public void Execute_MarksCanFetchByUrlOnlyForStoresWithAnAvailableFetcher()
    {
        var useCase = Build(
            [Fetcher(ProductSource.MercadoLivre, true), Fetcher(ProductSource.Kabum, false)], []);

        var stores = useCase.Execute();

        stores.Single(s => s.Source == ProductSource.MercadoLivre).CanFetchByUrl.Should().BeTrue();
        stores.Single(s => s.Source == ProductSource.Kabum).CanFetchByUrl.Should().BeFalse();
    }

    [Fact]
    public void Execute_MarksCanGenerateAffiliateLinkOnlyWhenARealNetworkIsAvailable()
    {
        var useCase = Build(
            [],
            [
                Generator(ProductSource.Kabum, true),
                Generator(ProductSource.MercadoLivre, false),
                Generator(ProductSource.Manual, true) // passthrough genérico: nunca conta como rede
            ]);

        var stores = useCase.Execute();

        stores.Single(s => s.Source == ProductSource.Kabum).CanGenerateAffiliateLink.Should().BeTrue();
        stores.Single(s => s.Source == ProductSource.MercadoLivre).CanGenerateAffiliateLink.Should().BeFalse();
    }

    [Fact]
    public void Execute_MarksCanSearchOnlyForStoresWithAnAvailableSearcher()
    {
        var useCase = Build([], [], [Searcher(ProductSource.MercadoLivre, true), Searcher(ProductSource.Kabum, false)]);

        var stores = useCase.Execute();

        stores.Single(s => s.Source == ProductSource.MercadoLivre).CanSearch.Should().BeTrue();
        stores.Single(s => s.Source == ProductSource.Kabum).CanSearch.Should().BeFalse();
    }

    [Fact]
    public void Execute_WithoutSearchers_NoStoreCanSearch()
    {
        Build([Fetcher(ProductSource.MercadoLivre, true)], []).Execute().Should().OnlyContain(s => !s.CanSearch);
    }
}
