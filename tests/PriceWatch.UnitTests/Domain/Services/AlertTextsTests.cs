using FluentAssertions;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Services;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class AlertTextsTests
{
    [Fact]
    public void Push_InPortuguese_UsesBrazilianCurrencyFormat()
    {
        var message = AlertTexts.Push("pt-BR", NotificationType.TargetPriceReached, "Notebook Acer", 2599.9m);

        message.Title.Should().Be("Preço-alvo atingido");
        message.Body.Should().Be("Notebook Acer: R$ 2.599,90");
    }

    [Fact]
    public void Push_ForANewLowestPrice_SaysSo()
    {
        var message = AlertTexts.Push("pt", NotificationType.NewLowestPrice, "Mouse", 99m);

        message.Title.Should().Be("Novo menor preço");
        message.Body.Should().Be("Mouse: R$ 99,00");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("")]
    [InlineData("fr")]
    public void Push_InAnythingButPortuguese_FallsBackToEnglish(string locale)
    {
        var message = AlertTexts.Push(locale, NotificationType.TargetPriceReached, "Mouse", 1299m);

        message.Title.Should().Be("Target price reached");
        message.Body.Should().Be("Mouse: R$ 1,299.00");
    }

    [Fact]
    public void Push_CarriesTheTypeInTheData()
    {
        var message = AlertTexts.Push("pt-BR", NotificationType.NewLowestPrice, "X", 1m);

        message.Data!["type"].Should().Be("NewLowestPrice");
    }
}
