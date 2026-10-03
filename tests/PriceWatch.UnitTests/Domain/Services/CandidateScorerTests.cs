using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Services;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class CandidateScorerTests
{
    private static OfferSearchResult Result(string name, string? gtin = null, string? brand = null, string? model = null) =>
        new("https://loja.com.br/p/1", name, 100m, null, gtin, brand, model);

    [Fact]
    public void Score_WithTheSameGtin_AcceptsWithFullScoreAndGtinReason()
    {
        var product = Product.Create("Notebook Acer", gtin: "7891234567895");

        var score = CandidateScorer.Score(product, Result("Qualquer titulo diferente", gtin: "7891234567895"));

        score.Accepted.Should().BeTrue();
        score.Score.Should().Be(1d);
        score.Reason.Should().Be(CandidateReason.Gtin);
    }

    [Fact]
    public void Score_ComparesGtinsIgnoringLeadingZeros()
    {
        var product = Product.Create("Notebook", gtin: "00190199719729");

        var score = CandidateScorer.Score(product, Result("Notebook", gtin: "190199719729"));

        score.Accepted.Should().BeTrue();
        score.Reason.Should().Be(CandidateReason.Gtin);
    }

    [Fact]
    public void Score_WithDifferentGtins_RejectsEvenWithIdenticalTitles()
    {
        var product = Product.Create("iPhone 15 128GB Azul", gtin: "7891234567895");

        var score = CandidateScorer.Score(product, Result("iPhone 15 128GB Azul", gtin: "7899999999999"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_ForTheSameProductWithAReorderedTitle_AcceptsByKeyword()
    {
        var product = Product.Create("Notebook Gamer Acer Nitro 5 AN515-58 Intel Core i5 16GB RTX 4050", brand: "Acer", model: "AN515-58");

        var score = CandidateScorer.Score(product, Result("Acer Nitro 5 AN515-58 Notebook Gamer i5 16GB RTX 4050"));

        score.Accepted.Should().BeTrue();
        score.Reason.Should().Be(CandidateReason.Keyword);
        score.Score.Should().BeInRange(0.5, 1.0);
    }

    [Fact]
    public void Score_RejectsAVariantWithADifferentCapacity()
    {
        var product = Product.Create("Apple iPhone 15 128GB Azul", brand: "Apple");

        var score = CandidateScorer.Score(product, Result("Apple iPhone 15 256GB Azul"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_RejectsADifferentBrand()
    {
        var product = Product.Create("Mouse Gamer RGB 16000 DPI", brand: "Logitech");

        var score = CandidateScorer.Score(product, Result("Mouse Gamer RGB 16000 DPI", brand: "Razer"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_AcceptsWhenTheBrandOnlyAppearsInTheCandidateTitle()
    {
        var product = Product.Create("Teclado Mecanico Redragon Kumara K552 Switch Red", brand: "Redragon", model: "K552");

        var score = CandidateScorer.Score(product, Result("Teclado Mecanico Gamer Redragon Kumara K552 Rgb Switch Red"));

        score.Accepted.Should().BeTrue();
    }

    [Fact]
    public void Score_RejectsAnUnrelatedProduct()
    {
        var product = Product.Create("Notebook Acer Nitro 5 16GB", brand: "Acer");

        var score = CandidateScorer.Score(product, Result("Cadeira Gamer Reclinavel Preta"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_RejectsASimilarTitleOfADifferentModel()
    {
        var product = Product.Create("Samsung Galaxy S23 128GB", brand: "Samsung", model: "S23");

        var score = CandidateScorer.Score(product, Result("Samsung Galaxy A54 128GB", brand: "Samsung"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_IsNotRejectedWhenOnlyOneSideListsAMeasurement()
    {
        var product = Product.Create("Mouse Gamer Logitech G203 RGB", brand: "Logitech", model: "G203");

        var score = CandidateScorer.Score(product, Result("Mouse Gamer Logitech G203 RGB 8000 DPI"));

        score.Accepted.Should().BeTrue();
    }

    [Fact]
    public void Score_RejectsAConfigurationVariantWithDifferentMemoryAndStorage()
    {
        var product = Product.Create("Notebook Dell Inspiron 15 3520 Core i5 8GB 256GB SSD", brand: "Dell", model: "3520");

        var score = CandidateScorer.Score(product, Result("Notebook Dell Inspiron 15 3520 Core i7 16GB 512GB SSD"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_RejectsAnAccessoryThatOnlySharesTheProductName()
    {
        var product = Product.Create("Apple iPhone 15 128GB Azul", brand: "Apple", model: "A3092");

        var score = CandidateScorer.Score(product, Result("Capa Protetora Silicone para iPhone 15 Transparente"));

        score.Accepted.Should().BeFalse();
    }

    [Fact]
    public void Score_NeverReturnsAKeywordScoreAboveTheGtinScore()
    {
        var product = Product.Create("Mouse Gamer Logitech G203 RGB", brand: "Logitech", model: "G203");

        var score = CandidateScorer.Score(product, Result("Mouse Gamer Logitech G203 RGB", brand: "Logitech", model: "G203"));

        score.Accepted.Should().BeTrue();
        score.Score.Should().BeLessThan(1d);
    }
}
