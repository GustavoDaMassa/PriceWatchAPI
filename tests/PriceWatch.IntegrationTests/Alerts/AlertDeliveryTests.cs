using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Alerts;

[Collection(IntegrationTestCollection.Name)]
public class AlertDeliveryTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public AlertDeliveryTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    private static string NewToken() => "fcm-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private async Task<(HttpClient Client, string UserId, string Email)> NewUserAsync(string locale)
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Alert User", email, password = "Password123!", locale });
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = _factory.FakeEmail.GetVerificationToken(email)! });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString()!);
        var me = await (await client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (client, me.GetProperty("id").GetString()!, email);
    }

    private async Task ProcessAlertAsync(string userId, string productName, decimal price)
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProcessAlertUseCase>()
            .ExecuteAsync(userId, "item-1", productName, NotificationType.TargetPriceReached, price);
    }

    [Fact]
    public async Task AnAlert_CreatesTheNotificationAndPushesToEveryRegisteredDevice()
    {
        var (client, userId, _) = await NewUserAsync("pt-BR");
        var first = NewToken();
        var second = NewToken();
        await client.PostAsJsonAsync("/api/devices", new { token = first, platform = "android" });
        await client.PostAsJsonAsync("/api/devices", new { token = second, platform = "android" });

        await ProcessAlertAsync(userId, "Notebook Acer", 2599.9m);

        var pushes = _factory.FakePush.Sent.Where(s => s.Token == first || s.Token == second).ToList();
        pushes.Should().HaveCount(2);
        pushes.Should().OnlyContain(p => p.Message.Title == "Preço-alvo atingido" && p.Message.Body == "Notebook Acer: R$ 2.599,90");
        pushes.Should().OnlyContain(p => p.Message.Data!["productId"] == "item-1" && p.Message.Data["type"] == "TargetPriceReached");

        var notifications = (await (await client.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().ToList();
        notifications.Should().ContainSingle();
    }

    [Fact]
    public async Task AnInvalidToken_IsRemovedWhileTheGoodOneKeepsReceiving()
    {
        var (client, userId, _) = await NewUserAsync("en");
        var stale = NewToken();
        var good = NewToken();
        await client.PostAsJsonAsync("/api/devices", new { token = stale, platform = "android" });
        await client.PostAsJsonAsync("/api/devices", new { token = good, platform = "android" });
        _factory.FakePush.SetResult(stale, PushResult.InvalidToken);

        await ProcessAlertAsync(userId, "Mouse", 99m);

        using var scope = _factory.Services.CreateScope();
        var remaining = (await scope.ServiceProvider.GetRequiredService<IDeviceTokenRepository>().GetByUserIdAsync(userId)).ToList();
        remaining.Select(d => d.Token).Should().Equal(good);
    }

    [Fact]
    public async Task AUserWithoutDevices_StillGetsTheNotification()
    {
        var (client, userId, _) = await NewUserAsync("pt-BR");

        await ProcessAlertAsync(userId, "Mouse", 99m);

        (await (await client.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<JsonElement>())
            .GetArrayLength().Should().Be(1);
    }
}
