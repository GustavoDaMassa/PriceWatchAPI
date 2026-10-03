using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Offers;

[Collection(IntegrationTestCollection.Name)]
public class OfferFlowTests
{
    private readonly PriceWatchWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly FakeEmailSender _fakeEmail;

    public OfferFlowTests(PriceWatchWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _fakeEmail = factory.FakeEmail;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            name = "Offer User",
            email,
            password = "Password123!"
        });
        var verifyToken = _fakeEmail.GetVerificationToken(email)!;
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = verifyToken });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task CreateOffer_WithValidToken_Returns201WithFetchedData()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.PostAsJsonAsync("/api/offers", new
        {
            url = $"https://produto.mercadolivre.com.br/MLB-{Guid.NewGuid():N}",
            targetPrice = 150m
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be("Produto de Teste");
        body.GetProperty("currentPrice").GetDecimal().Should().Be(199.90m);
    }

    [Fact]
    public async Task CreateOffer_CalledTwiceForSameUrl_ReusesSameProductAndOffer()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        var url = $"https://produto.mercadolivre.com.br/MLB-{Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync("/api/offers", new { url, targetPrice = 150m });
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();

        var second = await _client.PostAsJsonAsync("/api/offers", new { url, targetPrice = 90m });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        secondBody.GetProperty("productId").GetString().Should().Be(firstBody.GetProperty("productId").GetString());
        secondBody.GetProperty("offerId").GetString().Should().Be(firstBody.GetProperty("offerId").GetString());
        secondBody.GetProperty("targetPrice").GetDecimal().Should().Be(90m);
    }

    [Fact]
    public async Task CreateOffer_WithoutToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/offers", new { url = "https://ml.com/item", targetPrice = 100m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetComparison_ForNonExistentProduct_Returns404()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.GetAsync("/api/products/nonexistent-id/comparison");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetComparison_WithMultipleOffers_HighlightsCheapestOne()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        using var scope = _factory.Services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var offers = scope.ServiceProvider.GetRequiredService<IStoreOfferRepository>();

        var product = Product.Create("Produto Comparado");
        await products.CreateAsync(product);

        var cheaper = StoreOffer.Create(product.Id, ProductSource.Kabum, $"https://kabum.com/{Guid.NewGuid():N}");
        cheaper.RecordPrice(100m);
        await offers.CreateAsync(cheaper);

        var pricier = StoreOffer.Create(product.Id, ProductSource.MercadoLivre, $"https://ml.com/{Guid.NewGuid():N}");
        pricier.RecordPrice(150m);
        await offers.CreateAsync(pricier);

        var response = await _client.GetAsync($"/api/products/{product.Id}/comparison");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var offersJson = body.GetProperty("offers").EnumerateArray().ToList();
        offersJson.Should().HaveCount(2);
        offersJson.Single(o => o.GetProperty("offerId").GetString() == cheaper.Id)
            .GetProperty("isCheapest").GetBoolean().Should().BeTrue();
        offersJson.Single(o => o.GetProperty("offerId").GetString() == pricier.Id)
            .GetProperty("isCheapest").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Redirect_ForExistingOffer_Returns302ToAffiliateUrl()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        using var scope = _factory.Services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var offers = scope.ServiceProvider.GetRequiredService<IStoreOfferRepository>();

        var product = Product.Create("Produto Redirect");
        await products.CreateAsync(product);
        var offer = StoreOffer.Create(product.Id, ProductSource.MercadoLivre, "https://ml.com/redirect-target");
        await offers.CreateAsync(offer);

        using var noRedirectClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        noRedirectClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await noRedirectClient.GetAsync($"/api/offers/{offer.Id}/redirect");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location!.ToString().Should().Be("https://ml.com/redirect-target");
    }

    [Fact]
    public async Task Redirect_ForNonExistentOffer_Returns404()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _client.GetAsync("/api/offers/nonexistent-id/redirect");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
