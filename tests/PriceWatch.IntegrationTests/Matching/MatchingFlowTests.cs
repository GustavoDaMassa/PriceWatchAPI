using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Matching;

[Collection(IntegrationTestCollection.Name)]
public class MatchingFlowTests
{
    private readonly PriceWatchWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly FakeEmailSender _fakeEmail;

    public MatchingFlowTests(PriceWatchWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _fakeEmail = factory.FakeEmail;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var email = $"{Guid.NewGuid()}@test.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { name = "Matching User", email, password = "Password123!" });
        var verifyToken = _fakeEmail.GetVerificationToken(email)!;
        await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = verifyToken });
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task MatchStoreOfferUseCase_WhenTwoOffersShareGtin_MergesThemUnderSameProduct()
    {
        var jwt = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        // por padrão o FakePriceFetcher deriva um GTIN único por URL (evita colidir com
        // outros testes da suíte, que compartilham o mesmo Mongo) — aqui forçamos as duas
        // URLs a compartilhar o mesmo GTIN, simulando o mesmo produto físico em duas lojas
        var sharedGtin = $"GTIN-SHARED-{Guid.NewGuid():N}";
        var firstUrl = $"https://produto.mercadolivre.com.br/MLB-{Guid.NewGuid():N}";
        var secondUrl = $"https://produto.mercadolivre.com.br/MLB-{Guid.NewGuid():N}";
        _factory.FakeFetcher.SetSharedGtin(firstUrl, sharedGtin);
        _factory.FakeFetcher.SetSharedGtin(secondUrl, sharedGtin);

        var firstResponse = await _client.PostAsJsonAsync("/api/offers", new { url = firstUrl, targetPrice = 150m });
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstProductId = firstBody.GetProperty("productId").GetString()!;

        var secondResponse = await _client.PostAsJsonAsync("/api/offers", new { url = secondUrl, targetPrice = 130m });
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondProductId = secondBody.GetProperty("productId").GetString()!;
        var secondOfferId = secondBody.GetProperty("offerId").GetString()!;

        secondProductId.Should().NotBe(firstProductId, "ainda não rodou o matching — cada oferta nasceu no próprio produto");

        // o consumer assíncrono está desligado nos testes — chama o use case direto,
        // como o ProductMatchingConsumer faria ao processar o evento da segunda oferta
        using (var scope = _factory.Services.CreateScope())
        {
            var matchUseCase = scope.ServiceProvider.GetRequiredService<MatchStoreOfferUseCase>();
            await matchUseCase.ExecuteAsync(secondOfferId, secondProductId);
        }

        using var verifyScope = _factory.Services.CreateScope();
        var offers = verifyScope.ServiceProvider.GetRequiredService<IStoreOfferRepository>();
        var products = verifyScope.ServiceProvider.GetRequiredService<IProductRepository>();
        var subscriptions = verifyScope.ServiceProvider.GetRequiredService<IUserProductSubscriptionRepository>();

        var secondOfferAfterMatch = await offers.GetByIdAsync(secondOfferId);
        secondOfferAfterMatch!.ProductId.Should().Be(firstProductId);

        var firstProductAfterMatch = await products.GetByIdAsync(firstProductId);
        firstProductAfterMatch!.MatchStatus.Should().Be(MatchStatus.AutoMatched);

        var offersUnderFirstProduct = await offers.GetByProductIdAsync(firstProductId);
        offersUnderFirstProduct.Should().HaveCount(2);

        // mesmo usuário adicionou as duas ofertas — a segunda subscrição vira duplicata e é removida
        var subscriptionsForSecondProduct = await subscriptions.GetByProductIdAsync(secondProductId);
        subscriptionsForSecondProduct.Should().BeEmpty();

        var comparisonResponse = await _client.GetAsync($"/api/products/{firstProductId}/comparison");
        var comparisonBody = await comparisonResponse.Content.ReadFromJsonAsync<JsonElement>();
        comparisonBody.GetProperty("offers").GetArrayLength().Should().Be(2);
    }
}
