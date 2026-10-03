using FluentAssertions;
using PriceWatch.Domain.Services;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class TitleSimilarityTests
{
    [Fact]
    public void Normalize_ShouldLowercaseTrimAndCollapseWhitespace()
    {
        TitleSimilarity.Normalize("  iPhone   15  128GB  ").Should().Be("iphone 15 128gb");
    }

    [Fact]
    public void Normalize_ShouldRemovePunctuation()
    {
        TitleSimilarity.Normalize("iPhone 15 - 128GB (Azul)").Should().Be("iphone 15 128gb azul");
    }

    [Fact]
    public void Ratio_ForIdenticalTitles_ReturnsOne()
    {
        TitleSimilarity.Ratio("iPhone 15 128GB", "iPhone 15 128GB").Should().Be(1d);
    }

    [Fact]
    public void Ratio_ForIdenticalTitlesModuloCaseAndPunctuation_ReturnsOne()
    {
        TitleSimilarity.Ratio("iPhone 15 - 128GB", "iphone 15 128gb").Should().Be(1d);
    }

    [Fact]
    public void Ratio_ForCompletelyDifferentTitles_ReturnsLowScore()
    {
        var ratio = TitleSimilarity.Ratio("iPhone 15 128GB", "Cadeira Gamer XPTO");

        ratio.Should().BeLessThan(0.5);
    }

    [Fact]
    public void Ratio_ForSlightlyDifferentTitles_ReturnsHighButNotPerfectScore()
    {
        var ratio = TitleSimilarity.Ratio("iPhone 15 128GB Azul", "iPhone 15 128GB Preto");

        ratio.Should().BeInRange(0.7, 0.99);
    }

    [Fact]
    public void Ratio_IsSymmetric()
    {
        var a = "Notebook Dell Inspiron 15";
        var b = "Notebook Dell Inspiron 15 3520";

        TitleSimilarity.Ratio(a, b).Should().Be(TitleSimilarity.Ratio(b, a));
    }
}
