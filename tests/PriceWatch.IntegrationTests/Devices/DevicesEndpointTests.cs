using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Devices;

[Collection(IntegrationTestCollection.Name)]
public class DevicesEndpointTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public DevicesEndpointTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    private static string NewToken() => "fcm-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private async Task<(HttpClient Client, string UserId)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Device User", email, password = "Password123!" });
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = _factory.FakeEmail.GetVerificationToken(email)! });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString()!);
        var me = await (await client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (client, me.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Register_CreatesTheDeviceThenIsIdempotent()
    {
        var (client, _) = await NewUserAsync();
        var token = NewToken();

        var first = await client.PostAsJsonAsync("/api/devices", new { token, platform = "android" });
        var second = await client.PostAsJsonAsync("/api/devices", new { token, platform = "android" });

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        secondBody.GetProperty("id").GetString().Should().Be(firstBody.GetProperty("id").GetString());
        firstBody.GetProperty("platform").GetString().Should().Be("android");
    }

    [Fact]
    public async Task Register_ATokenFromAnotherAccount_MovesToTheCurrentUser()
    {
        var (alice, aliceId) = await NewUserAsync();
        var (bob, bobId) = await NewUserAsync();
        var token = NewToken();
        await alice.PostAsJsonAsync("/api/devices", new { token, platform = "android" });

        await bob.PostAsJsonAsync("/api/devices", new { token, platform = "android" });

        using var scope = _factory.Services.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceTokenRepository>();
        (await devices.GetByUserIdAsync(aliceId)).Should().BeEmpty();
        (await devices.GetByUserIdAsync(bobId)).Should().ContainSingle().Which.Token.Should().Be(token);
    }

    [Theory]
    [InlineData("curto", "android")]
    [InlineData("fcm-token-valido-com-tamanho-suficiente-123456", "windows")]
    public async Task Register_WithInvalidData_Returns400(string token, string platform)
    {
        var (client, _) = await NewUserAsync();

        var response = await client.PostAsJsonAsync("/api/devices", new { token, platform });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unregister_RemovesTheDeviceAndOthersCannotRemoveIt()
    {
        var (owner, _) = await NewUserAsync();
        var (intruder, _) = await NewUserAsync();
        var created = await (await owner.PostAsJsonAsync("/api/devices", new { token = NewToken(), platform = "ios" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();

        (await intruder.DeleteAsync($"/api/devices/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.DeleteAsync($"/api/devices/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/devices/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Devices_WithoutToken_Return401()
    {
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/devices", new { token = NewToken(), platform = "android" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync("/api/devices/x")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesTheUsersDevices()
    {
        var (client, userId) = await NewUserAsync();
        await client.PostAsJsonAsync("/api/devices", new { token = NewToken(), platform = "android" });

        var deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new { password = "Password123!" })
        });

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IDeviceTokenRepository>().GetByUserIdAsync(userId)).Should().BeEmpty();
    }
}
