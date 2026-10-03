using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Domain.Enums;
using PriceWatch.Infrastructure.Affiliates;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.UnitTests.Infrastructure.Affiliates;

public class LomadeeDeeplinkGeneratorTests
{
    private const string OriginalUrl = "https://www.kabum.com.br/produto/123456/placa-de-video-rtx-4060";

    private static LomadeeDeeplinkGenerator BuildGenerator(
        string responseJson = """{"shortUrl":"https://s.lomadee.com/abc123"}""",
        HttpStatusCode status = HttpStatusCode.OK,
        bool throwOnSend = false)
    {
        var handler = new Mock<HttpMessageHandler>();
        var setup = handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

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
            AppToken = "test-app-token",
            SourceId = "test-source-id"
        });

        return new LomadeeDeeplinkGenerator(
            new HttpClient(handler.Object), settings, NullLogger<LomadeeDeeplinkGenerator>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_WithSuccessfulResponse_ReturnsShortUrl()
    {
        var generator = BuildGenerator();

        var result = await generator.GenerateAsync(OriginalUrl);

        result.Should().Be("https://s.lomadee.com/abc123");
    }

    [Fact]
    public async Task GenerateAsync_WhenApiReturnsError_FallsBackToOriginalUrl()
    {
        var generator = BuildGenerator(status: HttpStatusCode.InternalServerError);

        var result = await generator.GenerateAsync(OriginalUrl);

        result.Should().Be(OriginalUrl);
    }

    [Fact]
    public async Task GenerateAsync_WhenHttpCallThrows_FallsBackToOriginalUrlWithoutThrowing()
    {
        var generator = BuildGenerator(throwOnSend: true);

        var result = await generator.GenerateAsync(OriginalUrl);

        result.Should().Be(OriginalUrl);
    }

    [Fact]
    public async Task GenerateAsync_WhenResponseHasNoShortUrl_FallsBackToOriginalUrl()
    {
        var generator = BuildGenerator(responseJson: """{}""");

        var result = await generator.GenerateAsync(OriginalUrl);

        result.Should().Be(OriginalUrl);
    }

    [Theory]
    [InlineData("https://www.kabum.com.br/produto/123", true)]
    [InlineData("https://www.mercadolivre.com.br/produto/123", false)]
    public void CanHandle_ReturnsExpectedResult(string url, bool expected)
    {
        var generator = BuildGenerator();

        generator.CanHandle(url).Should().Be(expected);
    }

    [Fact]
    public void CanHandle_WhenLomadeeIsNotConfigured_ReturnsFalseSoThePassthroughIsUsed()
    {
        var generator = new LomadeeDeeplinkGenerator(
            new HttpClient(new Mock<HttpMessageHandler>().Object),
            Options.Create(new LomadeeSettings()),
            NullLogger<LomadeeDeeplinkGenerator>.Instance);

        generator.CanHandle(OriginalUrl).Should().BeFalse();
    }

    [Fact]
    public void ProductSource_ShouldBeKabum()
    {
        var generator = BuildGenerator();

        generator.ProductSource.Should().Be(ProductSource.Kabum);
    }
}
