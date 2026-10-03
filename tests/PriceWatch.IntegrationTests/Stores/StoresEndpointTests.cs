using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Stores;

[Collection(IntegrationTestCollection.Name)]
public class StoresEndpointTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public StoresEndpointTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetStores_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/stores");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStores_ListsStoresAndTheirCapabilities()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Stores User", email, password = "Password123!" });
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = _factory.FakeEmail.GetVerificationToken(email)! });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString()!);

        var response = await client.GetAsync("/api/stores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stores = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var mercadoLivre = stores.Single(s => s.GetProperty("source").GetInt32() == 0);
        mercadoLivre.GetProperty("name").GetString().Should().Be("Mercado Livre");
        mercadoLivre.GetProperty("homeUrl").GetString().Should().Be("https://www.mercadolivre.com.br");
        mercadoLivre.GetProperty("canFetchByUrl").GetBoolean().Should().BeTrue();
        // sem credenciais da Lomadee no ambiente de teste a Kabum não está disponível
        var kabum = stores.Single(s => s.GetProperty("source").GetInt32() == 1);
        kabum.GetProperty("canGenerateAffiliateLink").GetBoolean().Should().BeFalse();
    }
}
