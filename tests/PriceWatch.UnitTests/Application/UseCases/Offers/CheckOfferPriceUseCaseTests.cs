using FluentAssertions;
using Moq;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class CheckOfferPriceUseCaseTests
{
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IPriceSnapshotRepository> _snapshots = new();
    private readonly Mock<IPriceFetcherResolver> _fetchers = new();
    private readonly Mock<IPriceFetcher> _fetcher = new();
    private readonly Mock<IAlertPublisher> _alerts = new();
    private readonly CheckOfferPriceUseCase _useCase;

    private readonly Product _product = Product.Create("Notebook Gamer");

    public CheckOfferPriceUseCaseTests()
    {
        _fetchers.Setup(r => r.Resolve(It.IsAny<ProductSource>())).Returns(_fetcher.Object);
        _products.Setup(r => r.GetByIdAsync(_product.Id)).ReturnsAsync(_product);
        _useCase = new CheckOfferPriceUseCase(
            _offers.Object, _subscriptions.Object, _products.Object,
            _snapshots.Object, _fetchers.Object, _alerts.Object);
    }

    private StoreOffer OfferWithPrice(decimal current, decimal lowest, string url = "https://ml.com/a")
    {
        var offer = StoreOffer.Create(_product.Id, ProductSource.MercadoLivre, url);
        if (current > 0) offer.RecordPrice(current);
        offer.RestoreLowestPrice(lowest);
        return offer;
    }

    private UserProductSubscription Subscription(decimal target, string userId = "user-1")
    {
        var sub = UserProductSubscription.Create(userId, _product.Id, target);
        return sub;
    }

    private void Setup(StoreOffer offer, IEnumerable<UserProductSubscription> subs, IEnumerable<StoreOffer>? siblings = null)
    {
        _subscriptions.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(subs.ToList());
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync((siblings ?? new[] { offer }).ToList());
    }

    private void FetchReturns(decimal price) =>
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProductFetchResult(price, "Notebook Gamer"));

    [Fact]
    public async Task Execute_RecordsThePriceAndSavesTheSnapshot()
    {
        var offer = OfferWithPrice(1000m, 1000m);
        Setup(offer, new[] { Subscription(0m) });
        FetchReturns(950m);

        await _useCase.ExecuteAsync(offer);

        offer.CurrentPrice.Should().Be(950m);
        offer.LowestPrice.Should().Be(950m);
        _snapshots.Verify(r => r.CreateAsync(It.Is<PriceSnapshot>(s => s.Price == 950m && s.ProductId == offer.Id)), Times.Once);
        _offers.Verify(r => r.UpdateAsync(offer), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenPriceReachesTheTarget_PublishesATargetAlertWithTheSubscriptionId()
    {
        var offer = OfferWithPrice(1000m, 900m);
        var sub = Subscription(target: 920m);
        Setup(offer, new[] { sub });
        FetchReturns(910m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(a => a.PublishAsync(sub.Id, "user-1", "Notebook Gamer", NotificationType.TargetPriceReached, 910m), Times.Once);
        sub.TargetAlertSent.Should().BeTrue();
        _subscriptions.Verify(r => r.UpdateAsync(sub), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenTheTargetAlertWasAlreadySent_DoesNotRepeatIt()
    {
        var offer = OfferWithPrice(900m, 800m);
        var sub = Subscription(target: 920m);
        sub.MarkTargetAlertSent();
        Setup(offer, new[] { sub });
        FetchReturns(890m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            NotificationType.TargetPriceReached, It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenThePriceGoesBackAboveTheTarget_ReArmsTheTargetAlert()
    {
        var offer = OfferWithPrice(900m, 800m);
        var sub = Subscription(target: 920m);
        sub.MarkTargetAlertSent();
        Setup(offer, new[] { sub });
        FetchReturns(1000m);

        await _useCase.ExecuteAsync(offer);

        sub.TargetAlertSent.Should().BeFalse();
        _subscriptions.Verify(r => r.UpdateAsync(sub), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenPriceIsANewLowestAndNoTarget_PublishesANewLowestAlert()
    {
        var offer = OfferWithPrice(1000m, 950m);
        var sub = Subscription(target: 0m);
        Setup(offer, new[] { sub });
        FetchReturns(900m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(a => a.PublishAsync(sub.Id, "user-1", "Notebook Gamer", NotificationType.NewLowestPrice, 900m), Times.Once);
    }

    [Fact]
    public async Task Execute_OnTheVeryFirstPrice_DoesNotPublishANewLowestAlert()
    {
        var offer = OfferWithPrice(0m, 0m);
        Setup(offer, new[] { Subscription(0m) });
        FetchReturns(500m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<NotificationType>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Execute_TargetAlertEvaluatesTheBestPriceAcrossStores()
    {
        var offer = OfferWithPrice(1000m, 1000m, "https://ml.com/a");
        var cheaperElsewhere = OfferWithPrice(850m, 850m, "https://kabum.com/b");
        var sub = Subscription(target: 900m);
        Setup(offer, new[] { sub }, new[] { offer, cheaperElsewhere });
        FetchReturns(1100m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(a => a.PublishAsync(sub.Id, "user-1", "Notebook Gamer", NotificationType.TargetPriceReached, 850m), Times.Once);
    }

    [Fact]
    public async Task Execute_NewLowestIsComparedAgainstTheLowestOfAllStores()
    {
        var offer = OfferWithPrice(1000m, 1000m, "https://ml.com/a");
        var otherStore = OfferWithPrice(800m, 700m, "https://kabum.com/b");
        Setup(offer, new[] { Subscription(0m) }, new[] { offer, otherStore });
        FetchReturns(900m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            NotificationType.NewLowestPrice, It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Execute_AlertsEveryActiveSubscriberButNotInactiveOnes()
    {
        var offer = OfferWithPrice(1000m, 1000m);
        var a = Subscription(1000m, "user-a");
        var b = Subscription(1000m, "user-b");
        var inactive = Subscription(1000m, "user-c");
        inactive.Deactivate();
        Setup(offer, new[] { a, b, inactive });
        FetchReturns(900m);

        await _useCase.ExecuteAsync(offer);

        _alerts.Verify(x => x.PublishAsync(a.Id, "user-a", It.IsAny<string>(), NotificationType.TargetPriceReached, 900m), Times.Once);
        _alerts.Verify(x => x.PublishAsync(b.Id, "user-b", It.IsAny<string>(), NotificationType.TargetPriceReached, 900m), Times.Once);
        _alerts.Verify(x => x.PublishAsync(inactive.Id, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<NotificationType>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithNoActiveSubscriptions_DeactivatesTheOfferWithoutFetching()
    {
        var offer = OfferWithPrice(1000m, 1000m);
        var inactive = Subscription(100m);
        inactive.Deactivate();
        Setup(offer, new[] { inactive });

        await _useCase.ExecuteAsync(offer);

        offer.IsActive.Should().BeFalse();
        _offers.Verify(r => r.UpdateAsync(offer), Times.Once);
        _fetcher.Verify(f => f.FetchAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenTheFetchFails_PostponesTheNextCheckAndRethrows()
    {
        var offer = OfferWithPrice(1000m, 1000m);
        offer.PostponeCheck(TimeSpan.Zero);
        Setup(offer, new[] { Subscription(500m) });
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>())).ThrowsAsync(new HttpRequestException("boom"));

        var act = () => _useCase.ExecuteAsync(offer);

        await act.Should().ThrowAsync<HttpRequestException>();
        offer.NextCheckAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(5));
        offer.CurrentPrice.Should().Be(1000m);
        _snapshots.Verify(r => r.CreateAsync(It.IsAny<PriceSnapshot>()), Times.Never);
        _offers.Verify(r => r.UpdateAsync(offer), Times.Once);
    }
}
