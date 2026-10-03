using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Products;

/// <summary>
/// Contrato dos endpoints legados (/api/products, /api/lists/{id}/analysis) que a SPA atual e a
/// extensão publicada (v1.1.0) consomem. Roda duas vezes: contra o modelo legado (TrackedProduct)
/// e contra o catálogo unificado (Product/StoreOffer/Subscription) — o mesmo comportamento observável
/// nos dois é o que garante que o cutover não quebra clientes existentes.
/// </summary>
public abstract class LegacyProductContractTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    protected LegacyProductContractTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    private static string NewUrl() => $"https://produto.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}";

    private async Task<HttpClient> NewAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Contract User", email, password = "Password123!" });
        var verifyToken = _factory.FakeEmail.GetVerificationToken(email)!;
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = verifyToken });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString()!);
        return client;
    }

    private static async Task<JsonElement> AddProductAsync(HttpClient client, string url, decimal targetPrice, string? listId = null)
    {
        var response = await client.PostAsJsonAsync("/api/products", new { url, targetPrice, listId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> CreateListAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/lists", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private static async Task<List<JsonElement>> GetProductsAsync(HttpClient client, string? listId = null)
    {
        var path = listId is null ? "/api/products" : $"/api/products?listId={listId}";
        var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.EnumerateArray().ToList();
    }

    [Fact]
    public async Task AddProduct_Returns201WithTheLegacyResponseShape()
    {
        var client = await NewAuthenticatedClientAsync();
        var url = NewUrl();

        var product = await AddProductAsync(client, url, 150m);

        product.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        product.GetProperty("name").GetString().Should().Be("Produto de Teste");
        product.GetProperty("url").GetString().Should().Be(url);
        product.GetProperty("imageUrl").GetString().Should().Be("https://example.com/image.png");
        product.GetProperty("source").GetInt32().Should().Be(0);
        product.GetProperty("targetPrice").GetDecimal().Should().Be(150m);
        product.GetProperty("currentPrice").GetDecimal().Should().Be(199.90m);
        product.GetProperty("lowestPrice").GetDecimal().Should().Be(199.90m);
        product.GetProperty("isActive").GetBoolean().Should().BeTrue();
        product.TryGetProperty("nextCheckAt", out _).Should().BeTrue();
        product.TryGetProperty("listId", out _).Should().BeTrue();
        // campo aditivo: nulo no modelo legado, preenchido no catálogo unificado (ver UnifiedCutoverTests)
        product.TryGetProperty("productId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task AddProduct_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products", new { url = NewUrl(), targetPrice = 100m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddProduct_CalledTwiceForTheSameUrl_ReturnsTheSameItem()
    {
        var client = await NewAuthenticatedClientAsync();
        var url = NewUrl();

        var first = await AddProductAsync(client, url, 100m);
        var second = await AddProductAsync(client, url, 100m);

        second.GetProperty("id").GetString().Should().Be(first.GetProperty("id").GetString());
        (await GetProductsAsync(client)).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProducts_ReturnsOnlyTheCallersItems()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();

        var aliceItem = await AddProductAsync(alice, NewUrl(), 100m);
        await AddProductAsync(bob, NewUrl(), 100m);

        var aliceItems = await GetProductsAsync(alice);
        aliceItems.Should().HaveCount(1);
        aliceItems[0].GetProperty("id").GetString().Should().Be(aliceItem.GetProperty("id").GetString());
    }

    [Fact]
    public async Task TwoUsersTrackingTheSameUrl_KeepIndependentItemsAndTargets()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();
        var url = NewUrl();

        var aliceItem = await AddProductAsync(alice, url, 100m);
        var bobItem = await AddProductAsync(bob, url, 150m);

        aliceItem.GetProperty("id").GetString().Should().NotBe(bobItem.GetProperty("id").GetString());
        (await GetProductsAsync(alice))[0].GetProperty("targetPrice").GetDecimal().Should().Be(100m);
        (await GetProductsAsync(bob))[0].GetProperty("targetPrice").GetDecimal().Should().Be(150m);
    }

    [Fact]
    public async Task UpdateProduct_ChangesTargetPriceAndActiveFlag()
    {
        var client = await NewAuthenticatedClientAsync();
        var item = await AddProductAsync(client, NewUrl(), 100m);
        var id = item.GetProperty("id").GetString()!;

        var response = await client.PutAsJsonAsync($"/api/products/{id}", new { targetPrice = 120m, isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var updated = (await GetProductsAsync(client)).Single(p => p.GetProperty("id").GetString() == id);
        updated.GetProperty("targetPrice").GetDecimal().Should().Be(120m);
        updated.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProduct_ReactivatingBringsTheItemBack()
    {
        var client = await NewAuthenticatedClientAsync();
        var id = (await AddProductAsync(client, NewUrl(), 100m)).GetProperty("id").GetString()!;
        await client.PutAsJsonAsync($"/api/products/{id}", new { targetPrice = 100m, isActive = false });

        await client.PutAsJsonAsync($"/api/products/{id}", new { targetPrice = 100m, isActive = true });

        var item = (await GetProductsAsync(client)).Single(p => p.GetProperty("id").GetString() == id);
        item.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateProduct_OfAnotherUser_Returns404()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();
        var id = (await AddProductAsync(alice, NewUrl(), 100m)).GetProperty("id").GetString()!;

        var response = await bob.PutAsJsonAsync($"/api/products/{id}", new { targetPrice = 1m, isActive = true });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveProduct_DeletesItAndAnotherDeleteReturns404()
    {
        var client = await NewAuthenticatedClientAsync();
        var id = (await AddProductAsync(client, NewUrl(), 100m)).GetProperty("id").GetString()!;

        var first = await client.DeleteAsync($"/api/products/{id}");
        var second = await client.DeleteAsync($"/api/products/{id}");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetProductsAsync(client)).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveProduct_OfAnotherUser_Returns404AndKeepsTheItem()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();
        var id = (await AddProductAsync(alice, NewUrl(), 100m)).GetProperty("id").GetString()!;

        var response = await bob.DeleteAsync($"/api/products/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await GetProductsAsync(alice)).Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveProduct_OfOneUser_DoesNotAffectAnotherUserTrackingTheSameUrl()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();
        var url = NewUrl();
        var aliceId = (await AddProductAsync(alice, url, 100m)).GetProperty("id").GetString()!;
        await AddProductAsync(bob, url, 100m);

        await alice.DeleteAsync($"/api/products/{aliceId}");

        (await GetProductsAsync(alice)).Should().BeEmpty();
        (await GetProductsAsync(bob)).Should().HaveCount(1);
    }

    [Fact]
    public async Task AssignToList_AddsAndRemovesTheItemFromTheList()
    {
        var client = await NewAuthenticatedClientAsync();
        var listId = await CreateListAsync(client, "Setup");
        var id = (await AddProductAsync(client, NewUrl(), 100m)).GetProperty("id").GetString()!;

        var assign = await client.PatchAsJsonAsync($"/api/products/{id}/list", new { listId });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetProductsAsync(client, listId)).Should().HaveCount(1);

        var remove = await client.PatchAsJsonAsync($"/api/products/{id}/list", new { listId = (string?)null });
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetProductsAsync(client, listId)).Should().BeEmpty();
        (await GetProductsAsync(client)).Should().HaveCount(1);
    }

    [Fact]
    public async Task AssignToList_WithUnknownList_Returns404()
    {
        var client = await NewAuthenticatedClientAsync();
        var id = (await AddProductAsync(client, NewUrl(), 100m)).GetProperty("id").GetString()!;

        var response = await client.PatchAsJsonAsync($"/api/products/{id}/list", new { listId = Guid.NewGuid().ToString() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddProduct_WithListId_PutsTheItemInTheList()
    {
        var client = await NewAuthenticatedClientAsync();
        var listId = await CreateListAsync(client, "Black Friday");

        var item = await AddProductAsync(client, NewUrl(), 100m, listId);

        item.GetProperty("listId").GetString().Should().Be(listId);
        (await GetProductsAsync(client, listId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task ClickProduct_ReturnsTheStoreUrlAndOnlyForTheOwner()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();
        var url = NewUrl();
        var id = (await AddProductAsync(alice, url, 100m)).GetProperty("id").GetString()!;

        var own = await alice.PostAsync($"/api/products/{id}/click", content: null);
        var other = await bob.PostAsync($"/api/products/{id}/click", content: null);

        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await own.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString().Should().Be(url);
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClickProduct_WithoutToken_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/products/x/click", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPriceHistory_ReturnsAnArrayForTheOwnerAnd404ForOthers()
    {
        var alice = await NewAuthenticatedClientAsync();
        var bob = await NewAuthenticatedClientAsync();
        var id = (await AddProductAsync(alice, NewUrl(), 100m)).GetProperty("id").GetString()!;

        var own = await alice.GetAsync($"/api/products/{id}/history");
        var other = await bob.GetAsync($"/api/products/{id}/history");

        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await own.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListAnalysis_ReturnsActiveItemsOrderedByDistanceToTarget()
    {
        var client = await NewAuthenticatedClientAsync();
        var listId = await CreateListAsync(client, "Analise");
        var near = (await AddProductAsync(client, NewUrl(), 190m, listId)).GetProperty("id").GetString()!;
        var far = (await AddProductAsync(client, NewUrl(), 100m, listId)).GetProperty("id").GetString()!;
        var inactive = (await AddProductAsync(client, NewUrl(), 150m, listId)).GetProperty("id").GetString()!;
        await client.PutAsJsonAsync($"/api/products/{inactive}", new { targetPrice = 150m, isActive = false });

        var response = await client.GetAsync($"/api/lists/{listId}/analysis");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        items.Select(i => i.GetProperty("productId").GetString()).Should().Equal(near, far);
        items[0].GetProperty("currentPrice").GetDecimal().Should().Be(199.90m);
        items[0].GetProperty("targetPrice").GetDecimal().Should().Be(190m);
        items[0].GetProperty("distancePercent").GetDecimal().Should().Be(5.21m);
    }

    [Fact]
    public async Task DeleteList_KeepsItsItemsButDetachesThem()
    {
        var client = await NewAuthenticatedClientAsync();
        var listId = await CreateListAsync(client, "Temporaria");
        await AddProductAsync(client, NewUrl(), 100m, listId);

        var response = await client.DeleteAsync($"/api/lists/{listId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var items = await GetProductsAsync(client);
        items.Should().HaveCount(1);
        items[0].GetProperty("listId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DeleteAccount_RemovesTheUsersItems()
    {
        var client = await NewAuthenticatedClientAsync();
        await AddProductAsync(client, NewUrl(), 100m);

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new { password = "Password123!" })
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterDelete = await client.GetAsync("/api/products");
        afterDelete.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        if (afterDelete.StatusCode == HttpStatusCode.OK)
            (await afterDelete.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }
}

[Collection(IntegrationTestCollection.Name)]
public class LegacyModeProductContractTests : LegacyProductContractTests
{
    public LegacyModeProductContractTests(PriceWatchWebApplicationFactory factory) : base(factory) { }
}

[Collection(UnifiedIntegrationTestCollection.Name)]
public class UnifiedModeProductContractTests : LegacyProductContractTests
{
    public UnifiedModeProductContractTests(UnifiedPriceWatchWebApplicationFactory factory) : base(factory) { }
}
