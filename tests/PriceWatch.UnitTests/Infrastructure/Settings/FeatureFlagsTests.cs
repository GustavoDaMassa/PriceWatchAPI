using FluentAssertions;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.UnitTests.Infrastructure.Settings;

public class FeatureFlagsTests
{
    [Fact]
    public void Defaults_AllNewBehaviorsAreDisabled()
    {
        var flags = new FeatureFlags();

        flags.BackfillEnabled.Should().BeFalse();
        flags.MatchingEnabled.Should().BeFalse();
    }

    [Fact]
    public void UnifiedCatalogEnabled_DefaultsToFalse()
    {
        new FeatureFlags().UnifiedCatalogEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void BackfillActive_IsOnWhenBackfillOrUnifiedCatalogIsEnabled(bool backfill, bool unified, bool expected)
    {
        var flags = new FeatureFlags { BackfillEnabled = backfill, UnifiedCatalogEnabled = unified };

        flags.BackfillActive.Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void MatchingActive_IsOnWhenMatchingOrUnifiedCatalogIsEnabled(bool matching, bool unified, bool expected)
    {
        var flags = new FeatureFlags { MatchingEnabled = matching, UnifiedCatalogEnabled = unified };

        flags.MatchingActive.Should().Be(expected);
    }
}
