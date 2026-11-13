using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Search;
using PriceWatch.Infrastructure.Settings;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Search;

public class MercadoLivreOfferSearcherTests
{
    private readonly Mock<IPriceFetcher> _catalogFetcher = new();
    private readonly Mock<IMercadoLivreTokenProvider> _tokens = new();
    private readonly List<HttpRequestMessage> _sent = new();

    public MercadoLivreOfferSearcherTests()
    {
        _tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("ml-token");
        _catalogFetcher.Setup(f => f.FetchAsync(It.IsAny<string>()))
            .ReturnsAsync((string url) => new ProductFetchResult(1999m, $"Produto {url[^3..]}", "https://img/x.jpg", "7891234567895", "Samsung", "SM-S911"));
    }

    private MercadoLivreOfferSearcher Build(
        string responseJson = """{"results":[]}""",
        HttpStatusCode status = HttpStatusCode.OK,
        bool configured = true,
        bool throwOnSend = false)
    {
        var handler = new Mock<HttpMessageHandler>();
        var setup = handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => _sent.Add(req));

        if (throwOnSend)
            setup.ThrowsAsync(new HttpRequestException("network error"));
        else
            setup.ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var settings = Options.Create(new MercadoLivreSettings
        {
            ClientId = configured ? "id" : "",
            ClientSecret = configured ? "secret" : ""
        });

        return new MercadoLivreOfferSearcher(
            new HttpClient(handler.Object), _tokens.Object, settings, _catalogFetcher.Object,
            NullLogger<MercadoLivreOfferSearcher>.Instance);
    }

    private const string FourProducts = """{"results":[{"id":"MLB111","name":"A"},{"id":"MLB222","name":"B"},{"id":"MLB333","name":"C"},{"id":"MLB444","name":"D"}]}""";

    [Fact]
    public void ProductSource_IsMercadoLivre()
    {
        Build().ProductSource.Should().Be(ProductSource.MercadoLivre);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsAvailable_FollowsTheMercadoLivreCredentials(bool configured)
    {
        Build(configured: configured).IsAvailable.Should().Be(configured);
    }

    [Fact]
    public async Task SearchByGtin_QueriesTheCatalogByProductIdentifierWithTheBearerToken()
    {
        var searcher = Build("""{"results":[{"id":"MLB111","name":"A"}]}""");

        var results = await searcher.SearchByGtinAsync("7891234567895");

        var request = _sent.Single();
        request.RequestUri!.ToString().Should()
            .Contain("api.mercadolibre.com/products/search")
            .And.Contain("product_identifier=7891234567895")
            .And.Contain("site_id=MLB")
            .And.Contain("status=active")
            .And.Contain("limit=20");
        request.Headers.Authorization!.Parameter.Should().Be("ml-token");
        results.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchByKeyword_EncodesTheQuery()
    {
        var searcher = Build();

        await searcher.SearchByKeywordAsync("galaxy s23 128gb");

        _sent.Single().RequestUri!.AbsoluteUri.Should().Contain("q=galaxy%20s23%20128gb");
    }

    [Fact]
    public async Task Search_BuildsCatalogUrlsAndReadsPriceAndDetailsThroughTheCatalogFetcher()
    {
        var searcher = Build("""{"results":[{"id":"MLB111","name":"A"}]}""");

        var results = await searcher.SearchByGtinAsync("7891234567895");

        var result = results.Single();
        result.Url.Should().Be("https://www.mercadolivre.com.br/p/MLB111");
        result.Price.Should().Be(1999m);
        result.Name.Should().Be("Produto 111");
        result.Gtin.Should().Be("7891234567895");
        result.Brand.Should().Be("Samsung");
        result.Model.Should().Be("SM-S911");
        _catalogFetcher.Verify(f => f.FetchAsync("https://www.mercadolivre.com.br/p/MLB111"), Times.Once);
    }

    [Fact]
    public async Task Search_ReadsAtMostTwentyCatalogProducts()
    {
        var ids = string.Join(",", Enumerable.Range(101, 25).Select(i => $$"""{"id":"MLB{{i}}","name":"P"}"""));
        var searcher = Build($$"""{"results":[{{ids}}]}""");

        var results = await searcher.SearchByKeywordAsync("galaxy");

        results.Should().HaveCount(20);
        _catalogFetcher.Verify(f => f.FetchAsync(It.Is<string>(u => u.EndsWith("MLB121"))), Times.Never);
    }

    [Fact]
    public async Task Search_SkipsProductsWhoseDetailsCannotBeRead()
    {
        _catalogFetcher.Setup(f => f.FetchAsync("https://www.mercadolivre.com.br/p/MLB222"))
            .ThrowsAsync(new BusinessException("no price"));
        var searcher = Build(FourProducts);

        var results = await searcher.SearchByKeywordAsync("galaxy");

        results.Select(r => r.Url).Should().Equal(
            "https://www.mercadolivre.com.br/p/MLB111", "https://www.mercadolivre.com.br/p/MLB333", "https://www.mercadolivre.com.br/p/MLB444");
    }

    [Fact]
    public async Task Search_WhenMercadoLivreIsNotConfigured_ReturnsNothingWithoutCallingTheApi()
    {
        var searcher = Build(FourProducts, configured: false);

        (await searcher.SearchByKeywordAsync("galaxy")).Should().BeEmpty();
        _sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Search_WhenTheApiFails_ReturnsNothingInsteadOfThrowing(HttpStatusCode status)
    {
        (await Build(status: status).SearchByKeywordAsync("galaxy")).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WhenTheNetworkOrTheTokenFails_ReturnsNothing()
    {
        (await Build(throwOnSend: true).SearchByKeywordAsync("galaxy")).Should().BeEmpty();

        _tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("auth down"));
        (await Build(FourProducts).SearchByKeywordAsync("galaxy")).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_IgnoresResultsWithoutAUsableCatalogId()
    {
        var searcher = Build("""{"results":[{"id":null,"name":"x"},{"id":"abc","name":"y"},{"id":"MLB555","name":"ok"}]}""");

        var results = await searcher.SearchByKeywordAsync("galaxy");

        results.Should().ContainSingle().Which.Url.Should().EndWith("/p/MLB555");
    }
}
