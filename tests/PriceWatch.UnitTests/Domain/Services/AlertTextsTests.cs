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

    [Fact]
    public void Message_InPortuguese_UsesRealsAndBrazilianFormat()
    {
        AlertTexts.Message("pt-BR", NotificationType.TargetPriceReached, "iPhone 15", 3800m)
            .Should().Be("'iPhone 15' atingiu o seu preço-alvo! Preço atual: R$ 3.800,00");
        AlertTexts.Message("pt", NotificationType.NewLowestPrice, "iPhone 15", 3799.9m)
            .Should().Be("'iPhone 15' chegou ao menor preço já visto: R$ 3.799,90");
    }

    [Fact]
    public void Message_InEnglish_StillUsesReals_NotTheGenericCurrencySign()
    {
        var message = AlertTexts.Message("en", NotificationType.TargetPriceReached, "iPhone 15", 3800m);

        message.Should().Be("'iPhone 15' reached your target price! Current price: R$ 3,800.00");
        message.Should().NotContain("¤");
    }

    [Fact]
    public void EmailSubject_FollowsTheUsersLanguage()
    {
        AlertTexts.EmailSubject("pt-BR", NotificationType.TargetPriceReached, "iPhone 15")
            .Should().Be("PriceWatch: 'iPhone 15' atingiu o seu preço-alvo!");
        AlertTexts.EmailSubject("pt-BR", NotificationType.NewLowestPrice, "iPhone 15")
            .Should().Be("PriceWatch: novo menor preço para 'iPhone 15'!");
        AlertTexts.EmailSubject("en", NotificationType.TargetPriceReached, "iPhone 15")
            .Should().Be("PriceWatch: 'iPhone 15' reached your target price!");
    }
}
