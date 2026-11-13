using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Discovery;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.UseCases.Discovery;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Discovery;

public class ConfirmOfferMatchUseCaseTests
{
    private readonly Mock<IOfferCandidateRepository> _candidates = new();
    private readonly Mock<IOfferMatchFeedbackRepository> _feedback = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<ICreateOfferUseCase> _createOffer = new();
    private readonly ConfirmOfferMatchUseCase _useCase;

    private const string UserId = "user-1";
    private const string ListId = "list-1";
    private readonly Product _product = Product.Create("Notebook Acer", gtin: "7891234567895");
    private readonly UserProductSubscription _subscription;

    public ConfirmOfferMatchUseCaseTests()
    {
        _subscription = UserProductSubscription.Create(UserId, _product.Id, 100m, ListId);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, _product.Id)).ReturnsAsync(_subscription);
        _feedback.Setup(r => r.GetByUserAndCandidateAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((OfferMatchFeedback?)null);
        _createOffer.Setup(c => c.ExecuteAsync(UserId, It.IsAny<CreateOfferRequest>()))
            .ReturnsAsync((string _, CreateOfferRequest r) => new OfferResponse(
                "sub-new", "product-new", "offer-new", "Notebook Acer", r.Url, null, ProductSource.MercadoLivre,
                r.TargetPrice, 2599.90m, 2599.90m, true, DateTime.UtcNow, r.ListId));

        _useCase = new ConfirmOfferMatchUseCase(_candidates.Object, _feedback.Object, _subscriptions.Object, _createOffer.Object);
    }

    private OfferCandidate Candidate(CandidateReason reason = CandidateReason.Keyword, string url = "https://ml/p/MLB1")
    {
        var candidate = OfferCandidate.Create(_product.Id, ProductSource.MercadoLivre,
            new OfferSearchResult(url, "Notebook Acer", 2599.90m, null, "7891234567895"), 0.9, reason, TimeSpan.FromHours(1));
        _candidates.Setup(r => r.GetByIdAsync(candidate.Id)).ReturnsAsync(candidate);
        return candidate;
    }

    [Fact]
    public async Task Execute_WhenTheCandidateDoesNotExistOrIsExpired_ThrowsNotFound()
    {
        var expired = OfferCandidate.Create(_product.Id, ProductSource.MercadoLivre, new OfferSearchResult("u", "n", 1m), 1, CandidateReason.Gtin, TimeSpan.FromSeconds(-1));
        _candidates.Setup(r => r.GetByIdAsync(expired.Id)).ReturnsAsync(expired);

        var missing = () => _useCase.ExecuteAsync(UserId, "nope", new ConfirmOfferMatchRequest(true, true));
        var stale = () => _useCase.ExecuteAsync(UserId, expired.Id, new ConfirmOfferMatchRequest(true, true));

        await missing.Should().ThrowAsync<OfferCandidateNotFoundException>();
        await stale.Should().ThrowAsync<OfferCandidateNotFoundException>();
    }

    [Fact]
    public async Task Execute_WhenTheUserDoesNotTrackTheProduct_ThrowsNotFoundWithoutRevealingIt()
    {
        var candidate = Candidate();

        var act = () => _useCase.ExecuteAsync("outsider", candidate.Id, new ConfirmOfferMatchRequest(true, true));

        await act.Should().ThrowAsync<OfferCandidateNotFoundException>();
        _feedback.Verify(r => r.SaveAsync(It.IsAny<OfferMatchFeedback>()), Times.Never);
    }

    [Fact]
    public async Task Execute_Rejected_RecordsTheVerdictAndCreatesNothing()
    {
        var candidate = Candidate();

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(false, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Rejected);
        result.ProductId.Should().Be(_product.Id);
        _feedback.Verify(r => r.SaveAsync(It.Is<OfferMatchFeedback>(f => f.Verdict == MatchVerdict.NotSame && !f.Track)), Times.Once);
        _createOffer.Verify(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<CreateOfferRequest>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SimilarWithoutTracking_OnlyRecordsTheVerdict()
    {
        var candidate = Candidate();

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, false));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Recorded);
        _createOffer.Verify(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<CreateOfferRequest>()), Times.Never);
    }

    [Fact]
    public async Task Execute_Tracking_CreatesANewProductInTheSameListWithoutASecondConfirmation()
    {
        var candidate = Candidate(CandidateReason.Keyword);

        var result = await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        result.Outcome.Should().Be(ConfirmOfferMatchOutcome.Attached);
        result.ProductId.Should().Be("product-new");
        result.OfferId.Should().Be("offer-new");
        _createOffer.Verify(c => c.ExecuteAsync(UserId,
            It.Is<CreateOfferRequest>(r => r.Url == candidate.Url && r.ListId == ListId)), Times.Once);
        _feedback.Verify(r => r.SaveAsync(It.Is<OfferMatchFeedback>(f => f.Verdict == MatchVerdict.Same && f.Track)), Times.Once);
    }

    [Fact]
    public async Task Execute_Tracking_UsesTheTargetPriceGivenForTheNewProduct()
    {
        var candidate = Candidate();

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true, 2000m));

        _createOffer.Verify(c => c.ExecuteAsync(UserId, It.Is<CreateOfferRequest>(r => r.TargetPrice == 2000m)), Times.Once);
        _subscription.TargetPrice.Should().Be(100m);
    }

    [Fact]
    public async Task Execute_Tracking_InheritsTheTargetPriceOfTheOriginProductWhenNoneIsGiven()
    {
        var candidate = Candidate();

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, true));

        _createOffer.Verify(c => c.ExecuteAsync(UserId, It.Is<CreateOfferRequest>(r => r.TargetPrice == 100m)), Times.Once);
    }

    [Fact]
    public async Task Execute_VotingAgain_RevisesTheExistingFeedbackInsteadOfDuplicatingIt()
    {
        var candidate = Candidate();
        var previous = OfferMatchFeedback.Create(UserId, candidate.Id, _product.Id, candidate.Url, MatchVerdict.NotSame, false);
        _feedback.Setup(r => r.GetByUserAndCandidateAsync(UserId, candidate.Id)).ReturnsAsync(previous);

        await _useCase.ExecuteAsync(UserId, candidate.Id, new ConfirmOfferMatchRequest(true, false));

        previous.Verdict.Should().Be(MatchVerdict.Same);
        _feedback.Verify(r => r.SaveAsync(It.Is<OfferMatchFeedback>(f => f.Id == previous.Id)), Times.Once);
    }
}
