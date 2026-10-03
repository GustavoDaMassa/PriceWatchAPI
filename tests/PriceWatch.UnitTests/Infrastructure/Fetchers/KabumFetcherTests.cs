using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.UnitTests.Infrastructure.Fetchers;

public class KabumFetcherTests
{
    private const string ProductUrl = "https://www.kabum.com.br/produto/123456/placa-de-video-rtx-4060";

    private static KabumFetcher BuildFetcher(
        string responseJson = """{"offers":[]}""",
        HttpStatusCode status = HttpStatusCode.OK,
        Action<HttpRequestMessage>? captureRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captureRequest?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var settings = Options.Create(new LomadeeSettings
        {
            AppToken = "test-app-token",
            SourceId = "test-source-id",
            BaseUrl = "https://api.lomadee.com/v2"
        });

        return new KabumFetcher(new HttpClient(handler.Object), settings);
    }

    [Fact]
    public async Task FetchAsync_WithExactLinkMatch_ReturnsPriceNameImageAndBarcode()
    {
        var fetcher = BuildFetcher($$"""
            {"offers":[
                {"description":"Placa de Video RTX 4060","link":"{{ProductUrl}}","barcode":"7891234567890","images":["https://kabum.com/img.jpg"],"prices":[{"price":2599.90}]}
            ]}
            """);

        var result = await fetcher.FetchAsync(ProductUrl);

        result.Price.Should().Be(2599.90m);
        result.Name.Should().Be("Placa de Video RTX 4060");
        result.ImageUrl.Should().Be("https://kabum.com/img.jpg");
        result.Gtin.Should().Be("7891234567890");
    }

    [Fact]
    public async Task FetchAsync_RequestsLomadeeWithKeywordExtractedFromSlug()
    {
        HttpRequestMessage? captured = null;
        var fetcher = BuildFetcher(
            responseJson: $$"""{"offers":[{"description":"x","link":"{{ProductUrl}}","prices":[{"price":10}]}]}""",
            captureRequest: req => captured = req);

        await fetcher.FetchAsync(ProductUrl);

        captured.Should().NotBeNull();
        var uri = captured!.RequestUri!.AbsoluteUri;
        uri.Should().Contain("test-app-token");
        uri.Should().Contain("sourceId=test-source-id");
        uri.Should().Contain(Uri.EscapeDataString("placa de video rtx 4060"));
    }

    [Fact]
    public async Task FetchAsync_WhenNoExactMatch_FallsBackToFirstKabumOffer()
    {
        var fetcher = BuildFetcher("""
            {"offers":[
                {"description":"Placa de Video RTX 4060 - outra variação","link":"https://www.kabum.com.br/produto/999999/outra-variacao","barcode":"111","prices":[{"price":2499.90}]}
            ]}
            """);

        var result = await fetcher.FetchAsync(ProductUrl);

        result.Price.Should().Be(2499.90m);
    }

    [Fact]
    public async Task FetchAsync_WhenNoOffersFound_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher("""{"offers":[]}""");

        var act = () => fetcher.FetchAsync(ProductUrl);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*Could not find a matching Kabum offer*");
    }

    [Fact]
    public async Task FetchAsync_WhenOnlyNonKabumOffersReturned_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher("""{"offers":[{"description":"x","link":"https://www.otherstore.com.br/produto/1","prices":[{"price":10}]}]}""");

        var act = () => fetcher.FetchAsync(ProductUrl);

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task FetchAsync_WhenApiReturnsError_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher(status: HttpStatusCode.InternalServerError);

        var act = () => fetcher.FetchAsync(ProductUrl);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*500*");
    }

    [Fact]
    public async Task FetchAsync_WhenNoValidPrice_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher($$"""{"offers":[{"description":"x","link":"{{ProductUrl}}","prices":[{"price":0}]}]}""");

        var act = () => fetcher.FetchAsync(ProductUrl);

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*valid price*");
    }

    [Theory]
    [InlineData("https://www.kabum.com.br/produto/123/item", true)]
    [InlineData("https://www.mercadolivre.com.br/produto/123", false)]
    [InlineData("https://www.amazon.com.br/produto/123", false)]
    public void CanHandle_ReturnsExpectedResult(string url, bool expected)
    {
        var fetcher = BuildFetcher();

        fetcher.CanHandle(url).Should().Be(expected);
    }

    [Fact]
    public void ProductSource_ShouldBeKabum()
    {
        var fetcher = BuildFetcher();

        fetcher.ProductSource.Should().Be(ProductSource.Kabum);
    }
}
