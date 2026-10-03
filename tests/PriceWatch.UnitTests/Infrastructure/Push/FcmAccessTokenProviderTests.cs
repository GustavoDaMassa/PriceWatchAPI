using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PriceWatch.Infrastructure.Push;
using PriceWatch.Infrastructure.Settings;
using Xunit;

namespace PriceWatch.UnitTests.Infrastructure.Push;

public class FcmAccessTokenProviderTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly List<HttpRequestMessage> _requests = new();
    private readonly List<string> _bodies = new();

    public void Dispose() => _rsa.Dispose();

    private string ServiceAccountJson() => JsonSerializer.Serialize(new
    {
        type = "service_account",
        client_email = "pricewatch@pw-project.iam.gserviceaccount.com",
        private_key = _rsa.ExportPkcs8PrivateKeyPem(),
        token_uri = "https://oauth2.example.com/token"
    });

    private FcmAccessTokenProvider Build(FcmSettings settings, int expiresIn = 3600)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                _requests.Add(req);
                _bodies.Add(await req.Content!.ReadAsStringAsync());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"access_token":"ya29.token-{{_requests.Count}}","expires_in":{{expiresIn}}}""", Encoding.UTF8, "application/json")
                };
            });

        return new FcmAccessTokenProvider(new HttpClient(handler.Object), Options.Create(settings), NullLogger<FcmAccessTokenProvider>.Instance);
    }

    private FcmSettings Configured() => new() { ProjectId = "pw-project", ServiceAccountJson = ServiceAccountJson() };

    private static byte[] Base64UrlDecode(string value) =>
        Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));

    [Fact]
    public async Task GetToken_ExchangesASignedJwtForAnAccessToken()
    {
        var provider = Build(Configured());

        var token = await provider.GetTokenAsync();

        token.Should().Be("ya29.token-1");
        _requests.Single().RequestUri!.ToString().Should().Be("https://oauth2.example.com/token");
        var form = System.Web.HttpUtility.ParseQueryString(_bodies.Single());
        form["grant_type"].Should().Be("urn:ietf:params:oauth:grant-type:jwt-bearer");
        form["assertion"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetToken_TheJwtHasTheRightClaimsAndAValidRs256Signature()
    {
        var provider = Build(Configured());
        await provider.GetTokenAsync();

        var assertion = System.Web.HttpUtility.ParseQueryString(_bodies.Single())["assertion"]!;
        var parts = assertion.Split('.');
        parts.Should().HaveCount(3);

        var header = JsonDocument.Parse(Base64UrlDecode(parts[0])).RootElement;
        header.GetProperty("alg").GetString().Should().Be("RS256");
        header.GetProperty("typ").GetString().Should().Be("JWT");

        var claims = JsonDocument.Parse(Base64UrlDecode(parts[1])).RootElement;
        claims.GetProperty("iss").GetString().Should().Be("pricewatch@pw-project.iam.gserviceaccount.com");
        claims.GetProperty("scope").GetString().Should().Be("https://www.googleapis.com/auth/firebase.messaging");
        claims.GetProperty("aud").GetString().Should().Be("https://oauth2.example.com/token");
        (claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64()).Should().Be(3600);

        var signedData = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        _rsa.VerifyData(signedData, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .Should().BeTrue();
    }

    [Fact]
    public async Task GetToken_ReusesTheCachedTokenUntilItIsNearExpiry()
    {
        var provider = Build(Configured());

        var first = await provider.GetTokenAsync();
        var second = await provider.GetTokenAsync();

        second.Should().Be(first);
        _requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetToken_RefreshesATokenThatIsAboutToExpire()
    {
        var provider = Build(Configured(), expiresIn: 60); // dentro da margem de renovação (5 min)

        var first = await provider.GetTokenAsync();
        var second = await provider.GetTokenAsync();

        second.Should().NotBe(first);
        _requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetToken_AcceptsTheServiceAccountAsBase64()
    {
        var settings = new FcmSettings
        {
            ProjectId = "pw-project",
            ServiceAccountJsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ServiceAccountJson()))
        };

        var token = await Build(settings).GetTokenAsync();

        token.Should().Be("ya29.token-1");
    }

    [Fact]
    public async Task GetToken_WithoutCredentials_Throws()
    {
        var provider = Build(new FcmSettings());

        var act = () => provider.GetTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
