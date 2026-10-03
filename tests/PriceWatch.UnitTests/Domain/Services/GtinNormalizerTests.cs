using FluentAssertions;
using PriceWatch.Domain.Services;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class GtinNormalizerTests
{
    [Theory]
    [InlineData("7891234567895", "7891234567895")]      // EAN-13
    [InlineData("00190199719729", "00190199719729")]    // GTIN-14
    [InlineData("036000291452", "036000291452")]        // UPC-A (12)
    [InlineData("96385074", "96385074")]                // EAN-8
    [InlineData(" 789 123 456 7895 ", "7891234567895")] // espaços
    [InlineData("789-1234567895", "7891234567895")]     // hífen
    public void Normalize_KeepsDigitsOfValidLengths(string input, string expected)
    {
        GtinNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("123456789")]
    [InlineData("123456789012345")]
    [InlineData("78912345678AB")]
    public void Normalize_ReturnsNullForAnythingThatIsNotAGtin(string? input)
    {
        GtinNormalizer.Normalize(input).Should().BeNull();
    }
}
