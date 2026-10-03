using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.API.Extensions;
using Xunit;

namespace PriceWatch.IntegrationTests.RateLimiting;

public class ProxyHeadersTests
{
    private static async Task<HttpClient> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProxyHeaders();
        var app = builder.Build();
        app.UseForwardedHeaders();
        app.MapGet("/ip", (HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "none");
        await app.StartAsync();
        return app.GetTestClient();
    }

    [Fact]
    public async Task ForwardedFor_ReplacesTheProxyAddressWithTheClientAddress()
    {
        var client = await StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/ip");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        var ip = await (await client.SendAsync(request)).Content.ReadAsStringAsync();

        ip.Should().Be("203.0.113.7");
    }

    [Fact]
    public async Task WithoutTheHeader_KeepsTheConnectionAddress()
    {
        var client = await StartAsync();

        var ip = await (await client.GetAsync("/ip")).Content.ReadAsStringAsync();

        ip.Should().NotBe("203.0.113.7");
    }
}
