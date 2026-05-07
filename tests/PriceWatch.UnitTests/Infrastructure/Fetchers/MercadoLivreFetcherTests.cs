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

    private static MercadoLivreFetcher BuildFetcher(HttpStatusCode status, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        return new MercadoLivreFetcher(new HttpClient(handler.Object), BuildTokenService());
    }

    [Theory]
    [InlineData("https://produto.mercadolivre.com.br/MLB-1234567890-titulo-_JM")]
    [InlineData("https://www.mercadolivre.com.br/titulo/p/MLB1234567890")]
    [InlineData("https://www.mercadolivre.com.br/MLB1234567890-titulo-_JM")]
    public async Task FetchAsync_ValidUrl_ReturnsPriceAndName(string url)
    {
        var fetcher = BuildFetcher(HttpStatusCode.OK, """{"price": 1299.99, "title": "Produto Teste"}""");

        var result = await fetcher.FetchAsync(url);

        result.Price.Should().Be(1299.99m);
        result.Name.Should().Be("Produto Teste");
    }

    [Theory]
    [InlineData("https://www.amazon.com.br/produto/123")]
    [InlineData("https://www.mercadolivre.com.br/sem-id-aqui")]
    [InlineData("invalid-url")]
    public async Task FetchAsync_InvalidUrl_ThrowsBusinessException(string url)
    {
        var fetcher = BuildFetcher(HttpStatusCode.OK, """{"price": 100, "title": "X"}""");

        var act = () => fetcher.FetchAsync(url);

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*Invalid Mercado Livre URL*");
    }

    [Fact]
    public async Task FetchAsync_ApiReturnsError_ThrowsBusinessException()
    {
        var fetcher = BuildFetcher(HttpStatusCode.NotFound, "{}");

        var act = () => fetcher.FetchAsync("https://produto.mercadolivre.com.br/MLB-1234567890-titulo-_JM");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*404*");
    }

    [Theory]
    [InlineData("""{"price": 0, "title": "X"}""")]
    [InlineData("""{"price": null, "title": "X"}""")]
    [InlineData("{}")]
    public async Task FetchAsync_InvalidPrice_ThrowsBusinessException(string json)
    {
        var fetcher = BuildFetcher(HttpStatusCode.OK, json);

        var act = () => fetcher.FetchAsync("https://produto.mercadolivre.com.br/MLB-1234567890-titulo-_JM");

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
