using FluentAssertions;
using PriceWatch.Domain.Entities;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class ClickLogTests
{
    [Fact]
    public void Create_ShouldRecordWhoClickedWhichOfferAndWhen()
    {
        var click = ClickLog.Create("offer-1", "user-1");

        click.Id.Should().NotBeNullOrEmpty();
        click.OfferId.Should().Be("offer-1");
        click.UserId.Should().Be("user-1");
        click.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
