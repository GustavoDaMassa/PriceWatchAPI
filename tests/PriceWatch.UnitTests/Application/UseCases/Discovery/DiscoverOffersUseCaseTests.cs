using FluentAssertions;
using Moq;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Discovery;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Discovery;

public class DiscoverOffersUseCaseTests
{
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IOfferCandidateRepository> _candidates = new();
    private readonly Mock<IOfferMatchFeedbackRepository> _feedback = new();
    private readonly Mock<IDiscoveryGate> _gate = new();
    private readonly Mock<IMatchJudge> _judge = new();
    private readonly List<Mock<IOfferSearcher>> _searchers = new();

    private readonly Product _product = Product.Create(
        "Notebook Gamer Acer Nitro 5 AN515-58 16GB", gtin: "7891234567895", brand: "Acer", model: "AN515-58");

    private const string UserId = "user-1";

    public DiscoverOffersUseCaseTests()
    {
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, _product.Id))
            .ReturnsAsync(UserProductSubscription.Create(UserId, _product.Id, 100m));
        _products.Setup(r => r.GetByIdAsync(_product.Id)).ReturnsAsync(_product);
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id))
            .ReturnsAsync(new[] { StoreOffer.Create(_product.Id, ProductSource.MercadoLivre, "https://ml/origem") });
        _gate.Setup(g => g.TryAcquireAsync(_product.Id, It.IsAny<TimeSpan>())).ReturnsAsync(true);
        _candidates.Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<ProductSource>(), It.IsAny<string>()))
            .ReturnsAsync((OfferCandidate?)null);
        _judge.Setup(j => j.IsAvailable).Returns(false);
        _gate.Setup(g => g.TryAcquireAsync($"{_product.Id}:new", It.IsAny<TimeSpan>())).ReturnsAsync(true);
        _candidates.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(Array.Empty<OfferCandidate>());
        _feedback.Setup(r => r.GetByUserAndProductAsync(UserId, _product.Id)).ReturnsAsync(Array.Empty<OfferMatchFeedback>());
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<string?>())).ReturnsAsync(Array.Empty<UserProductSubscription>());
    }

    private DiscoverOffersUseCase Build() =>
        new(_subscriptions.Object, _products.Object, _offers.Object, _candidates.Object, _feedback.Object,
            _searchers.Select(s => s.Object), _gate.Object, _judge.Object);

    private Mock<IOfferSearcher> Searcher(ProductSource source, bool available = true,
        IReadOnlyList<OfferSearchResult>? byGtin = null, IReadOnlyList<OfferSearchResult>? byKeyword = null,
        IReadOnlyList<IReadOnlyList<OfferSearchResult>>? pages = null)
    {
        var searcher = new Mock<IOfferSearcher>();
        searcher.Setup(s => s.ProductSource).Returns(source);
        searcher.Setup(s => s.IsAvailable).Returns(available);
        searcher.Setup(s => s.SearchByGtinAsync(It.IsAny<string>())).ReturnsAsync(byGtin ?? []);
        searcher.Setup(s => s.SearchByKeywordAsync(It.IsAny<string>())).ReturnsAsync(byKeyword ?? []);
        for (var page = 0; page < 5; page++)
        {
            var results = pages is not null && page < pages.Count ? pages[page] : [];
            searcher.Setup(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), page)).ReturnsAsync(results);
        }
        _searchers.Add(searcher);
        return searcher;
    }

    private static OfferSearchResult Result(string url, string name, string? gtin = null, string? brand = null, string? model = null) =>
        new(url, name, 2500m, null, gtin, brand, model);

    private List<OfferCandidate> Saved()
    {
        var saved = new List<OfferCandidate>();
        _candidates.Setup(r => r.SaveAsync(It.IsAny<OfferCandidate>()))
            .Callback<OfferCandidate>(saved.Add).Returns(Task.CompletedTask);
        return saved;
    }

    [Fact]
    public async Task Execute_WhenTheUserDoesNotTrackTheProduct_ThrowsNotFound()
    {
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync("outsider", _product.Id)).ReturnsAsync((UserProductSubscription?)null);

        var act = () => Build().ExecuteAsync("outsider", _product.Id);

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task Execute_SearchesByGtinFirstAndSavesAnAcceptedCandidateWithTheGtinReason()
    {
        var kabum = Searcher(ProductSource.Kabum, byGtin: [Result("https://kabum/p/1", "Qualquer titulo", gtin: "7891234567895")]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().ContainSingle();
        saved[0].Reason.Should().Be(CandidateReason.Gtin);
        saved[0].Source.Should().Be(ProductSource.Kabum);
        saved[0].ProductId.Should().Be(_product.Id);
        kabum.Verify(s => s.SearchByKeywordAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenTheGtinSearchFindsNothing_FallsBackToTheKeywordSearch()
    {
        Searcher(ProductSource.Kabum, byGtin: [],
            byKeyword: [Result("https://kabum/p/2", "Notebook Gamer Acer Nitro 5 AN515-58 Intel Core i5 16GB RTX 4050")]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().ContainSingle().Which.Reason.Should().Be(CandidateReason.Keyword);
    }

    [Fact]
    public async Task Execute_DiscardsResultsThatDoNotLookLikeTheSameProduct()
    {
        Searcher(ProductSource.Kabum, byKeyword:
        [
            Result("https://kabum/p/3", "Cadeira Gamer Reclinavel"),
            Result("https://kabum/p/4", "Notebook Gamer Acer Nitro 5 AN515-58 32GB"),   // outra configuração de memória
            Result("https://kabum/p/5", "Notebook Gamer Acer Nitro 5 AN515-58 16GB Intel Core i5")
        ]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Select(c => c.Url).Should().Equal("https://kabum/p/5");
    }

    [Fact]
    public async Task Execute_KeepsAtMostThreeCandidatesPerStoreOrderedByScore()
    {
        Searcher(ProductSource.Kabum, byKeyword: Enumerable.Range(1, 6).Select(i =>
            Result($"https://kabum/p/{i}", "Notebook Gamer Acer Nitro 5 AN515-58 16GB" + string.Concat(Enumerable.Repeat(" x", i - 1)))).ToList());
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().HaveCount(3);
        saved.Select(c => c.Score).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Execute_AlsoSearchesTheStoreOfTheOriginOffer()
    {
        var mercadoLivre = Searcher(ProductSource.MercadoLivre, byGtin: [Result("https://ml/outro", "x", gtin: "7891234567895")]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        mercadoLivre.Verify(s => s.SearchByGtinAsync("7891234567895"), Times.Once);
        saved.Should().ContainSingle().Which.Source.Should().Be(ProductSource.MercadoLivre);
    }

    [Fact]
    public async Task Execute_WhenTheGtinSearchOnlyReturnsTheTrackedOffer_FallsBackToTheKeywordSearch()
    {
        var mercadoLivre = Searcher(ProductSource.MercadoLivre,
            byGtin: [Result("https://ml/origem", "Notebook Gamer Acer Nitro 5 AN515-58 16GB", gtin: "7891234567895")],
            byKeyword: [Result("https://ml/outro", "Notebook Gamer Acer Nitro 5 AN515-58 16GB Intel Core i5")]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        mercadoLivre.Verify(s => s.SearchByKeywordAsync(It.IsAny<string>()), Times.Once);
        saved.Select(c => c.Url).Should().Equal("https://ml/outro");
    }

    [Fact]
    public async Task Execute_SkipsUnavailableSearchers()
    {
        var kabum = Searcher(ProductSource.Kabum, available: false);

        await Build().ExecuteAsync(UserId, _product.Id);

        kabum.Verify(s => s.SearchByGtinAsync(It.IsAny<string>()), Times.Never);
        kabum.Verify(s => s.SearchByKeywordAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_DoesNotOfferAnUrlTheProductAlreadyTracks()
    {
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[]
        {
            StoreOffer.Create(_product.Id, ProductSource.MercadoLivre, "https://ml/origem"),
            StoreOffer.Create(_product.Id, ProductSource.Manual, "https://kabum/p/ja-acompanhada")
        });
        Searcher(ProductSource.Kabum, byGtin: [Result("https://kabum/p/ja-acompanhada", "x", gtin: "7891234567895")]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_RefreshesAnExistingCandidateInsteadOfCreatingADuplicate()
    {
        var existing = OfferCandidate.Create(_product.Id, ProductSource.Kabum,
            Result("https://kabum/p/1", "antigo"), 0.5, CandidateReason.Keyword, TimeSpan.FromHours(1));
        _candidates.Setup(r => r.GetByKeyAsync(_product.Id, ProductSource.Kabum, "https://kabum/p/1")).ReturnsAsync(existing);
        Searcher(ProductSource.Kabum, byGtin: [Result("https://kabum/p/1", "novo titulo", gtin: "7891234567895") with { Price = 1800m }]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().ContainSingle().Which.Id.Should().Be(existing.Id);
        existing.Price.Should().Be(1800m);
        existing.Reason.Should().Be(CandidateReason.Gtin);
    }

    [Fact]
    public async Task Execute_WhenTheStoreWasSearchedRecently_DoesNotCallTheStoresAgain()
    {
        _gate.Setup(g => g.TryAcquireAsync(_product.Id, It.IsAny<TimeSpan>())).ReturnsAsync(false);
        var kabum = Searcher(ProductSource.Kabum, byGtin: [Result("https://kabum/p/1", "x", gtin: "7891234567895")]);

        var ran = await Build().ExecuteAsync(UserId, _product.Id);

        ran.Should().BeFalse();
        kabum.Verify(s => s.SearchByGtinAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenOneStoreFindsNothing_TheOthersStillWork()
    {
        Searcher(ProductSource.Manual);
        Searcher(ProductSource.Kabum, byGtin: [Result("https://kabum/p/1", "x", gtin: "7891234567895")]);
        var saved = Saved();

        var ran = await Build().ExecuteAsync(UserId, _product.Id);

        ran.Should().BeTrue();
        saved.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_WithAProductWithoutGtin_UsesTheKeywordSearchOnly()
    {
        var noGtin = Product.Create("Notebook Gamer Acer Nitro 5 AN515-58 16GB", brand: "Acer", model: "AN515-58");
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, noGtin.Id)).ReturnsAsync(UserProductSubscription.Create(UserId, noGtin.Id, 1m));
        _products.Setup(r => r.GetByIdAsync(noGtin.Id)).ReturnsAsync(noGtin);
        _offers.Setup(r => r.GetByProductIdAsync(noGtin.Id)).ReturnsAsync(Array.Empty<StoreOffer>());
        _gate.Setup(g => g.TryAcquireAsync(noGtin.Id, It.IsAny<TimeSpan>())).ReturnsAsync(true);
        var kabum = Searcher(ProductSource.Kabum);

        await Build().ExecuteAsync(UserId, noGtin.Id);

        kabum.Verify(s => s.SearchByGtinAsync(It.IsAny<string>()), Times.Never);
        kabum.Verify(s => s.SearchByKeywordAsync(It.Is<string>(q => q.Contains("acer"))), Times.Once);
    }

    private static readonly string AcerTitle = "Notebook Gamer Acer Nitro 5 AN515-58 16GB Intel Core i5";

    [Fact]
    public async Task Execute_WithTheJudge_DropsTheCandidatesItSaysAreADifferentProductAndKeepsItsReason()
    {
        _judge.Setup(j => j.IsAvailable).Returns(true);
        _judge.Setup(j => j.JudgeAsync(_product, It.IsAny<IReadOnlyList<OfferSearchResult>>()))
            .ReturnsAsync(new[] { new MatchJudgement(0, false, "É uma capa"), new MatchJudgement(1, true, "Mesmo modelo e memória") });
        Searcher(ProductSource.MercadoLivre, byKeyword:
        [
            Result("https://ml/p/MLB1", AcerTitle + " capa"),
            Result("https://ml/p/MLB2", AcerTitle)
        ]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        var candidate = saved.Should().ContainSingle().Subject;
        candidate.AiSaysSame.Should().BeTrue();
        candidate.AiReason.Should().Be("Mesmo modelo e memória");
    }

    [Fact]
    public async Task Execute_WhenTheJudgeDoesNotAnswer_KeepsWhatTheRulesApproved()
    {
        _judge.Setup(j => j.IsAvailable).Returns(true);
        _judge.Setup(j => j.JudgeAsync(It.IsAny<Product>(), It.IsAny<IReadOnlyList<OfferSearchResult>>()))
            .ReturnsAsync((IReadOnlyList<MatchJudgement>?)null);
        Searcher(ProductSource.MercadoLivre, byKeyword: [Result("https://ml/p/MLB2", AcerTitle)]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().ContainSingle().Which.AiSaysSame.Should().BeNull();
    }

    [Fact]
    public async Task Execute_DoesNotAskTheJudgeAboutAGtinMatch()
    {
        _judge.Setup(j => j.IsAvailable).Returns(true);
        Searcher(ProductSource.MercadoLivre, byGtin: [Result("https://ml/p/MLB9", "Qualquer titulo", gtin: "7891234567895")]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Should().ContainSingle().Which.Reason.Should().Be(CandidateReason.Gtin);
        _judge.Verify(j => j.JudgeAsync(It.IsAny<Product>(), It.IsAny<IReadOnlyList<OfferSearchResult>>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UsesTheQuerySuggestedByTheJudgeForTheKeywordSearch()
    {
        _judge.Setup(j => j.IsAvailable).Returns(true);
        _judge.Setup(j => j.SuggestSearchQueryAsync(_product)).ReturnsAsync("acer nitro 5 an515-58 16gb");
        var store = Searcher(ProductSource.MercadoLivre);

        await Build().ExecuteAsync(UserId, _product.Id);

        store.Verify(s => s.SearchByKeywordAsync("acer nitro 5 an515-58 16gb"), Times.Once);
    }

    [Fact]
    public async Task Execute_WithoutASuggestion_FallsBackToTheRulesQuery()
    {
        _judge.Setup(j => j.IsAvailable).Returns(true);
        _judge.Setup(j => j.SuggestSearchQueryAsync(_product)).ReturnsAsync((string?)null);
        var store = Searcher(ProductSource.MercadoLivre);

        await Build().ExecuteAsync(UserId, _product.Id);

        store.Verify(s => s.SearchByKeywordAsync(It.Is<string>(q => q.Contains("acer"))), Times.Once);
    }

    // ---- nova busca (findNew) ----

    [Fact]
    public async Task FindNew_IgnoresTheSixHourWindowButHasItsOwnShortCooldown()
    {
        _gate.Setup(g => g.TryAcquireAsync(_product.Id, It.IsAny<TimeSpan>())).ReturnsAsync(false);
        var store = Searcher(ProductSource.MercadoLivre, pages: [[Result("https://ml/p/MLB2", AcerTitle)]]);
        var saved = Saved();

        var ran = await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        ran.Should().BeTrue();
        saved.Should().ContainSingle();
        _gate.Verify(g => g.TryAcquireAsync($"{_product.Id}:new", TimeSpan.FromSeconds(45)), Times.Once);
        store.Verify(s => s.SearchByGtinAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FindNew_WhenTheCooldownIsActive_DoesNotCallTheStores()
    {
        _gate.Setup(g => g.TryAcquireAsync($"{_product.Id}:new", It.IsAny<TimeSpan>())).ReturnsAsync(false);
        var store = Searcher(ProductSource.MercadoLivre, pages: [[Result("https://ml/p/MLB2", AcerTitle)]]);

        var ran = await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        ran.Should().BeFalse();
        store.Verify(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task FindNew_SkipsCandidatesThatAlreadyAppearedAndWhatTheUserAlreadyJudged()
    {
        var shown = OfferCandidate.Create(_product.Id, ProductSource.MercadoLivre, Result("https://ml/p/MLB10", AcerTitle), 0.9, CandidateReason.Keyword, TimeSpan.FromHours(1));
        _candidates.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { shown });
        _feedback.Setup(r => r.GetByUserAndProductAsync(UserId, _product.Id)).ReturnsAsync(new[]
        {
            OfferMatchFeedback.Create(UserId, "c-old", _product.Id, "https://ml/p/MLB11", MatchVerdict.NotSame, false)
        });
        Searcher(ProductSource.MercadoLivre, pages:
        [[
            Result("https://ml/p/MLB10", AcerTitle),
            Result("https://ml/p/MLB11", AcerTitle),
            Result("https://ml/p/MLB12", AcerTitle)
        ]]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        saved.Select(c => c.Url).Should().Equal("https://ml/p/MLB12");
    }

    [Fact]
    public async Task FindNew_SkipsWhatTheUserAlreadyTracksInAnyOfTheirProducts()
    {
        var other = Product.Create("Outro produto");
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<string?>()))
            .ReturnsAsync(new[] { UserProductSubscription.Create(UserId, other.Id, 1m) });
        _offers.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new[] { StoreOffer.Create(other.Id, ProductSource.MercadoLivre, "https://ml/p/MLB20") });
        Searcher(ProductSource.MercadoLivre, pages: [[Result("https://ml/p/MLB20", AcerTitle), Result("https://ml/p/MLB21", AcerTitle)]]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        saved.Select(c => c.Url).Should().Equal("https://ml/p/MLB21");
    }

    [Fact]
    public async Task FindNew_WhenThePageHasNothingNew_GoesOnToTheNextPage()
    {
        var shown = OfferCandidate.Create(_product.Id, ProductSource.MercadoLivre, Result("https://ml/p/MLB10", AcerTitle), 0.9, CandidateReason.Keyword, TimeSpan.FromHours(1));
        _candidates.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { shown });
        var store = Searcher(ProductSource.MercadoLivre, pages:
        [
            [Result("https://ml/p/MLB10", AcerTitle)],
            [Result("https://ml/p/MLB30", AcerTitle)]
        ]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        saved.Select(c => c.Url).Should().Equal("https://ml/p/MLB30");
        store.Verify(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), 2), Times.Never);
    }

    [Fact]
    public async Task FindNew_StopsAtThreePages()
    {
        var shown = OfferCandidate.Create(_product.Id, ProductSource.MercadoLivre, Result("https://ml/p/MLB10", AcerTitle), 0.9, CandidateReason.Keyword, TimeSpan.FromHours(1));
        _candidates.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { shown });
        IReadOnlyList<OfferSearchResult> onlyKnown = [Result("https://ml/p/MLB10", AcerTitle)];
        var endless = Searcher(ProductSource.MercadoLivre, pages: [onlyKnown, onlyKnown, onlyKnown, onlyKnown]);

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        endless.Verify(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(3));
    }

    [Fact]
    public async Task FindNew_WhenTheStoreRunsOutOfResults_DoesNotAskForMorePages()
    {
        var empty = Searcher(ProductSource.MercadoLivre);

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        empty.Verify(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), 0), Times.Once);
        empty.Verify(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), 1), Times.Never);
    }

    [Fact]
    public async Task Execute_NeverSuggestsWhatTheUserAlreadyTracksInAnotherProduct()
    {
        var other = Product.Create("Outro produto");
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<string?>()))
            .ReturnsAsync(new[] { UserProductSubscription.Create(UserId, other.Id, 1m) });
        _offers.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new[] { StoreOffer.Create(other.Id, ProductSource.MercadoLivre, "https://ml/p/MLB40") });
        Searcher(ProductSource.MercadoLivre, byKeyword: [Result("https://ml/p/MLB40", AcerTitle), Result("https://ml/p/MLB41", AcerTitle)]);
        var saved = Saved();

        await Build().ExecuteAsync(UserId, _product.Id);

        saved.Select(c => c.Url).Should().Equal("https://ml/p/MLB41");
    }

    [Fact]
    public async Task FindNew_AfterThreeEmptyNewSearches_StopsWithoutCallingTheStoresOrTakingTheCooldown()
    {
        _gate.Setup(g => g.CountEmptyRunsAsync(_product.Id)).ReturnsAsync(3);
        var store = Searcher(ProductSource.MercadoLivre, pages: [[Result("https://ml/p/MLB2", AcerTitle)]]);

        var act = () => Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        await act.Should().ThrowAsync<NoMoreSimilarProductsException>();
        store.Verify(s => s.SearchByKeywordPageAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        _gate.Verify(g => g.TryAcquireAsync($"{_product.Id}:new", It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task FindNew_WithTwoEmptyNewSearchesSoFar_StillRuns()
    {
        _gate.Setup(g => g.CountEmptyRunsAsync(_product.Id)).ReturnsAsync(2);
        Searcher(ProductSource.MercadoLivre);

        var ran = await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task FindNew_ARunWithNothingNew_CountsAsEmpty()
    {
        Searcher(ProductSource.MercadoLivre);

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        _gate.Verify(g => g.RecordNewSearchAsync(_product.Id, false), Times.Once);
    }

    [Fact]
    public async Task FindNew_ARunThatFindsSomethingNew_ResetsTheEmptyCount()
    {
        Searcher(ProductSource.MercadoLivre, pages: [[Result("https://ml/p/MLB2", AcerTitle)]]);
        Saved();

        await Build().ExecuteAsync(UserId, _product.Id, findNew: true);

        _gate.Verify(g => g.RecordNewSearchAsync(_product.Id, true), Times.Once);
    }

    [Fact]
    public async Task Execute_ANormalSearch_NeitherCountsNorIsBlockedByEmptyNewSearches()
    {
        _gate.Setup(g => g.CountEmptyRunsAsync(_product.Id)).ReturnsAsync(3);
        Searcher(ProductSource.MercadoLivre);

        var ran = await Build().ExecuteAsync(UserId, _product.Id);

        ran.Should().BeTrue();
        _gate.Verify(g => g.RecordNewSearchAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }
}
