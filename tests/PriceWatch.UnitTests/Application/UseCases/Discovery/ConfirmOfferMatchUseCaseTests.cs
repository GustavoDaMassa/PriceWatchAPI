using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Discovery;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Discovery;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Discovery;

public class ConfirmOfferMatchUseCaseTests
{
    private readonly Mock<IOfferCandidateRepository> _candidates = new();
    private readonly Mock<IOfferMatchFeedbackRepository> _feedback = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IAffiliateLinkResolver> _affiliateResolver = new();
    private readonly Mock<IAffiliateLinkGenerator> _affiliate = new();
    private readonly ConfirmOfferMatchUseCase _useCase;

    private const string UserId = "user-1";
    private readonly Product _product = Product.Create("Notebook Acer", gtin: "7891234567895");
    private readonly UserProductSubscription _subscription;

    public ConfirmOfferMatchUseCaseTests()
    {
        _subscription = UserProductSubscription.Create(UserId, _product.Id, 100m);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, _product.Id)).ReturnsAsync(_subscription);
        _subscriptions.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { _subscription });
        _products.Setup(r => r.GetByIdAsync(_product.Id)).ReturnsAsync(_product);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(It.IsAny<ProductSource>(), It.IsAny<string>())).ReturnsAsync((StoreOffer?)null);
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(Array.Empty<StoreOffer>());
        _feedback.Setup(r => r.GetByUserAndCandidateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((OfferMatchFeedback?)null);
        _feedback.Setup(r => r.CountTrackVotesAsync(It.IsAny<string>())).ReturnsAsync(1);
        _affiliateResolver.Setup(r => r.Resolve(It.IsAny<ProductSource>())).Returns(_affiliate.Object);

        _useCase = new ConfirmOfferMatchUseCase(
            _candidates.Object, _feedback.Object, _subscriptions.Object, _products.Object, _offers.Object,
            _affiliateResolver.Object, new SyncOfferActivityUseCase(_offers.Object, _subscriptions.Object));
    }

    private OfferCandidate Candidate(CandidateReason reason, string url = "https://kabum/p/1")
    {
        var candidate = OfferCandidate.Create(_product.Id, ProductSource.Kabum,
            new OfferSearchResult(url, "Notebook Acer", 2599.90m, null, "7891234567895"), 0.9, reason, TimeSpan.FromHours(1));
        _candidates.Setup(r => r.GetByIdAsync(candidate.Id)).ReturnsAsync(candidate);
        return candidate;
    }

    private void OtherActiveSubscribers(int count)
    {
        var all = new List<UserProductSubscription> { _subscription };
        for (var i = 0; i < count; i++)
            all.Add(UserProductSubscription.Create($"other-{i}", _product.Id, 50m));
        _subscriptions.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(all);
    }

    [Fact]
    public async Task Execute_WhenTheCandidateDoesNotExistOrIsExpired_ThrowsNotFound()
    {
        var expired = OfferCandidate.Create(_product.Id, ProductSource.Kabum, new OfferSearchResult("u", "n", 1m), 1, CandidateReason.Gtin, TimeSpan.FromSeconds(-1));
        _candidates.Setup(r => r.GetByIdAsync(expired.Id)).ReturnsAsync(expired);

        var missing = () => _useCase.ExecuteAsync(UserId, "nope", new ConfirmOfferMatchRequest(true, true));
        var stale = () => _useCase.ExecuteAsync(UserId, expired.Id, new ConfirmOfferMatchRequest(true, true));

        await missing.Should().ThrowAsync<OfferCandidateNotFoundException>();
        await stale.Should().ThrowAsync<OfferCandidateNotFoundException>();
    }

    [Fact]
    public async Task Execute_WhenTheUserDoesNotTrackTheProduct_ThrowsNotFoundWithoutRevealingIt()
    {
        var candidate = Candidate(CandidateReason.Gtin);

        var act = () => _useCase.ExecuteAsync("outsider", candidate.Id, new ConfirmOfferMatchRequest(true, true));

        await act.Should().ThrowAsync<OfferCandidateNotFoundException>();
        _feedback.Verify(r => r.SaveAsync(It.IsAny<OfferMatchFeedback>()), Times.Never);
    }

    [Fact]
    public async Task Execute_NotTheSameProduct_RecordsTheVerdictAndCreatesNoOffer()
    {
        var candidate = Candidate(CandidateReason.Keyword);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(false, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Rejected);
        _feedback.Verify(r => r.SaveAsync(It.Is<OfferMatchFeedback>(f => f.Verdict == MatchVerdict.NotSame && !f.Track)), Times.Once);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Never);
    }

    [Fact]
    public async Task Execute_TheSameProductWithoutTracking_OnlyRecordsTheVerdict()
    {
        var candidate = Candidate(CandidateReason.Gtin);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, false));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Recorded);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Never);
    }

    [Fact]
    public async Task Execute_AGtinMatch_AttachesTheOfferImmediatelyEvenWithOtherSubscribers()
    {
        OtherActiveSubscribers(3);
        var candidate = Candidate(CandidateReason.Gtin);
        _affiliate.Setup(a => a.IsAvailable).Returns(true);
        _affiliate.Setup(a => a.GenerateAsync(candidate.Url)).ReturnsAsync("https://afiliado/?u=abc");

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Attached);
        result.ProductId.Should().Be(_product.Id);
        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o =>
            o.ProductId == _product.Id && o.Source == ProductSource.Kabum && o.Url == candidate.Url &&
            o.CurrentPrice == 2599.90m && o.AffiliateUrl == "https://afiliado/?u=abc" && o.Gtin == "7891234567895")), Times.Once);
        _products.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.MatchStatus == MatchStatus.Confirmed)), Times.Once);
    }

    [Fact]
    public async Task Execute_AttachedOffer_IsCheckedRightAwayByTheWorker()
    {
        var candidate = Candidate(CandidateReason.Gtin);
        StoreOffer? created = null;
        _offers.Setup(r => r.CreateAsync(It.IsAny<StoreOffer>())).Callback<StoreOffer>(o => created = o).Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        created!.NextCheckAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Execute_AKeywordMatchForASoleSubscriber_AttachesImmediately()
    {
        var candidate = Candidate(CandidateReason.Keyword);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Attached);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Once);
    }

    [Fact]
    public async Task Execute_AKeywordMatchForASharedProduct_StaysPendingUntilAnotherUserConfirms()
    {
        OtherActiveSubscribers(2);
        var candidate = Candidate(CandidateReason.Keyword);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Pending);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Never);
        _feedback.Verify(r => r.SaveAsync(It.Is<OfferMatchFeedback>(f => f.Verdict == MatchVerdict.Same && f.Track)), Times.Once);
    }

    [Fact]
    public async Task Execute_TheSecondIndependentConfirmationOfAKeywordMatch_Attaches()
    {
        OtherActiveSubscribers(2);
        var candidate = Candidate(CandidateReason.Keyword);
        _feedback.Setup(r => r.CountTrackVotesAsync(candidate.Id)).ReturnsAsync(2);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Attached);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenTheOfferAlreadyBelongsToTheSameProduct_ReusesItWithoutDuplicating()
    {
        var candidate = Candidate(CandidateReason.Gtin);
        var existing = StoreOffer.Create(_product.Id, ProductSource.Kabum, candidate.Url);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.Kabum, candidate.Url)).ReturnsAsync(existing);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Attached);
        result.OfferId.Should().Be(existing.Id);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenTheOfferBelongsToAnotherProduct_DoesNotMergeCatalogProductsByVote()
    {
        var candidate = Candidate(CandidateReason.Gtin);
        var elsewhere = StoreOffer.Create("some-other-product", ProductSource.Kabum, candidate.Url);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.Kabum, candidate.Url)).ReturnsAsync(elsewhere);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Unavailable);
        elsewhere.ProductId.Should().Be("some-other-product");
        _offers.Verify(r => r.UpdateAsync(It.Is<StoreOffer>(o => o.ProductId == _product.Id)), Times.Never);
    }

    [Fact]
    public async Task Execute_VotingAgain_RevisesTheExistingFeedbackInsteadOfDuplicatingIt()
    {
        var candidate = Candidate(CandidateReason.Keyword);
        var previous = OfferMatchFeedback.Create(UserId, candidate.Id, _product.Id, candidate.Url, MatchVerdict.NotSame, false);
        _feedback.Setup(r => r.GetByUserAndCandidateAsync(UserId, candidate.Id)).ReturnsAsync(previous);

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, false));

        previous.Verdict.Should().Be(MatchVerdict.Same);
        _feedback.Verify(r => r.SaveAsync(It.Is<OfferMatchFeedback>(f => f.Id == previous.Id)), Times.Once);
    }

    [Fact]
    public async Task Execute_AppliesTheTargetPriceToTheUsersSubscription()
    {
        var candidate = Candidate(CandidateReason.Gtin);

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true, 2000m));

        _subscription.TargetPrice.Should().Be(2000m);
        _subscriptions.Verify(r => r.UpdateAsync(_subscription), Times.Once);
    }

    [Fact]
    public async Task Execute_LeavesTheTargetPriceAloneWhenNoneIsGiven()
    {
        var candidate = Candidate(CandidateReason.Gtin);

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        _subscription.TargetPrice.Should().Be(100m);
    }
}
