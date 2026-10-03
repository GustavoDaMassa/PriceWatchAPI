using FluentAssertions;
using PriceWatch.Domain.Entities;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class DeviceTokenTests
{
    [Fact]
    public void Create_SetsTheOwnerTokenAndPlatform()
    {
        var device = DeviceToken.Create("user-1", "fcm-token-abc", "android");

        device.Id.Should().NotBeNullOrEmpty();
        device.UserId.Should().Be("user-1");
        device.Token.Should().Be("fcm-token-abc");
        device.Platform.Should().Be("android");
        device.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Touch_RefreshesLastSeen()
    {
        var device = DeviceToken.Restore("id", "user-1", "t", "android", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-30));

        device.Touch();

        device.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ReassignTo_MovesTheDeviceToAnotherUser()
    {
        var device = DeviceToken.Create("user-1", "t", "android");

        device.ReassignTo("user-2");

        device.UserId.Should().Be("user-2");
    }
}
