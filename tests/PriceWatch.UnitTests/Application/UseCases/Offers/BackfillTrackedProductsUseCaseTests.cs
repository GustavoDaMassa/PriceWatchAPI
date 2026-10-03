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

    [Fact]
    public async Task Execute_ShouldPreserveTheLegacyIdOnTheOfferAndTheSubscription()
    {
        var legacy = CreateLegacy();
        legacy.RecordPrice(90m);
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        await _useCase.ExecuteAsync();

        // o id da oferta é o do item legado, então o histórico já gravado em price_snapshots
        // (chaveado pelo id do TrackedProduct) continua valendo; o da assinatura mantém os links da SPA
        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.Id == legacy.Id)), Times.Once);
        _subscriptions.Verify(r => r.CreateAsync(It.Is<UserProductSubscription>(s => s.Id == legacy.Id)), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldRestoreTheLegacyLowestPrice()
    {
        var legacy = CreateLegacy();
        legacy.RecordPrice(80m);
        legacy.RecordPrice(120m);
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        await _useCase.ExecuteAsync();

        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.CurrentPrice == 120m && o.LowestPrice == 80m)), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldNotRealertWhenTheLegacyTargetAlertWasAlreadySent()
    {
        var legacy = CreateLegacy(targetPrice: 100m);
        legacy.RecordPrice(90m);
        legacy.MarkTargetAlertSent();
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        await _useCase.ExecuteAsync();

        _subscriptions.Verify(r => r.CreateAsync(It.Is<UserProductSubscription>(s => s.TargetAlertSent)), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenTwoUsersTrackTheSameUrl_TheOldestKeepsTheOfferIdAndBothKeepTheirSubscriptionIds()
    {
        var oldest = DomainTrackedProduct.Create("user-1", "https://ml.com/same", ProductSource.MercadoLivre, "Produto", 100m);
        oldest.RecordPrice(90m);
        Thread.Sleep(5);
        var newest = DomainTrackedProduct.Create("user-2", "https://ml.com/same", ProductSource.MercadoLivre, "Produto", 120m);
        newest.RecordPrice(90m);
        // devolvido fora de ordem de propósito: o mais antigo precisa ser processado primeiro
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([newest, oldest]);

        StoreOffer? createdOffer = null;
        _offers.Setup(r => r.CreateAsync(It.IsAny<StoreOffer>()))
            .Callback<StoreOffer>(o => createdOffer = o)
            .Returns(Task.CompletedTask);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, "https://ml.com/same"))
            .ReturnsAsync(() => createdOffer);
        Product? createdProduct = null;
        _products.Setup(r => r.CreateAsync(It.IsAny<Product>()))
            .Callback<Product>(p => createdProduct = p)
            .Returns(Task.CompletedTask);
        _products.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync(() => createdProduct);

        await _useCase.ExecuteAsync();

        createdOffer!.Id.Should().Be(oldest.Id);
        _subscriptions.Verify(r => r.CreateAsync(It.Is<UserProductSubscription>(s => s.Id == oldest.Id && s.UserId == "user-1")), Times.Once);
        _subscriptions.Verify(r => r.CreateAsync(It.Is<UserProductSubscription>(s => s.Id == newest.Id && s.UserId == "user-2")), Times.Once);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenAnActiveItemJoinsAnInactiveSharedOffer_ReactivatesTheOffer()
    {
        var product = Product.Create("Produto Legado");
        var inactiveOffer = StoreOffer.Create(product.Id, ProductSource.MercadoLivre, "https://ml.com/item");
        inactiveOffer.Deactivate();
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, "https://ml.com/item")).ReturnsAsync(inactiveOffer);
        _products.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        var legacy = CreateLegacy();
        _trackedProducts.Setup(r => r.GetAllAsync()).ReturnsAsync([legacy]);

        await _useCase.ExecuteAsync();

        inactiveOffer.IsActive.Should().BeTrue();
        _offers.Verify(r => r.UpdateAsync(inactiveOffer), Times.Once);
    }
}
