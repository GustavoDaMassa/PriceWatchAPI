using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class TrackedProductTests
{
    private static TrackedProduct CreateProduct(decimal targetPrice = 100m) =>
        TrackedProduct.Create("list-1", "user-1", "http://example.com", ProductSource.Manual, "Test Product", targetPrice);

    [Fact]
    public void Create_ShouldSetInitialValues_IsActiveTrue()
    {
        var product = CreateProduct();

        product.ListId.Should().Be("list-1");
        product.UserId.Should().Be("user-1");
        product.IsActive.Should().BeTrue();
        product.CurrentPrice.Should().Be(0m);
        product.LowestPrice.Should().Be(0m);
        product.Id.Should().NotBeNullOrEmpty();
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordPrice_WithFirstPrice_ShouldSetCurrentAndLowest()
    {
        var product = CreateProduct();

        product.RecordPrice(80m);

        product.CurrentPrice.Should().Be(80m);
        product.LowestPrice.Should().Be(80m);
    }

    [Fact]
    public void RecordPrice_WithLowerPrice_ShouldUpdateLowest()
    {
        var product = CreateProduct();
        product.RecordPrice(80m);

        product.RecordPrice(60m);

        product.CurrentPrice.Should().Be(60m);
        product.LowestPrice.Should().Be(60m);
    }

    [Fact]
    public void RecordPrice_WithHigherPrice_ShouldNotUpdateLowest()
    {
        var product = CreateProduct();
        product.RecordPrice(60m);

        product.RecordPrice(80m);

        product.CurrentPrice.Should().Be(80m);
        product.LowestPrice.Should().Be(60m);
    }

    [Fact]
    public void RecordPrice_ShouldAdvanceNextCheckAt()
    {
        var product = CreateProduct();
        var before = DateTime.UtcNow;

        product.RecordPrice(80m);

        product.NextCheckAt.Should().BeAfter(before);
        product.LastCheckedAt.Should().NotBeNull();
        product.LastCheckedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ShouldTriggerTargetAlert_WhenAtOrBelowTarget_ReturnsTrue()
    {
        var product = CreateProduct(targetPrice: 100m);
        product.RecordPrice(99m);

        product.ShouldTriggerTargetAlert().Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerTargetAlert_WhenAboveTarget_ReturnsFalse()
    {
        var product = CreateProduct(targetPrice: 100m);
        product.RecordPrice(101m);

        product.ShouldTriggerTargetAlert().Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerLowestAlert_WhenLowerThanPrevious_ReturnsTrue()
    {
        var product = CreateProduct();
        product.RecordPrice(80m);

        product.RecordPrice(70m);

        product.ShouldTriggerLowestAlert(80m).Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var product = CreateProduct();

        product.Deactivate();

        product.IsActive.Should().BeFalse();
    }
}
