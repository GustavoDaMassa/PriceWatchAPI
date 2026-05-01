using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.ProductLists;

[Collection(IntegrationTestCollection.Name)]
public class ProductListFlowTests
{
    private readonly HttpClient _client;
    private readonly FakeEmailSender _fakeEmail;

    public ProductListFlowTests(PriceWatchWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _fakeEmail = factory.FakeEmail;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "List User",
            email,
            password = "Password123!"
        });
        var verifyToken = _fakeEmail.GetVerificationToken(email)!;
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = verifyToken });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task CreateList_WithValidToken_Returns201()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.PostAsJsonAsync("/api/lists", new
        {
            name = "My Test List",
            description = "smoke test"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetUserLists_WithValidToken_ReturnsCreatedList()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        await _client.PostAsJsonAsync("/api/lists", new { name = "Lista A", description = "" });

        var response = await _client.GetAsync("/api/lists");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateList_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/lists", new
        {
            name = "Unauthorized List"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateList_WithValidToken_Returns204()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var createResponse = await _client.PostAsJsonAsync("/api/lists", new
        {
            name = "Original Name"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listId = created.GetProperty("id").GetString()!;

        var response = await _client.PutAsJsonAsync($"/api/lists/{listId}", new
        {
            name = "Updated Name",
            description = "updated"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteList_WithValidToken_Returns204()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var createResponse = await _client.PostAsJsonAsync("/api/lists", new { name = "To Delete" });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listId = created.GetProperty("id").GetString()!;

        var response = await _client.DeleteAsync($"/api/lists/{listId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
