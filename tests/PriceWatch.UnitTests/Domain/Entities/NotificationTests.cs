using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class NotificationTests
{
    [Fact]
    public void Create_ShouldSetIsReadFalse()
    {
        var notification = Notification.Create("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        notification.IsRead.Should().BeFalse();
        notification.UserId.Should().Be("user-1");
        notification.ProductId.Should().Be("prod-1");
        notification.Id.Should().NotBeNullOrEmpty();
        notification.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithTargetPriceType_ShouldFormatMessageWithPrice()
    {
        var notification = Notification.Create("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89.90m);

        notification.Type.Should().Be(NotificationType.TargetPriceReached);
        notification.Message.Should().Contain("89");
        notification.Message.Should().Contain("Produto X");
    }

    [Fact]
    public void Create_WithNewLowestType_ShouldFormatMessageWithPrice()
    {
        var notification = Notification.Create("user-1", "prod-1", "Produto Y", NotificationType.NewLowestPrice, 75m);

        notification.Type.Should().Be(NotificationType.NewLowestPrice);
        notification.Message.Should().Contain("75");
        notification.Message.Should().Contain("Produto Y");
    }

    [Fact]
    public void MarkAsRead_ShouldSetIsReadTrue()
    {
        var notification = Notification.Create("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        notification.MarkAsRead();

        notification.IsRead.Should().BeTrue();
    }
}
