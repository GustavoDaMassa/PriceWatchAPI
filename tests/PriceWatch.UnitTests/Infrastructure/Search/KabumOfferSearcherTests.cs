using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Enums;
using PriceWatch.Infrastructure.Search;
using PriceWatch.Infrastructure.Settings;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Search;

public class KabumOfferSearcherTests
{
    private static KabumOfferSearcher Build(
        string responseJson = """{"offers":[]}""",
        HttpStatusCode status = HttpStatusCode.OK,
        Action<HttpRequestMessage>? capture = null,
        bool configured = true,
        bool throwOnSend = false)
    {
        var handler = new Mock<HttpMessageHandler>();
        var setup = handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capture?.Invoke(req));

        if (throwOnSend)
            setup.ThrowsAsync(new HttpRequestException("network error"));
        else
            setup.ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var settings = Options.Create(new LomadeeSettings
        {
            AppToken = configured ? "app-token" : null,
            SourceId = configured ? "source-id" : null,
            BaseUrl = "https://api.lomadee.com/v2"
        });

        return new KabumOfferSearcher(new HttpClient(handler.Object), settings, NullLogger<KabumOfferSearcher>.Instance);
    }

    private const string TwoOffers = """
        {"offers":[
          {"description":"Placa de Video RTX 4060 8GB","link":"https://www.kabum.com.br/produto/1/placa-rtx-4060","barcode":"7891234567895","images":["https://img/1.jpg"],"prices":[{"price":2899.90},{"price":2599.90}]},
          {"description":"Placa de Video RTX 4060 Ti","link":"https://www.kabum.com.br/produto/2/placa-rtx-4060-ti","barcode":"7890000000001","images":[],"prices":[{"price":3299.00}]}
        ]}
        """;

    [Fact]
    public void ProductSource_IsKabum()
    {
        Build().ProductSource.Should().Be(ProductSource.Kabum);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsAvailable_FollowsTheLomadeeCredentials(bool configured)
    {
        Build(configured: configured).IsAvailable.Should().Be(configured);
    }

    [Fact]
    public async Task SearchByKeyword_MapsOffersUsingTheLowestPrice()
    {
        HttpRequestMessage? sent = null;
        var searcher = Build(TwoOffers, capture: r => sent = r);

        var results = await searcher.SearchByKeywordAsync("placa rtx 4060");

        results.Should().HaveCount(2);
        results[0].Url.Should().Be("https://www.kabum.com.br/produto/1/placa-rtx-4060");
        results[0].Name.Should().Be("Placa de Video RTX 4060 8GB");
        results[0].Price.Should().Be(2599.90m);
        results[0].ImageUrl.Should().Be("https://img/1.jpg");
        results[0].Gtin.Should().Be("7891234567895");
        sent!.RequestUri!.AbsoluteUri.Should().Contain("/app-token/offer/_search").And.Contain("keyword=placa%20rtx%204060");
    }

    [Fact]
    public async Task SearchByKeyword_IgnoresOtherStoresAndOffersWithoutAPrice()
    {
        var searcher = Build("""
            {"offers":[
              {"description":"Outra loja","link":"https://www.outraloja.com.br/p/1","prices":[{"price":10}]},
              {"description":"Sem preco","link":"https://www.kabum.com.br/produto/9/x","prices":[]},
              {"description":"Valido","link":"https://www.kabum.com.br/produto/3/y","prices":[{"price":50}]}
            ]}
            """);

        var results = await searcher.SearchByKeywordAsync("qualquer");

        results.Should().ContainSingle().Which.Name.Should().Be("Valido");
    }

    [Fact]
    public async Task SearchByKeyword_ReturnsAtMostFiveResults()
    {
        var offers = string.Join(",", Enumerable.Range(1, 9).Select(i =>
            $$"""{"description":"Produto {{i}}","link":"https://www.kabum.com.br/produto/{{i}}/p","prices":[{"price":{{i}}0}]}"""));
        var searcher = Build($$"""{"offers":[{{offers}}]}""");

        var results = await searcher.SearchByKeywordAsync("produto");

        results.Should().HaveCount(5);
    }

    [Fact]
    public async Task SearchByGtin_KeepsOnlyOffersWhoseBarcodeMatches()
    {
        var searcher = Build(TwoOffers);

        var results = await searcher.SearchByGtinAsync("7891234567895");

        results.Should().ContainSingle().Which.Name.Should().Be("Placa de Video RTX 4060 8GB");
    }

    [Fact]
    public async Task SearchByGtin_IgnoresLeadingZerosInTheBarcode()
    {
        var searcher = Build("""{"offers":[{"description":"P","link":"https://www.kabum.com.br/produto/1/p","barcode":"07891234567895","prices":[{"price":10}]}]}""");

        var results = await searcher.SearchByGtinAsync("7891234567895");

        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Search_WhenLomadeeIsNotConfigured_ReturnsNothingWithoutCallingTheApi()
    {
        var called = false;
        var searcher = Build(TwoOffers, capture: _ => called = true, configured: false);

        var byKeyword = await searcher.SearchByKeywordAsync("placa");
        var byGtin = await searcher.SearchByGtinAsync("7891234567895");

        byKeyword.Should().BeEmpty();
        byGtin.Should().BeEmpty();
        called.Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Search_WhenTheApiFails_ReturnsNothingInsteadOfThrowing(HttpStatusCode status)
    {
        var searcher = Build(status: status);

        (await searcher.SearchByKeywordAsync("placa")).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WhenTheNetworkFailsOrTheBodyIsInvalid_ReturnsNothing()
    {
        (await Build(throwOnSend: true).SearchByKeywordAsync("placa")).Should().BeEmpty();
        (await Build("not json").SearchByKeywordAsync("placa")).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithABlankQuery_ReturnsNothing()
    {
        (await Build(TwoOffers).SearchByKeywordAsync("   ")).Should().BeEmpty();
    }
}
