using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Fetchers;

namespace PriceWatch.UnitTests.Infrastructure.Fetchers;

public class MercadoLivreFetcherTests
{
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
        return new MercadoLivreFetcher(new HttpClient(handler.Object));
    }

    [Theory]
    [InlineData("https://produto.mercadolivre.com.br/MLB-1234567890-titulo-_JM")]
    [InlineData("https://www.mercadolivre.com.br/titulo/p/MLB1234567890")]
    [InlineData("https://www.mercadolivre.com.br/MLB1234567890-titulo-_JM")]
    public async Task FetchAsync_ValidUrl_ReturnsPrice(string url)
    {
        var fetcher = BuildFetcher(HttpStatusCode.OK, """{"price": 1299.99}""");

        var result = await fetcher.FetchAsync(url);

        result.Should().Be(1299.99m);
    }

    [Theory]
    [InlineData("https://www.amazon.com.br/produto/123")]
    [InlineData("https://www.mercadolivre.com.br/sem-id-aqui")]
    [InlineData("invalid-url")]
    public async Task FetchAsync_InvalidUrl_ThrowsBusinessException(string url)
    {
        var fetcher = BuildFetcher(HttpStatusCode.OK, """{"price": 100}""");

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
    [InlineData("""{"price": 0}""")]
    [InlineData("""{"price": null}""")]
    [InlineData("{}")]
    public async Task FetchAsync_InvalidPrice_ThrowsBusinessException(string json)
    {
        var fetcher = BuildFetcher(HttpStatusCode.OK, json);

        var act = () => fetcher.FetchAsync("https://produto.mercadolivre.com.br/MLB-1234567890-titulo-_JM");

        await act.Should().ThrowAsync<BusinessException>()
            .WithMessage("*valid price*");
    }

    [Fact]
    public void Source_ShouldBeMercadolivre()
    {
        var fetcher = new MercadoLivreFetcher(new HttpClient());

        fetcher.Source.Should().Be("mercadolivre");
    }
}
