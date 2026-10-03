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
}
