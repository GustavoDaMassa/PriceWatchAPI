using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.UnitTests.Infrastructure.Fetchers;

public class MercadoLivreFetcherTests
{
    private static MercadoLivreTokenService BuildTokenService(string token = "test-token")
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    $$"""{"access_token":"{{token}}","expires_in":21600}""",
                    Encoding.UTF8, "application/json")
            });

        var settings = Options.Create(new MercadoLivreSettings { ClientId = "id", ClientSecret = "secret" });
        return new MercadoLivreTokenService(new HttpClient(handler.Object), settings, NullLogger<MercadoLivreTokenService>.Instance);
    }

    private static MercadoLivreFetcher BuildFetcher(
        string productJson = """{"name": "Produto Teste"}""",
        string itemsJson = """{"results": [{"price": 1299.99}]}""",
        HttpStatusCode productStatus = HttpStatusCode.OK,
        HttpStatusCode itemsStatus = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsolutePath.EndsWith("/items")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = itemsStatus,
                Content = new StringContent(itemsJson, Encoding.UTF8, "application/json")
            });

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => !r.RequestUri!.AbsolutePath.EndsWith("/items")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = productStatus,
                Content = new StringContent(productJson, Encoding.UTF8, "application/json")
            });

        return new MercadoLivreFetcher(new HttpClient(handler.Object), BuildTokenService());
    }

    [Theory]
    [InlineData("https://www.mercadolivre.com.br/smartphone/p/MLB67361578")]
    [InlineData("https://www.mercadolivre.com.br/produto/p/MLB1234567890")]
    [InlineData("https://www.mercadolivre.com.br/produto/p/MLB1234567890?filter=x")]
    [InlineData("https://www.mercadolivre.com.br/produto/p/MLB1234567890#section")]
    public async Task FetchAsync_CatalogUrl_ReturnsPriceAndName(string url)
    {
        var fetcher = BuildFetcher(
            productJson: """{"name": "Produto Teste"}""",
            itemsJson: """{"results": [{"price": 1299.99}, {"price": 1399.00}]}""");

        var result = await fetcher.FetchAsync(url);

        result.Price.Should().Be(1299.99m);
        result.Name.Should().Be("Produto Teste");
    }

    [Theory]
    [InlineData("https://produto.mercadolivre.com.br/MLB-1234567890-titulo-_JM")]
    [InlineData("https://www.mercadolivre.com.br/MLB1234567890-titulo-_JM")]
    public async Task FetchAsync_ItemUrl_ThrowsBusinessExceptionWithHint(string url)
    {
        var fetcher = BuildFetcher();

        var act = () => fetcher.FetchAsync(url);

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*catalog page URL*");
    }

    [Theory]
    [InlineData("https://www.amazon.com.br/produto/123")]
    [InlineData("invalid-url")]
    [InlineData("https://www.mercadolivre.com.br/sem-id-aqui")]
    public async Task FetchAsync_InvalidUrl_ThrowsBusinessException(string url)
    {
        var fetcher = BuildFetcher();

        var act = () => fetcher.FetchAsync(url);

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*Invalid Mercado Livre URL*");
    }

    [Fact]
    public async Task FetchAsync_ProductApiReturnsError_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher(productStatus: HttpStatusCode.NotFound);

        var act = () => fetcher.FetchAsync("https://www.mercadolivre.com.br/produto/p/MLB1234567890");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*404*");
    }

    [Fact]
    public async Task FetchAsync_ItemsApiReturnsError_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher(itemsStatus: HttpStatusCode.InternalServerError);

        var act = () => fetcher.FetchAsync("https://www.mercadolivre.com.br/produto/p/MLB1234567890");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*500*");
    }

    [Theory]
    [InlineData("""{"results": []}""")]
    [InlineData("""{"results": [{"price": 0}]}""")]
    [InlineData("""{"results": null}""")]
    public async Task FetchAsync_NoValidPriceInItems_ThrowsBusinessException(string itemsJson)
    {
        var fetcher = BuildFetcher(itemsJson: itemsJson);

        var act = () => fetcher.FetchAsync("https://www.mercadolivre.com.br/produto/p/MLB1234567890");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*valid price*");
    }

    [Theory]
    [InlineData("https://produto.mercadolivre.com.br/MLB-123-titulo-_JM", true)]
    [InlineData("https://www.mercadolivre.com.br/titulo/p/MLB123", true)]
    [InlineData("https://www.mercadolibre.com/titulo/MLB123", true)]
    [InlineData("https://www.amazon.com.br/produto/123", false)]
    [InlineData("https://www.kabum.com.br/produto/123", false)]
    public void CanHandle_ReturnsExpectedResult(string url, bool expected)
    {
        var fetcher = new MercadoLivreFetcher(new HttpClient(), BuildTokenService());

        fetcher.CanHandle(url).Should().Be(expected);
    }

    [Fact]
    public void ProductSource_ShouldBeMercadoLivre()
    {
        var fetcher = new MercadoLivreFetcher(new HttpClient(), BuildTokenService());

        fetcher.ProductSource.Should().Be(ProductSource.MercadoLivre);
    }
}
