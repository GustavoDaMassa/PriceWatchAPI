using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Discovery;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Discovery;

public class GetOfferCandidatesUseCaseTests
{
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IOfferCandidateRepository> _candidates = new();
    private readonly Mock<IOfferMatchFeedbackRepository> _feedback = new();
    private readonly GetOfferCandidatesUseCase _useCase;
    private const string UserId = "user-1";
    private const string ProductId = "product-1";

    public GetOfferCandidatesUseCaseTests()
    {
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, ProductId))
            .ReturnsAsync(UserProductSubscription.Create(UserId, ProductId, 100m));
        _feedback.Setup(r => r.GetByUserAndProductAsync(UserId, ProductId)).ReturnsAsync(Array.Empty<OfferMatchFeedback>());
        _useCase = new GetOfferCandidatesUseCase(_subscriptions.Object, _candidates.Object, _feedback.Object);
    }

    private static OfferCandidate Candidate(string url, CandidateReason reason, double score = 0.8) =>
        OfferCandidate.Create(ProductId, ProductSource.Kabum, new OfferSearchResult(url, "Produto " + url, 99m), score, reason, TimeSpan.FromHours(1));

    [Fact]
    public async Task Execute_WhenTheUserDoesNotTrackTheProduct_ThrowsNotFound()
    {
        var act = () => _useCase.ExecuteAsync("outsider", ProductId);

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task Execute_MapsCandidatesPreselectingOnlyTheGtinMatches()
    {
        var byGtin = Candidate("https://kabum/p/1", CandidateReason.Gtin, 1d);
        var byKeyword = Candidate("https://kabum/p/2", CandidateReason.Keyword, 0.7);
        _candidates.Setup(r => r.GetByProductIdAsync(ProductId)).ReturnsAsync(new[] { byGtin, byKeyword });

        var result = await _useCase.ExecuteAsync(UserId, ProductId);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(byGtin.Id);
        result[0].StoreName.Should().Be("KaBuM!");
        result[0].Preselected.Should().BeTrue();
        result[1].Preselected.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_HidesCandidatesTheUserAlreadyJudged()
    {
        var judged = Candidate("https://kabum/p/1", CandidateReason.Keyword);
        var pending = Candidate("https://kabum/p/2", CandidateReason.Keyword);
        _candidates.Setup(r => r.GetByProductIdAsync(ProductId)).ReturnsAsync(new[] { judged, pending });
        _feedback.Setup(r => r.GetByUserAndProductAsync(UserId, ProductId)).ReturnsAsync(new[]
        {
            OfferMatchFeedback.Create(UserId, judged.Id, ProductId, judged.Url, MatchVerdict.NotSame, false)
        });

        var result = await _useCase.ExecuteAsync(UserId, ProductId);

        result.Should().ContainSingle().Which.Id.Should().Be(pending.Id);
    }

    [Fact]
    public async Task Execute_WithNoCandidates_ReturnsAnEmptyList()
    {
        _candidates.Setup(r => r.GetByProductIdAsync(ProductId)).ReturnsAsync(Array.Empty<OfferCandidate>());

        (await _useCase.ExecuteAsync(UserId, ProductId)).Should().BeEmpty();
    }
}
