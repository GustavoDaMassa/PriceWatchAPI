using FluentAssertions;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Services;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://www.kabum.com.br/produto/123/placa", "https://www.kabum.com.br/produto/123/placa")]
    [InlineData("  https://www.kabum.com.br/produto/123  ", "https://www.kabum.com.br/produto/123")]
    [InlineData("HTTPS://WWW.KABUM.COM.BR/produto/123", "https://www.kabum.com.br/produto/123")]
    [InlineData("www.kabum.com.br/produto/123", "https://www.kabum.com.br/produto/123")]
    [InlineData("https://www.kabum.com.br:443/produto/123", "https://www.kabum.com.br/produto/123")]
    [InlineData("http://www.kabum.com.br:80/produto/123", "http://www.kabum.com.br/produto/123")]
    public void Normalize_CleansSchemeHostAndPorts(string input, string expected)
    {
        UrlNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_RemovesFragmentsAndTrackingParametersButKeepsTheRest()
    {
        var result = UrlNormalizer.Normalize(
            "https://produto.mercadolivre.com.br/MLB-1?variation=42&utm_source=zap&utm_medium=x&gclid=abc&fbclid=def&matt_tool=1&color=azul#reviews");

        result.Should().Be("https://produto.mercadolivre.com.br/MLB-1?variation=42&color=azul");
    }

    [Fact]
    public void Normalize_WhenAllParametersAreTracking_DropsTheQueryString()
    {
        UrlNormalizer.Normalize("https://x.com.br/p/1?utm_source=a&utm_campaign=b").Should().Be("https://x.com.br/p/1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url at all")]
    public void Normalize_RejectsEmptyOrNonHttpUrls(string input)
    {
        var act = () => UrlNormalizer.Normalize(input);

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Normalize_RejectsUrlsWithCredentials()
    {
        var act = () => UrlNormalizer.Normalize("https://user:pass@example.com/p/1");

        act.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Normalize_RejectsAbsurdlyLongUrls()
    {
        var act = () => UrlNormalizer.Normalize("https://example.com/" + new string('a', 3000));

        act.Should().Throw<BusinessException>();
    }
}
