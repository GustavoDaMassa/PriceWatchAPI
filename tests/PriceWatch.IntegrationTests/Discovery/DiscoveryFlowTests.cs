using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Discovery;

[Collection(IntegrationTestCollection.Name)]
public class DiscoveryFlowTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public DiscoveryFlowTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    private static string NewGtin() => "789" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
    private static string NewMercadoLivreUrl() => $"https://www.mercadolivre.com.br/p/MLB{Guid.NewGuid():N}";
    private static string NewKabumUrl() => $"https://www.kabum.com.br/produto/{Random.Shared.NextInt64(100000, 999999)}/produto-teste";

    private async Task<(HttpClient Client, string UserId)> NewUserAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register", new { name = "Discovery User", email, password = "Password123!" });
        await client.PostAsJsonAsync("/api/auth/verify-email", new { email, token = _factory.FakeEmail.GetVerificationToken(email)! });
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("token").GetString()!);
        var me = await (await client.GetAsync("/api/users/me")).Content.ReadFromJsonAsync<JsonElement>();
        return (client, me.GetProperty("id").GetString()!);
    }

    private async Task<string> TrackAsync(HttpClient client, string url, string gtin)
    {
        _factory.FakeFetcher.SetSharedGtin(url, gtin);
        var response = await client.PostAsJsonAsync("/api/offers", new { url, targetPrice = 100m });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("productId").GetString()!;
    }

    private static async Task<List<JsonElement>> DiscoverAsync(HttpClient client, string productId)
    {
        var response = await client.PostAsync($"/api/products/{productId}/discover", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
    }

    private static async Task<int> OffersInComparisonAsync(HttpClient client, string productId) =>
        (await (await client.GetAsync($"/api/products/{productId}/comparison")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("offers").GetArrayLength();

    private static OfferSearchResult KabumResult(string url, string? gtin = null) =>
        new(url, "Produto de Teste MarcaTeste ModeloTeste", 150m, "https://img/k.png", gtin, "MarcaTeste", "ModeloTeste");

    [Fact]
    public async Task AGtinMatch_IsFoundPreselectedAndOnceConfirmedAppearsInTheComparison()
    {
        var (client, _) = await NewUserAsync();
        var gtin = NewGtin();
        var kabumUrl = NewKabumUrl();
        var productId = await TrackAsync(client, NewMercadoLivreUrl(), gtin);
        _factory.FakeSearcher.SetResultsForGtin(gtin, KabumResult(kabumUrl, gtin));

        var candidates = await DiscoverAsync(client, productId);

        var candidate = candidates.Should().ContainSingle().Subject;
        candidate.GetProperty("url").GetString().Should().Be(kabumUrl);
        candidate.GetProperty("storeName").GetString().Should().Be("KaBuM!");
        candidate.GetProperty("reason").GetInt32().Should().Be(0);          // Gtin
        candidate.GetProperty("preselected").GetBoolean().Should().BeTrue();
        // candidata ainda NÃO é uma oferta acompanhada (RI08)
        (await OffersInComparisonAsync(client, productId)).Should().Be(1);

        var feedback = await client.PostAsJsonAsync(
            $"/api/candidates/{candidate.GetProperty("id").GetString()}/feedback",
            new { isSameProduct = true, track = true, targetPrice = 120m });

        feedback.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await feedback.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("outcome").GetInt32().Should().Be(2);            // Attached
        result.GetProperty("offerId").GetString().Should().NotBeNullOrEmpty();
        (await OffersInComparisonAsync(client, productId)).Should().Be(2);

        var after = await (await client.GetAsync($"/api/products/{productId}/candidates")).Content.ReadFromJsonAsync<JsonElement>();
        after.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task DiscoveringTwiceInARow_CallsTheStoreOnlyOnce()
    {
        var (client, _) = await NewUserAsync();
        var gtin = NewGtin();
        var productId = await TrackAsync(client, NewMercadoLivreUrl(), gtin);
        _factory.FakeSearcher.SetResultsForGtin(gtin, KabumResult(NewKabumUrl(), gtin));
        var before = _factory.FakeSearcher.GtinCalls;

        var first = await DiscoverAsync(client, productId);
        var second = await DiscoverAsync(client, productId);

        (_factory.FakeSearcher.GtinCalls - before).Should().Be(1);
        second.Select(c => c.GetProperty("id").GetString()).Should().Equal(first.Select(c => c.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task ANotTheSameVerdict_HidesTheCandidateAndAttachesNothing()
    {
        var (client, _) = await NewUserAsync();
        var gtin = NewGtin();
        var productId = await TrackAsync(client, NewMercadoLivreUrl(), gtin);
        _factory.FakeSearcher.SetResultsForGtin(gtin, KabumResult(NewKabumUrl(), gtin));
        var candidate = (await DiscoverAsync(client, productId)).Single();

        var feedback = await client.PostAsJsonAsync(
            $"/api/candidates/{candidate.GetProperty("id").GetString()}/feedback", new { isSameProduct = false, track = true });

        (await feedback.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome").GetInt32().Should().Be(0); // Rejected
        (await OffersInComparisonAsync(client, productId)).Should().Be(1);
        (await (await client.GetAsync($"/api/products/{productId}/candidates")).Content.ReadFromJsonAsync<JsonElement>())
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AKeywordMatchOnASharedProduct_StaysPendingUntilASecondUserConfirms()
    {
        var (alice, _) = await NewUserAsync();
        var (bob, _) = await NewUserAsync();
        var url = NewMercadoLivreUrl();
        var gtin = NewGtin();
        var productId = await TrackAsync(alice, url, gtin);
        await TrackAsync(bob, url, gtin);                                   // mesmo produto, dois assinantes
        var kabumUrl = NewKabumUrl();
        // a loja não informa GTIN: só o pontuador por título/marca/modelo aprova
        _factory.FakeSearcher.KeywordHandler = _ => [KabumResult(kabumUrl)];
        try
        {
            var aliceCandidates = await DiscoverAsync(alice, productId);
            var candidate = aliceCandidates.Should().ContainSingle().Subject;
            candidate.GetProperty("reason").GetInt32().Should().Be(1);      // Keyword
            candidate.GetProperty("preselected").GetBoolean().Should().BeFalse();
            var candidateId = candidate.GetProperty("id").GetString();

            var first = await (await alice.PostAsJsonAsync($"/api/candidates/{candidateId}/feedback", new { isSameProduct = true, track = true }))
                .Content.ReadFromJsonAsync<JsonElement>();
            first.GetProperty("outcome").GetInt32().Should().Be(3);         // Pending
            (await OffersInComparisonAsync(alice, productId)).Should().Be(1);

            var second = await (await bob.PostAsJsonAsync($"/api/candidates/{candidateId}/feedback", new { isSameProduct = true, track = true }))
                .Content.ReadFromJsonAsync<JsonElement>();
            second.GetProperty("outcome").GetInt32().Should().Be(2);        // Attached (quórum)
            (await OffersInComparisonAsync(alice, productId)).Should().Be(2);
        }
        finally
        {
            _factory.FakeSearcher.KeywordHandler = _ => [];
        }
    }

    [Fact]
    public async Task AKeywordMatchForTheSoleSubscriber_AttachesImmediately()
    {
        var (client, _) = await NewUserAsync();
        var productId = await TrackAsync(client, NewMercadoLivreUrl(), NewGtin());
        _factory.FakeSearcher.KeywordHandler = _ => [KabumResult(NewKabumUrl())];
        try
        {
            var candidate = (await DiscoverAsync(client, productId)).Single();

            var result = await (await client.PostAsJsonAsync(
                $"/api/candidates/{candidate.GetProperty("id").GetString()}/feedback", new { isSameProduct = true, track = true }))
                .Content.ReadFromJsonAsync<JsonElement>();

            result.GetProperty("outcome").GetInt32().Should().Be(2);        // Attached
        }
        finally
        {
            _factory.FakeSearcher.KeywordHandler = _ => [];
        }
    }

    [Fact]
    public async Task UsersWhoDoNotTrackTheProduct_CannotDiscoverListOrJudgeItsCandidates()
    {
        var (owner, _) = await NewUserAsync();
        var (outsider, _) = await NewUserAsync();
        var gtin = NewGtin();
        var productId = await TrackAsync(owner, NewMercadoLivreUrl(), gtin);
        _factory.FakeSearcher.SetResultsForGtin(gtin, KabumResult(NewKabumUrl(), gtin));
        var candidate = (await DiscoverAsync(owner, productId)).Single();

        (await outsider.PostAsync($"/api/products/{productId}/discover", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync($"/api/products/{productId}/candidates")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.PostAsJsonAsync($"/api/candidates/{candidate.GetProperty("id").GetString()}/feedback",
            new { isSameProduct = true, track = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await OffersInComparisonAsync(owner, productId)).Should().Be(1);
    }

    [Fact]
    public async Task Discovery_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        (await client.PostAsync("/api/products/x/discover", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/products/x/candidates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/candidates/x/feedback", new { isSameProduct = true, track = true }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheStoresEndpoint_ReportsThatKabumCanBeSearched()
    {
        var (client, _) = await NewUserAsync();

        var stores = (await (await client.GetAsync("/api/stores")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        stores.Single(s => s.GetProperty("source").GetInt32() == 1).GetProperty("canSearch").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesTheUsersVerdicts()
    {
        var (client, userId) = await NewUserAsync();
        var gtin = NewGtin();
        var productId = await TrackAsync(client, NewMercadoLivreUrl(), gtin);
        _factory.FakeSearcher.SetResultsForGtin(gtin, KabumResult(NewKabumUrl(), gtin));
        var candidate = (await DiscoverAsync(client, productId)).Single();
        await client.PostAsJsonAsync($"/api/candidates/{candidate.GetProperty("id").GetString()}/feedback",
            new { isSameProduct = true, track = false });

        var deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new { password = "Password123!" })
        });

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var feedback = scope.ServiceProvider.GetRequiredService<IOfferMatchFeedbackRepository>();
        (await feedback.GetByUserAndProductAsync(userId, productId)).Should().BeEmpty();
    }
}
