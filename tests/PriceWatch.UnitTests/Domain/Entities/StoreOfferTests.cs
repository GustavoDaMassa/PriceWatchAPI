using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class StoreOfferTests
{
    private static StoreOffer CreateOffer() =>
        StoreOffer.Create("product-1", ProductSource.MercadoLivre, "http://example.com/item", gtin: "123456789");

    [Fact]
    public void Create_ShouldSetInitialValues_IsActiveTrue()
    {
        var offer = CreateOffer();

        offer.ProductId.Should().Be("product-1");
        offer.IsActive.Should().BeTrue();
        offer.CurrentPrice.Should().Be(0m);
        offer.LowestPrice.Should().Be(0m);
        offer.AffiliateUrl.Should().BeNull();
        offer.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RecordPrice_WithFirstPrice_ShouldSetCurrentAndLowest()
    {
        var offer = CreateOffer();

        var snapshot = offer.RecordPrice(80m);

        offer.CurrentPrice.Should().Be(80m);
        offer.LowestPrice.Should().Be(80m);
        snapshot.ProductId.Should().Be(offer.Id);
        snapshot.Price.Should().Be(80m);
    }

    [Fact]
    public void RecordPrice_WithLowerPrice_ShouldUpdateLowest()
    {
        var offer = CreateOffer();
        offer.RecordPrice(80m);

        offer.RecordPrice(60m);

        offer.CurrentPrice.Should().Be(60m);
        offer.LowestPrice.Should().Be(60m);
    }

    [Fact]
    public void RecordPrice_WithHigherPrice_ShouldNotUpdateLowest()
    {
        var offer = CreateOffer();
        offer.RecordPrice(60m);

        offer.RecordPrice(80m);

        offer.CurrentPrice.Should().Be(80m);
        offer.LowestPrice.Should().Be(60m);
    }

    [Fact]
    public void SetAffiliateUrl_ShouldCacheGeneratedLink()
    {
        var offer = CreateOffer();

        offer.SetAffiliateUrl("https://affiliate.example.com/redirect?id=1");

        offer.AffiliateUrl.Should().Be("https://affiliate.example.com/redirect?id=1");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var offer = CreateOffer();

        offer.Deactivate();

        offer.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ReassignToProduct_ShouldChangeProductId()
    {
        var offer = CreateOffer();

        offer.ReassignToProduct("product-2");

        offer.ProductId.Should().Be("product-2");
    }

    [Fact]
    public void Create_WithExplicitId_ShouldUseIt()
    {
        var offer = StoreOffer.Create("product-1", ProductSource.MercadoLivre, "http://example.com/item", id: "legacy-id");

        offer.Id.Should().Be("legacy-id");
    }

    [Fact]
    public void PostponeCheck_ShouldMoveNextCheckAtForwardWithoutTouchingPrices()
    {
        var offer = CreateOffer();
        offer.RecordPrice(100m);

        offer.PostponeCheck(TimeSpan.FromMinutes(15));

        offer.NextCheckAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
        offer.CurrentPrice.Should().Be(100m);
    }

    [Theory]
    [InlineData(0, 80, 80)]
    [InlineData(100, 80, 80)]
    [InlineData(100, 120, 100)]
    [InlineData(100, 0, 100)]
    public void RestoreLowestPrice_ShouldKeepTheMinimumPositiveValue(decimal current, decimal historic, decimal expected)
    {
        var offer = CreateOffer();
        if (current > 0) offer.RecordPrice(current);

        offer.RestoreLowestPrice(historic);

        offer.LowestPrice.Should().Be(expected);
    }
}
