using System.Net;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Health;

[Collection(IntegrationTestCollection.Name)]
public class HealthEndpointTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public HealthEndpointTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Live_IsOkWithoutAuthentication()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ChecksMongoAndRedis()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }
}
