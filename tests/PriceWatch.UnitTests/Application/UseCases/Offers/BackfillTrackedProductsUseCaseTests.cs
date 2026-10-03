using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class BackfillTrackedProductsUseCaseTests
{
    private readonly Mock<ITrackedProductRepository> _trackedProducts = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IProductMatchingEventPublisher> _matchingEventPublisher = new();
    private readonly BackfillTrackedProductsUseCase _useCase;

    public BackfillTrackedProductsUseCaseTests()
    {
        _offers.Setup(r => r.GetBySourceAndUrlAsync(It.IsAny<ProductSource>(), It.IsAny<string>()))
            .ReturnsAsync((StoreOffer?)null);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((UserProductSubscription?)null);

        _useCase = new BackfillTrackedProductsUseCase(
            _trackedProducts.Object, _products.Object, _offers.Object, _subscriptions.Object, _matchingEventPublisher.Object);
    }

    private static DomainTrackedProduct CreateLegacy(decimal targetPrice = 100m) =>
        DomainTrackedProduct.Create("user-1", "https://ml.com/item", ProductSource.MercadoLivre, "Produto Legado", targetPrice, "list-1");

    [Fact]
    public async Task Execute_ShouldCreateProductOfferAndSubscriptionForEachLegacyProduct()
    {
        var legacy = CreateLegacy();
        legacy.RecordPrice(90m);
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        var migrated = await _useCase.ExecuteAsync();

        migrated.Should().Be(1);
        _products.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Name == "Produto Legado")), Times.Once);
        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.Url == "https://ml.com/item" && o.CurrentPrice == 90m)), Times.Once);
        _subscriptions.Verify(r => r.CreateAsync(It.Is<UserProductSubscription>(s => s.UserId == "user-1" && s.TargetPrice == 100m)), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldMarkLegacyProductAsMigrated()
    {
        var legacy = CreateLegacy();
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        await _useCase.ExecuteAsync();

        legacy.IsMigratedToProduct().Should().BeTrue();
        _trackedProducts.Verify(r => r.UpdateAsync(It.Is<DomainTrackedProduct>(p => p.IsMigratedToProduct())), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldSkipAlreadyMigratedProducts()
    {
        var legacy = CreateLegacy();
        legacy.MarkMigratedToProduct("product-already-there");
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        var migrated = await _useCase.ExecuteAsync();

        migrated.Should().Be(0);
        _products.Verify(r => r.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenTwoLegacyProductsShareSourceAndUrl_ShouldReuseSameProductAndOnlyAddSubscription()
    {
        var legacyUser1 = DomainTrackedProduct.Create("user-1", "https://ml.com/shared-item", ProductSource.MercadoLivre, "Produto Compartilhado", 100m);
        var legacyUser2 = DomainTrackedProduct.Create("user-2", "https://ml.com/shared-item", ProductSource.MercadoLivre, "Produto Compartilhado", 120m);
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacyUser1, legacyUser2]);

        var existingProduct = Product.Create("Produto Compartilhado");
        var existingOffer = StoreOffer.Create(existingProduct.Id, ProductSource.MercadoLivre, "https://ml.com/shared-item");

        _offers.SetupSequence(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, "https://ml.com/shared-item"))
            .ReturnsAsync((StoreOffer?)null)
            .ReturnsAsync(existingOffer);

        _products.Setup(r => r.CreateAsync(It.IsAny<Product>()))
            .Callback<Product>(p => existingProduct = p)
            .Returns(Task.CompletedTask);
        _products.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(() => existingProduct);

        var migrated = await _useCase.ExecuteAsync();

        migrated.Should().Be(2);
        _products.Verify(r => r.CreateAsync(It.IsAny<Product>()), Times.Once);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Once);
        _subscriptions.Verify(r => r.CreateAsync(It.IsAny<UserProductSubscription>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_WhenLegacyProductIsInactive_ShouldCreateInactiveSubscription()
    {
        var legacy = CreateLegacy();
        legacy.Deactivate();
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        await _useCase.ExecuteAsync();

        _subscriptions.Verify(r => r.CreateAsync(It.Is<UserProductSubscription>(s => !s.IsActive)), Times.Once);
    }
}
