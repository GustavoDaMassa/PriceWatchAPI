using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Auth;

[Collection(IntegrationTestCollection.Name)]
public class AuthFlowTests
{
    private readonly HttpClient _client;
    private readonly FakeEmailSender _fakeEmail;

    public AuthFlowTests(PriceWatchWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _fakeEmail = factory.FakeEmail;
    }

    [Fact]
    public async Task Register_WithValidData_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Test User",
            email = $"{Guid.NewGuid()}@test.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400WithErrorResponse()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User One",
            email,
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User Two",
            email,
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("Bad Request");
    }

    [Fact]
    public async Task Login_BeforeEmailVerification_Returns400()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User",
            email,
            password = "Password123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_Returns200()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User",
            email,
            password = "Password123!"
        });

        var token = _fakeEmail.GetVerificationToken(email);
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", new
        {
            email,
            token
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_AfterEmailVerification_Returns200WithJwt()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User",
            email,
            password = "Password123!"
        });

        var token = _fakeEmail.GetVerificationToken(email);
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns400()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "User",
            email,
            password = "CorrectPassword!"
        });
        var token = _fakeEmail.GetVerificationToken(email);
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
