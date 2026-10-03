using System.Security.Claims;
using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PriceWatch.API.RateLimiting;
using Xunit;

namespace PriceWatch.IntegrationTests.RateLimiting;

public class RateLimitPartitionSelectorTests
{
    [Fact]
    public void AnonymousRequest_PartitionsByIpAndUsesAnonymousLimit()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.5");

        var partition = RateLimitPartitionSelector.Select(
            httpContext, anonymousPermitLimit: 5, authenticatedPermitLimit: 100, window: TimeSpan.FromMinutes(1));

        partition.PartitionKey.Should().Be("ip:203.0.113.5");
    }

    [Fact]
    public void AuthenticatedRequest_PartitionsByUserIdAndUsesAuthenticatedLimit()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-42")], "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var partition = RateLimitPartitionSelector.Select(
            httpContext, anonymousPermitLimit: 5, authenticatedPermitLimit: 100, window: TimeSpan.FromMinutes(1));

        partition.PartitionKey.Should().Be("user:user-42");
    }

    [Fact]
    public async Task AnonymousLimiter_RejectsRequestsAboveConfiguredPermitLimit()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.9");

        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartitionSelector.Select(ctx, anonymousPermitLimit: 2, authenticatedPermitLimit: 100, window: TimeSpan.FromMinutes(1)));

        using var first = await limiter.AcquireAsync(httpContext);
        using var second = await limiter.AcquireAsync(httpContext);
        using var third = await limiter.AcquireAsync(httpContext);

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
        third.IsAcquired.Should().BeFalse();
    }
}
