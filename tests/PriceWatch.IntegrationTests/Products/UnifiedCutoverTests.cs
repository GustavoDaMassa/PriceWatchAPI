using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Products;

/// <summary>
/// Simula o cutover real: um usuário com itens no modelo legado (TrackedProduct + histórico) passa a
/// ser atendido pelo catálogo unificado depois do backfill, mantendo ids, preços e histórico.
/// </summary>
[Collection(UnifiedIntegrationTestCollection.Name)]
public class UnifiedCutoverTests
{
    private readonly UnifiedPriceWatchWebApplicationFactory _factory;

    public UnifiedCutoverTests(UnifiedPriceWatchWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string UserId)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Cutover User", email, password = "Password123!" });
        var verifyToken = _factory.FakeEmail.GetVerificationToken(email)!;
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = verifyToken });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString()!);

        var me = await (await client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (client, me.GetProperty("id").GetString()!);
    }

    private async Task<TrackedProduct> SeedLegacyItemAsync(string userId, string url, decimal target, params decimal[] prices)
    {
        using var scope = _factory.Services.CreateScope();
        var tracked = scope.ServiceProvider.GetRequiredService<ITrackedProductRepository>();
        var snapshots = scope.ServiceProvider.GetRequiredService<IPriceSnapshotRepository>();

        var legacy = TrackedProduct.Create(userId, url, ProductSource.MercadoLivre, "Item Legado", target);
        foreach (var price in prices)
            await snapshots.CreateAsync(legacy.RecordPrice(price));
        await tracked.CreateAsync(legacy);
        return legacy;
    }

    private async Task<int> RunBackfillAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BackfillTrackedProductsUseCase>().ExecuteAsync();
    }

    [Fact]
    public async Task AfterTheBackfill_LegacyItemsKeepTheirIdsPricesAndHistory()
    {
        var (client, userId) = await NewUserAsync();
        var url = $"https://produto.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}";
        var legacy = await SeedLegacyItemAsync(userId, url, 150m, 120m, 80m, 130m);

        await RunBackfillAsync();

        var items = (await (await client.GetAsync("/api/products")).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().ToList();
        var item = items.Should().ContainSingle().Subject;
        item.GetProperty("id").GetString().Should().Be(legacy.Id);
        // clientes usam o productId para abrir a comparação e a descoberta de ofertas
        item.GetProperty("productId").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("url").GetString().Should().Be(url);
        item.GetProperty("name").GetString().Should().Be("Item Legado");
        item.GetProperty("targetPrice").GetDecimal().Should().Be(150m);
        item.GetProperty("currentPrice").GetDecimal().Should().Be(130m);
        item.GetProperty("lowestPrice").GetDecimal().Should().Be(80m);

        var history = (await (await client.GetAsync($"/api/products/{legacy.Id}/history")).Content
            .ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(h => h.GetProperty("price").GetDecimal()).ToList();
        history.Should().BeEquivalentTo(new[] { 120m, 80m, 130m });
    }

    [Fact]
    public async Task TheBackfillIsIdempotent()
    {
        var (client, userId) = await NewUserAsync();
        await SeedLegacyItemAsync(userId, $"https://produto.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}", 100m, 90m);

        var first = await RunBackfillAsync();
        var second = await RunBackfillAsync();

        first.Should().BeGreaterThanOrEqualTo(1);
        second.Should().Be(0);
        (await (await client.GetAsync("/api/products")).Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task EditsAfterTheCutover_AreAppliedToTheMigratedItem()
    {
        var (client, userId) = await NewUserAsync();
        var legacy = await SeedLegacyItemAsync(userId, $"https://produto.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}", 100m, 90m);
        await RunBackfillAsync();

        var update = await client.PutAsJsonAsync($"/api/products/{legacy.Id}", new { targetPrice = 70m, isActive = true });

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var item = (await (await client.GetAsync("/api/products")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single();
        item.GetProperty("targetPrice").GetDecimal().Should().Be(70m);
    }

    [Fact]
    public async Task TheOfferWorkerUseCase_ChecksTheMigratedOfferAndExtendsTheSameHistory()
    {
        var (client, userId) = await NewUserAsync();
        var legacy = await SeedLegacyItemAsync(userId, $"https://produto.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}", 100m, 120m);
        await RunBackfillAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var offers = scope.ServiceProvider.GetRequiredService<IStoreOfferRepository>();
            var offer = (await offers.GetByProductIdAsync(
                (await scope.ServiceProvider.GetRequiredService<IUserProductSubscriptionRepository>()
                    .GetByIdAsync(legacy.Id))!.ProductId)).Single();
            await scope.ServiceProvider.GetRequiredService<CheckOfferPriceUseCase>().ExecuteAsync(offer);
        }

        var history = (await (await client.GetAsync($"/api/products/{legacy.Id}/history")).Content
            .ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(h => h.GetProperty("price").GetDecimal()).ToList();
        history.Should().BeEquivalentTo(new[] { 120m, 199.90m });
    }

    [Fact]
    public async Task TwoUsersTrackingTheSameLegacyUrl_BothKeepTheirItemsAfterTheBackfill()
    {
        var (alice, aliceId) = await NewUserAsync();
        var (bob, bobId) = await NewUserAsync();
        var url = $"https://produto.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}";
        var aliceLegacy = await SeedLegacyItemAsync(aliceId, url, 100m, 90m);
        await Task.Delay(10);
        var bobLegacy = await SeedLegacyItemAsync(bobId, url, 150m, 90m);

        await RunBackfillAsync();

        var aliceItem = (await (await alice.GetAsync("/api/products")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single();
        var bobItem = (await (await bob.GetAsync("/api/products")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single();
        aliceItem.GetProperty("id").GetString().Should().Be(aliceLegacy.Id);
        bobItem.GetProperty("id").GetString().Should().Be(bobLegacy.Id);
        aliceItem.GetProperty("targetPrice").GetDecimal().Should().Be(100m);
        bobItem.GetProperty("targetPrice").GetDecimal().Should().Be(150m);

        // o histórico compartilhado (da oferta criada pelo item mais antigo) serve aos dois usuários
        var bobHistory = await bob.GetAsync($"/api/products/{bobLegacy.Id}/history");
        bobHistory.StatusCode.Should().Be(HttpStatusCode.OK);
        (await bobHistory.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().BeGreaterThan(0);
    }
}
