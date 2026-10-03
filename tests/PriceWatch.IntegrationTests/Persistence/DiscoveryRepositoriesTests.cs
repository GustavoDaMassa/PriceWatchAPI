using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.IntegrationTests.Fixtures;
using Xunit;

namespace PriceWatch.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public class DiscoveryRepositoriesTests
{
    private readonly PriceWatchWebApplicationFactory _factory;

    public DiscoveryRepositoriesTests(PriceWatchWebApplicationFactory factory) => _factory = factory;

    private static OfferSearchResult Found(string url) => new(url, "Produto", 100m, null, null, null, null);

    private static string NewProductId() => Guid.NewGuid().ToString();

    [Fact]
    public async Task Candidates_AreReturnedByScoreAndNeverWhenExpired()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfferCandidateRepository>();
        var productId = NewProductId();
        var low = OfferCandidate.Create(productId, ProductSource.Kabum, Found("https://k/1"), 0.5, CandidateReason.Keyword, TimeSpan.FromHours(1));
        var high = OfferCandidate.Create(productId, ProductSource.Kabum, Found("https://k/2"), 0.9, CandidateReason.Keyword, TimeSpan.FromHours(1));
        var expired = OfferCandidate.Create(productId, ProductSource.Kabum, Found("https://k/3"), 0.99, CandidateReason.Gtin, TimeSpan.FromMinutes(-5));
        await repo.SaveAsync(low);
        await repo.SaveAsync(high);
        await repo.SaveAsync(expired);

        var result = (await repo.GetByProductIdAsync(productId)).ToList();

        result.Select(c => c.Url).Should().Equal("https://k/2", "https://k/1");
    }

    [Fact]
    public async Task Save_IsAnUpsertByIdAndKeepsTheRefreshedValues()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfferCandidateRepository>();
        var productId = NewProductId();
        var candidate = OfferCandidate.Create(productId, ProductSource.Kabum, Found("https://k/1"), 0.5, CandidateReason.Keyword, TimeSpan.FromHours(1));
        await repo.SaveAsync(candidate);

        var loaded = (await repo.GetByKeyAsync(productId, ProductSource.Kabum, "https://k/1"))!;
        loaded.Refresh(Found("https://k/1") with { Price = 80m }, 0.8, CandidateReason.Gtin, TimeSpan.FromHours(2));
        await repo.SaveAsync(loaded);

        var reloaded = (await repo.GetByIdAsync(candidate.Id))!;
        reloaded.Price.Should().Be(80m);
        reloaded.Reason.Should().Be(CandidateReason.Gtin);
        (await repo.GetByProductIdAsync(productId)).Should().ContainSingle();
    }

    [Fact]
    public async Task TheSameOfferCannotBeStoredTwiceForTheSameProduct()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfferCandidateRepository>();
        var productId = NewProductId();
        await repo.SaveAsync(OfferCandidate.Create(productId, ProductSource.Kabum, Found("https://k/1"), 0.5, CandidateReason.Keyword, TimeSpan.FromHours(1)));

        var act = () => repo.SaveAsync(OfferCandidate.Create(productId, ProductSource.Kabum, Found("https://k/1"), 0.6, CandidateReason.Keyword, TimeSpan.FromHours(1)));

        await act.Should().ThrowAsync<MongoWriteException>();
    }

    [Fact]
    public async Task TheCandidatesCollectionHasATtlIndexOnExpiresAt()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var indexes = await (await database.GetCollection<BsonDocument>("offer_candidates").Indexes.ListAsync()).ToListAsync();

        var ttl = indexes.Single(i => i["name"] == "idx_offer_candidates_expiresAt_ttl");

        ttl["expireAfterSeconds"].ToInt32().Should().Be(0);
        ttl["key"].AsBsonDocument.Names.Should().Equal("expiresAt");
    }

    [Fact]
    public async Task Feedback_IsStoredPerUserAndCandidateAndCanBeRevised()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfferMatchFeedbackRepository>();
        var userId = Guid.NewGuid().ToString();
        var productId = NewProductId();
        var feedback = OfferMatchFeedback.Create(userId, "candidate-1", productId, "https://k/1", MatchVerdict.NotSame, false);
        await repo.SaveAsync(feedback);

        var loaded = (await repo.GetByUserAndCandidateAsync(userId, "candidate-1"))!;
        loaded.Revise(MatchVerdict.Same, track: true);
        await repo.SaveAsync(loaded);

        var all = (await repo.GetByUserAndProductAsync(userId, productId)).ToList();
        all.Should().ContainSingle();
        all[0].Id.Should().Be(feedback.Id);
        all[0].Verdict.Should().Be(MatchVerdict.Same);
        all[0].Track.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteByUserId_RemovesOnlyThatUsersFeedback()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfferMatchFeedbackRepository>();
        var alice = Guid.NewGuid().ToString();
        var bob = Guid.NewGuid().ToString();
        var productId = NewProductId();
        await repo.SaveAsync(OfferMatchFeedback.Create(alice, "c1", productId, "u", MatchVerdict.Same, true));
        await repo.SaveAsync(OfferMatchFeedback.Create(bob, "c1", productId, "u", MatchVerdict.NotSame, false));

        await repo.DeleteByUserIdAsync(alice);

        (await repo.GetByUserAndProductAsync(alice, productId)).Should().BeEmpty();
        (await repo.GetByUserAndProductAsync(bob, productId)).Should().ContainSingle();
    }
}
