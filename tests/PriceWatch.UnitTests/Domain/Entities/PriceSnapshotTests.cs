using FluentAssertions;
using PriceWatch.Domain.Entities;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class PriceSnapshotTests
{
    [Fact]
    public void Create_ShouldSetPriceAndCapturedAt()
    {
        var snapshot = PriceSnapshot.Create("product-1", 99.90m);

        snapshot.ProductId.Should().Be("product-1");
        snapshot.Price.Should().Be(99.90m);
        snapshot.CapturedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        snapshot.Id.Should().NotBeNullOrEmpty();
    }
}
