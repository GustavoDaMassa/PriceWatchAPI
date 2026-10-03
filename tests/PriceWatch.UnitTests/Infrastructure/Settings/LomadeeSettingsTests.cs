using FluentAssertions;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.UnitTests.Infrastructure.Settings;

public class LomadeeSettingsTests
{
    [Theory]
    [InlineData("token", "source", true)]
    [InlineData(null, "source", false)]
    [InlineData("token", null, false)]
    [InlineData("", "source", false)]
    [InlineData("token", "  ", false)]
    [InlineData(null, null, false)]
    public void IsConfigured_RequiresBothAppTokenAndSourceId(string? appToken, string? sourceId, bool expected)
    {
        var settings = new LomadeeSettings { AppToken = appToken, SourceId = sourceId };

        settings.IsConfigured.Should().Be(expected);
    }
}
