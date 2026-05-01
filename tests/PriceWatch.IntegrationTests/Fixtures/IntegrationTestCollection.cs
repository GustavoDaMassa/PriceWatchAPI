using Xunit;

namespace PriceWatch.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<PriceWatchWebApplicationFactory>
{
    public const string Name = "Integration";
}
