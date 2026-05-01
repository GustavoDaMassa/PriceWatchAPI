using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.ErrorHandling;

[Collection(IntegrationTestCollection.Name)]
public class ErrorHandlingTests
{
    private readonly HttpClient _client;
    private readonly FakeEmailSender _fakeEmail;

    public ErrorHandlingTests(PriceWatchWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _fakeEmail = factory.FakeEmail;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Error Test User",
            email,
            password = "Password123!"
        });
        var token = _fakeEmail.GetVerificationToken(email)!;
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token });
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task GetNonExistentList_Returns404WithErrorResponse()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.GetAsync("/api/lists/nonexistent-id/analysis");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("error").GetString().Should().Be("Not Found");
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AuthenticatedEndpoint_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/lists");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateNonExistentList_Returns404()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.PutAsJsonAsync("/api/lists/nonexistent-id", new
        {
            name = "Doesn't matter"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
