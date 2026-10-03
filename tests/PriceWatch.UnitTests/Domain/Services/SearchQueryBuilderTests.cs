using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Services;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class SearchQueryBuilderTests
{
    [Fact]
    public void Build_UsesTheLeadingMeaningfulWordsOfTheTitle()
    {
        var product = Product.Create("Notebook Gamer Acer Nitro 5 AN515-58 Intel Core i5 16GB RTX 4050 Tela 15.6 Full HD");

        var query = SearchQueryBuilder.Build(product);

        query.Split(' ').Length.Should().BeLessThanOrEqualTo(8);
        query.Should().StartWith("notebook gamer acer nitro");
    }

    [Fact]
    public void Build_PrependsTheBrandWhenTheTitleDoesNotMentionIt()
    {
        var product = Product.Create("Smartphone 128GB Azul", brand: "Motorola");

        SearchQueryBuilder.Build(product).Should().StartWith("motorola ");
    }

    [Fact]
    public void Build_AppendsTheModelWhenTheTitleDoesNotMentionIt()
    {
        var product = Product.Create("Fone de Ouvido Bluetooth", brand: "JBL", model: "Tune 510BT");

        SearchQueryBuilder.Build(product).Should().Contain("510bt");
    }

    [Fact]
    public void Build_DropsFillerWords()
    {
        var product = Product.Create("Cadeira de Escritorio para Home Office com Apoio");

        SearchQueryBuilder.Build(product).Split(' ').Should().NotContain(new[] { "de", "para", "com" });
    }
}
