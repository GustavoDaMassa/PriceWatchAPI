using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Users;

[Collection(IntegrationTestCollection.Name)]
public class ExportDataEndpointTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public ExportDataEndpointTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string Email)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Export User", email, password = "Password123!" });
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = _factory.FakeEmail.GetVerificationToken(email)! });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString()!);
        return (client, email);
    }

    [Fact]
    public async Task Export_WithoutToken_IsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me/export");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Export_ReturnsOnlyTheCallersDataWithoutSecrets()
    {
        var (client, email) = await NewUserAsync();
        var (other, otherEmail) = await NewUserAsync();
        var pushToken = "fcm-" + Guid.NewGuid().ToString("N");
        await client.PostAsJsonAsync("/api/devices", new { token = pushToken, platform = "android" });
        await client.PostAsJsonAsync("/api/lists", new { name = "Minha lista" });
        await other.PostAsJsonAsync("/api/lists", new { name = "Lista alheia" });

        var response = await client.GetAsync("/api/users/me/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("profile").GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("lists").EnumerateArray().Select(l => l.GetProperty("name").GetString())
            .Should().BeEquivalentTo(new[] { "Minha lista" });
        body.GetProperty("devices").GetArrayLength().Should().Be(1);
        raw.Should().NotContain(pushToken).And.NotContain(otherEmail).And.NotContain("passwordHash");
    }
}
