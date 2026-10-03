using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class MatchStoreOfferUseCaseTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly MatchStoreOfferUseCase _useCase;

    public MatchStoreOfferUseCaseTests()
    {
        _subscriptions.Setup(r => r.GetByProductIdAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<UserProductSubscription>());
        _useCase = new MatchStoreOfferUseCase(_products.Object, _offers.Object, _subscriptions.Object);
    }

    [Fact]
    public async Task Execute_WhenGtinMatchesAnotherProduct_ShouldReassignOfferAndMarkAutoMatched()
    {
        var source = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var offer = StoreOffer.Create(source.Id, ProductSource.MercadoLivre, "https://ml.com/item", "00190199719729");
        var target = Product.Create("iPhone 15 128GB", gtin: "00190199719729");

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetByGtinAsync(source.Gtin!, source.Id)).ReturnsAsync(target);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        offer.ProductId.Should().Be(target.Id);
        _offers.Verify(r => r.UpdateAsync(It.Is<StoreOffer>(o => o.ProductId == target.Id)), Times.Once);
        target.MatchStatus.Should().Be(MatchStatus.AutoMatched);
        _products.Verify(r => r.UpdateAsync(It.Is<Product>(p => p.Id == target.Id)), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenGtinHasNoOtherProduct_ShouldLeaveOfferUnchanged()
    {
        var source = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var offer = StoreOffer.Create(source.Id, ProductSource.MercadoLivre, "https://ml.com/item", "00190199719729");

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetByGtinAsync(source.Gtin!, source.Id)).ReturnsAsync((Product?)null);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        offer.ProductId.Should().Be(source.Id);
        _offers.Verify(r => r.UpdateAsync(It.IsAny<StoreOffer>()), Times.Never);
        _products.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithoutGtin_WhenFuzzyMatchFound_ShouldSuggestWithoutMergingOfferOrSubscriptions()
    {
        var source = Product.Create("iPhone 15 128GB Azul", brand: "Apple");
        var offer = StoreOffer.Create(source.Id, ProductSource.Kabum, "https://kabum.com/item");
        // mesmo produto, só formatado diferente entre lojas (espaço extra antes de "GB") —
        // cenário real de fuzzy match, não uma variação genuína de produto (cor/modelo)
        var target = Product.Create("iPhone 15 128 GB Azul", brand: "Apple");

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetWithoutGtinAsync(source.Id)).ReturnsAsync([target]);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        // RF15/RI07: fuzzy nunca funde de fato — só sugere. A oferta continua no produto
        // original, que segue funcionando normalmente pro usuário até confirmação manual
        offer.ProductId.Should().Be(source.Id);
        source.MatchStatus.Should().Be(MatchStatus.PendingReview);
        source.SuggestedMatchProductId.Should().Be(target.Id);
        _offers.Verify(r => r.UpdateAsync(It.IsAny<StoreOffer>()), Times.Never);
        _subscriptions.Verify(r => r.GetByProductIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithoutGtin_WhenNoCandidateAboveThreshold_ShouldLeaveUnmatched()
    {
        var source = Product.Create("iPhone 15 128GB", brand: "Apple");
        var offer = StoreOffer.Create(source.Id, ProductSource.Kabum, "https://kabum.com/item");
        var unrelated = Product.Create("Cadeira Gamer XPTO", brand: "OutraMarca");

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetWithoutGtinAsync(source.Id)).ReturnsAsync([unrelated]);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        offer.ProductId.Should().Be(source.Id);
        unrelated.MatchStatus.Should().Be(MatchStatus.Unmatched);
        source.MatchStatus.Should().Be(MatchStatus.Unmatched);
    }

    [Fact]
    public async Task Execute_WhenAlreadyReassigned_ShouldBeIdempotentAndDoNothing()
    {
        var source = Product.Create("iPhone 15 128GB");
        var offer = StoreOffer.Create(source.Id, ProductSource.MercadoLivre, "https://ml.com/item");
        offer.ReassignToProduct("some-other-product");

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        _products.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenOfferNotFound_ShouldDoNothing()
    {
        _offers.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((StoreOffer?)null);

        await _useCase.ExecuteAsync("missing", "product-1");

        _products.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenUserAlreadySubscribedToTargetProduct_ShouldDeleteDuplicateSubscription()
    {
        var source = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var offer = StoreOffer.Create(source.Id, ProductSource.MercadoLivre, "https://ml.com/item", "00190199719729");
        var target = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var duplicateSubscription = UserProductSubscription.Create("user-1", source.Id, 100m);

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetByGtinAsync(source.Gtin!, source.Id)).ReturnsAsync(target);
        _subscriptions.Setup(r => r.GetByProductIdAsync(source.Id)).ReturnsAsync([duplicateSubscription]);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync("user-1", target.Id))
            .ReturnsAsync(UserProductSubscription.Create("user-1", target.Id, 90m));

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        _subscriptions.Verify(r => r.DeleteAsync(duplicateSubscription.Id), Times.Once);
        _subscriptions.Verify(r => r.UpdateAsync(It.IsAny<UserProductSubscription>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenUserHasNoSubscriptionToTarget_ShouldReassignExistingSubscription()
    {
        var source = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var offer = StoreOffer.Create(source.Id, ProductSource.MercadoLivre, "https://ml.com/item", "00190199719729");
        var target = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var subscription = UserProductSubscription.Create("user-1", source.Id, 100m);

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetByGtinAsync(source.Gtin!, source.Id)).ReturnsAsync(target);
        _subscriptions.Setup(r => r.GetByProductIdAsync(source.Id)).ReturnsAsync([subscription]);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync("user-1", target.Id))
            .ReturnsAsync((UserProductSubscription?)null);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        subscription.ProductId.Should().Be(target.Id);
        _subscriptions.Verify(r => r.UpdateAsync(It.Is<UserProductSubscription>(s => s.ProductId == target.Id)), Times.Once);
        _subscriptions.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenTargetAlreadyConfirmed_ShouldNotDowngradeMatchStatus()
    {
        var source = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        var offer = StoreOffer.Create(source.Id, ProductSource.MercadoLivre, "https://ml.com/item", "00190199719729");
        var target = Product.Create("iPhone 15 128GB", gtin: "00190199719729");
        target.Confirm();

        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _products.Setup(r => r.GetByIdAsync(source.Id)).ReturnsAsync(source);
        _products.Setup(r => r.GetByGtinAsync(source.Gtin!, source.Id)).ReturnsAsync(target);

        await _useCase.ExecuteAsync(offer.Id, source.Id);

        target.MatchStatus.Should().Be(MatchStatus.Confirmed);
        _products.Verify(r => r.UpdateAsync(It.IsAny<Product>()), Times.Never);
        offer.ProductId.Should().Be(target.Id);
    }
}
