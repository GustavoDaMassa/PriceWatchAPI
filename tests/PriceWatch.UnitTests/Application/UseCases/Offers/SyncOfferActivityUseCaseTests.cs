using Moq;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class SyncOfferActivityUseCaseTests
{
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly SyncOfferActivityUseCase _useCase;

    public SyncOfferActivityUseCaseTests()
    {
        _useCase = new SyncOfferActivityUseCase(_offers.Object, _subscriptions.Object);
    }

    private static StoreOffer Offer(string productId, bool active)
    {
        var offer = StoreOffer.Create(productId, ProductSource.MercadoLivre, $"https://ml.com/{Guid.NewGuid()}");
        if (!active) offer.Deactivate();
        return offer;
    }

    private static UserProductSubscription Subscription(string productId, bool active)
    {
        var sub = UserProductSubscription.Create(Guid.NewGuid().ToString(), productId, 100m);
        if (!active) sub.Deactivate();
        return sub;
    }

    [Fact]
    public async Task Execute_WithAnActiveSubscription_ActivatesInactiveOffers()
    {
        var offer = Offer("p1", active: false);
        _subscriptions.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(new[] { Subscription("p1", true) });
        _offers.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(new[] { offer });

        await _useCase.ExecuteAsync("p1");

        Assert.True(offer.IsActive);
        _offers.Verify(r => r.UpdateAsync(offer), Times.Once);
    }

    [Fact]
    public async Task Execute_WithNoActiveSubscription_DeactivatesActiveOffers()
    {
        var offer = Offer("p1", active: true);
        _subscriptions.Setup(r => r.GetByProductIdAsync("p1"))
            .ReturnsAsync(new[] { Subscription("p1", false), Subscription("p1", false) });
        _offers.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(new[] { offer });

        await _useCase.ExecuteAsync("p1");

        Assert.False(offer.IsActive);
        _offers.Verify(r => r.UpdateAsync(offer), Times.Once);
    }

    [Fact]
    public async Task Execute_WithNoSubscriptionsAtAll_DeactivatesOffers()
    {
        var offer = Offer("p1", active: true);
        _subscriptions.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(Array.Empty<UserProductSubscription>());
        _offers.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(new[] { offer });

        await _useCase.ExecuteAsync("p1");

        Assert.False(offer.IsActive);
    }

    [Fact]
    public async Task Execute_WhenOffersAlreadyMatchTheDesiredState_DoesNotWrite()
    {
        var offer = Offer("p1", active: true);
        _subscriptions.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(new[] { Subscription("p1", true) });
        _offers.Setup(r => r.GetByProductIdAsync("p1")).ReturnsAsync(new[] { offer });

        await _useCase.ExecuteAsync("p1");

        _offers.Verify(r => r.UpdateAsync(It.IsAny<StoreOffer>()), Times.Never);
    }
}
