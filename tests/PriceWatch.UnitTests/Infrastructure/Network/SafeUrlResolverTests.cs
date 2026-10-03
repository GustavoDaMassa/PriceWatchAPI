using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Network;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Network;

public class SafeUrlResolverTests
{
    private sealed class FakeHostResolver : IHostAddressResolver
    {
        private readonly Dictionary<string, IPAddress[]> _hosts;
        public FakeHostResolver(Dictionary<string, IPAddress[]> hosts) => _hosts = hosts;

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            if (IPAddress.TryParse(host, out var literal))
                return Task.FromResult(new[] { literal });
            return Task.FromResult(_hosts.TryGetValue(host, out var ips) ? ips : new[] { IPAddress.Parse("93.184.216.34") });
        }
    }

    private sealed class RouteHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> _routes;
        public List<string> Requested { get; } = new();
        public RouteHandler(Dictionary<string, HttpResponseMessage> routes) => _routes = routes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!.ToString());
            return Task.FromResult(_routes.TryGetValue(request.RequestUri.ToString(), out var response)
                ? response
                : new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static HttpResponseMessage Redirect(string location, HttpStatusCode status = HttpStatusCode.Found)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }

    private static SafeUrlResolver Build(
        Dictionary<string, HttpResponseMessage> routes,
        out RouteHandler handler,
        Dictionary<string, IPAddress[]>? hosts = null)
    {
        handler = new RouteHandler(routes);
        return new SafeUrlResolver(
            new HttpClient(handler),
            new FakeHostResolver(hosts ?? new()),
            NullLogger<SafeUrlResolver>.Instance);
    }

    [Fact]
    public async Task Resolve_WithoutRedirect_ReturnsTheSameUrl()
    {
        var resolver = Build(new(), out _);

        var result = await resolver.ResolveFinalUrlAsync("https://loja.com.br/produto/1");

        result.Should().Be("https://loja.com.br/produto/1");
    }

    [Fact]
    public async Task Resolve_FollowsAChainOfRedirectsToTheFinalUrl()
    {
        var resolver = Build(new()
        {
            ["https://curto.link/abc"] = Redirect("https://encurtador.com/x"),
            ["https://encurtador.com/x"] = Redirect("https://loja.com.br/produto/1?utm_source=zap", HttpStatusCode.MovedPermanently),
        }, out var handler);

        var result = await resolver.ResolveFinalUrlAsync("https://curto.link/abc");

        result.Should().Be("https://loja.com.br/produto/1?utm_source=zap");
        handler.Requested.Should().HaveCount(3);
    }

    [Fact]
    public async Task Resolve_HandlesRelativeLocationHeaders()
    {
        var resolver = Build(new()
        {
            ["https://loja.com.br/sec/abc"] = Redirect("/produto/1"),
        }, out _);

        var result = await resolver.ResolveFinalUrlAsync("https://loja.com.br/sec/abc");

        result.Should().Be("https://loja.com.br/produto/1");
    }

    [Fact]
    public async Task Resolve_StopsAfterTheMaximumNumberOfHops()
    {
        var routes = new Dictionary<string, HttpResponseMessage>();
        for (var i = 0; i < 20; i++)
            routes[$"https://loop.com/{i}"] = Redirect($"https://loop.com/{i + 1}");
        var resolver = Build(routes, out _);

        var act = () => resolver.ResolveFinalUrlAsync("https://loop.com/0");

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*redirect*");
    }

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://localhost/admin")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://192.168.0.244:8083/")]
    public async Task Resolve_RejectsPrivateAndLoopbackTargetsWithoutMakingAnyRequest(string url)
    {
        var resolver = Build(new(), out var handler, new() { ["localhost"] = [IPAddress.Loopback] });

        var act = () => resolver.ResolveFinalUrlAsync(url);

        await act.Should().ThrowAsync<BusinessException>();
        handler.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_RejectsHostnamesThatResolveToPrivateAddresses()
    {
        var resolver = Build(new(), out var handler,
            new() { ["interno.exemplo.com"] = [IPAddress.Parse("10.1.2.3")] });

        var act = () => resolver.ResolveFinalUrlAsync("https://interno.exemplo.com/x");

        await act.Should().ThrowAsync<BusinessException>();
        handler.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_RejectsARedirectThatPointsToAPrivateAddress()
    {
        var resolver = Build(new()
        {
            ["https://curto.link/abc"] = Redirect("http://169.254.169.254/latest/meta-data/"),
        }, out var handler);

        var act = () => resolver.ResolveFinalUrlAsync("https://curto.link/abc");

        await act.Should().ThrowAsync<BusinessException>();
        handler.Requested.Should().ContainSingle();
    }

    [Theory]
    [InlineData("ftp://loja.com/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://loja.com.br:8443/produto/1")]
    [InlineData("http://loja.com.br:22/")]
    public async Task Resolve_RejectsNonHttpSchemesAndUnusualPorts(string url)
    {
        var resolver = Build(new(), out _);

        var act = () => resolver.ResolveFinalUrlAsync(url);

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Resolve_WhenTheStoreIsUnreachable_ReturnsTheLastKnownUrlInsteadOfFailing()
    {
        var handler = new ThrowingHandler();
        var resolver = new SafeUrlResolver(new HttpClient(handler), new FakeHostResolver(new()), NullLogger<SafeUrlResolver>.Instance);

        var result = await resolver.ResolveFinalUrlAsync("https://loja.com.br/produto/1");

        result.Should().Be("https://loja.com.br/produto/1");
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("network down");
    }
}
