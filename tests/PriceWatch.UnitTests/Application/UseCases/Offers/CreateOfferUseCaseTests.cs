using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class CreateOfferUseCaseTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IPriceFetcherResolver> _resolver = new();
    private readonly Mock<IPriceFetcher> _fetcher = new();
    private readonly Mock<IProductMatchingEventPublisher> _matchingEventPublisher = new();
    private readonly Mock<IAffiliateLinkResolver> _affiliateResolver = new();
    private readonly Mock<IAffiliateLinkGenerator> _affiliateGenerator = new();
    private readonly CreateOfferUseCase _useCase;

    private const string MercadoLivreUrl = "https://produto.mercadolivre.com.br/MLB-123-titulo-_JM";

    public CreateOfferUseCaseTests()
    {
        _resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_fetcher.Object);
        _affiliateResolver.Setup(r => r.Resolve(It.IsAny<ProductSource>())).Returns(_affiliateGenerator.Object);
        _fetcher.Setup(f => f.ProductSource).Returns(ProductSource.MercadoLivre);
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProductFetchResult(150m, "iPhone 15 128GB", Gtin: "00190199719729"));

        _offers.Setup(r => r.GetBySourceAndUrlAsync(It.IsAny<ProductSource>(), It.IsAny<string>()))
            .ReturnsAsync((StoreOffer?)null);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((UserProductSubscription?)null);

        _useCase = new CreateOfferUseCase(
            _products.Object, _offers.Object, _subscriptions.Object, _resolver.Object, _matchingEventPublisher.Object,
            new SyncOfferActivityUseCase(_offers.Object, _subscriptions.Object),
            _affiliateResolver.Object);
    }

    [Fact]
    public async Task Execute_WhenOfferDoesNotExist_ShouldCreateProductAndOffer()
    {
        var request = new CreateOfferRequest(MercadoLivreUrl, 100m, "list-1");

        var result = await _useCase.ExecuteAsync("user-1", request);

        result.Name.Should().Be("iPhone 15 128GB");
        result.CurrentPrice.Should().Be(150m);
        _products.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Gtin == "00190199719729")), Times.Once);
        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.Url == MercadoLivreUrl && o.CurrentPrice == 150m)), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenOfferDoesNotExist_ShouldCreateSubscriptionWithTargetPrice()
    {
        var request = new CreateOfferRequest(MercadoLivreUrl, 100m, "list-1");

        await _useCase.ExecuteAsync("user-1", request);

        _subscriptions.Verify(r => r.CreateAsync(
            It.Is<UserProductSubscription>(s => s.UserId == "user-1" && s.TargetPrice == 100m && s.ListId == "list-1")),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WhenOfferAlreadyExists_ShouldNotFetchOrCreateNewProduct()
    {
        var existingProduct = Product.Create("iPhone 15 128GB");
        var existingOffer = StoreOffer.Create(existingProduct.Id, ProductSource.MercadoLivre, MercadoLivreUrl);
        existingOffer.RecordPrice(120m);

        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, MercadoLivreUrl))
            .ReturnsAsync(existingOffer);
        _products.Setup(r => r.GetByIdAsync(existingProduct.Id)).ReturnsAsync(existingProduct);

        var request = new CreateOfferRequest(MercadoLivreUrl, 100m);

        var result = await _useCase.ExecuteAsync("user-1", request);

        result.CurrentPrice.Should().Be(120m);
        _fetcher.Verify(f => f.FetchAsync(It.IsAny<string>()), Times.Never);
        _products.Verify(r => r.CreateAsync(It.IsAny<Product>()), Times.Never);
        _offers.Verify(r => r.CreateAsync(It.IsAny<StoreOffer>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenOfferDoesNotExist_ShouldPublishMatchingEvent()
    {
        var request = new CreateOfferRequest(MercadoLivreUrl, 100m);

        var result = await _useCase.ExecuteAsync("user-1", request);

        _matchingEventPublisher.Verify(p => p.PublishAsync(result.OfferId, result.ProductId), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenOfferAlreadyExists_ShouldNotPublishMatchingEventAgain()
    {
        var existingProduct = Product.Create("iPhone 15 128GB");
        var existingOffer = StoreOffer.Create(existingProduct.Id, ProductSource.MercadoLivre, MercadoLivreUrl);

        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, MercadoLivreUrl))
            .ReturnsAsync(existingOffer);
        _products.Setup(r => r.GetByIdAsync(existingProduct.Id)).ReturnsAsync(existingProduct);

        var request = new CreateOfferRequest(MercadoLivreUrl, 100m);

        await _useCase.ExecuteAsync("user-1", request);

        _matchingEventPublisher.Verify(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenSubscriptionAlreadyExists_ShouldUpdateTargetPriceInsteadOfCreating()
    {
        var existingProduct = Product.Create("iPhone 15 128GB");
        var existingOffer = StoreOffer.Create(existingProduct.Id, ProductSource.MercadoLivre, MercadoLivreUrl);
        var existingSubscription = UserProductSubscription.Create("user-1", existingProduct.Id, 200m);

        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, MercadoLivreUrl))
            .ReturnsAsync(existingOffer);
        _products.Setup(r => r.GetByIdAsync(existingProduct.Id)).ReturnsAsync(existingProduct);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync("user-1", existingProduct.Id))
            .ReturnsAsync(existingSubscription);

        var request = new CreateOfferRequest(MercadoLivreUrl, 90m);

        var result = await _useCase.ExecuteAsync("user-1", request);

        result.TargetPrice.Should().Be(90m);
        _subscriptions.Verify(r => r.CreateAsync(It.IsAny<UserProductSubscription>()), Times.Never);
        _subscriptions.Verify(r => r.UpdateAsync(It.Is<UserProductSubscription>(s => s.TargetPrice == 90m)), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldReturnTheSubscriptionListId()
    {
        var result = await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m, "list-1"));

        result.ListId.Should().Be("list-1");
    }

    [Fact]
    public async Task Execute_WhenAnInactiveOfferGetsANewSubscriber_ReactivatesTheOffer()
    {
        var existingProduct = Product.Create("iPhone 15 128GB");
        var inactiveOffer = StoreOffer.Create(existingProduct.Id, ProductSource.MercadoLivre, MercadoLivreUrl);
        inactiveOffer.Deactivate();

        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, MercadoLivreUrl)).ReturnsAsync(inactiveOffer);
        _offers.Setup(r => r.GetByProductIdAsync(existingProduct.Id)).ReturnsAsync(new[] { inactiveOffer });
        _products.Setup(r => r.GetByIdAsync(existingProduct.Id)).ReturnsAsync(existingProduct);
        _subscriptions.Setup(r => r.GetByProductIdAsync(existingProduct.Id))
            .ReturnsAsync(new[] { UserProductSubscription.Create("user-1", existingProduct.Id, 90m) });

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 90m));

        inactiveOffer.IsActive.Should().BeTrue();
        _offers.Verify(r => r.UpdateAsync(inactiveOffer), Times.Once);
    }

    [Fact]
    public async Task Execute_ClientDataFillsBrandAndModelOnlyWhenTheStoreDidNotProvideThem()
    {
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProductFetchResult(150m, "iPhone 15 128GB", Brand: "Apple"));
        var clientData = new ClientProductData(Brand: "OutraMarca", Model: "  A3092  ");

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m, ClientData: clientData));

        _products.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Brand == "Apple" && p.Model == "A3092")), Times.Once);
    }

    [Fact]
    public async Task Execute_ClientDataNeverOverridesTheStorePriceNameOrGtin()
    {
        var clientData = new ClientProductData(Brand: "Marca", Model: "Modelo");

        var result = await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m, ClientData: clientData));

        result.Name.Should().Be("iPhone 15 128GB");
        result.CurrentPrice.Should().Be(150m);
        _products.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Gtin == "00190199719729")), Times.Once);
    }

    [Fact]
    public async Task Execute_ClientDataCannotInjectAGtinIntoTheSharedCatalog()
    {
        // a loja não devolveu GTIN: nada vindo do cliente pode preencher a chave de matching
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>())).ReturnsAsync(new ProductFetchResult(150m, "Produto"));
        var clientData = new ClientProductData(Brand: "Marca", Model: "Modelo");

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m, ClientData: clientData));

        _products.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Gtin == null)), Times.Once);
    }

    [Fact]
    public async Task Execute_OverlongClientBrandIsIgnored()
    {
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>())).ReturnsAsync(new ProductFetchResult(150m, "Produto"));
        var clientData = new ClientProductData(Brand: new string('x', 500));

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m, ClientData: clientData));

        _products.Verify(r => r.CreateAsync(It.Is<Product>(p => p.Brand == null)), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenANetworkIsAvailable_StoresTheAffiliateLinkOnTheNewOffer()
    {
        _affiliateGenerator.Setup(g => g.IsAvailable).Returns(true);
        _affiliateGenerator.Setup(g => g.GenerateAsync(MercadoLivreUrl)).ReturnsAsync("https://afiliado.com/?u=abc");

        var result = await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m));

        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.AffiliateUrl == "https://afiliado.com/?u=abc")), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenNoNetworkIsAvailable_LeavesTheAffiliateLinkEmptyForALaterRedirect()
    {
        _affiliateGenerator.Setup(g => g.IsAvailable).Returns(false);

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m));

        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.AffiliateUrl == null)), Times.Once);
        _affiliateGenerator.Verify(g => g.GenerateAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenTheNetworkFailsAndEchoesTheUrl_DoesNotCacheItAsAnAffiliateLink()
    {
        _affiliateGenerator.Setup(g => g.IsAvailable).Returns(true);
        _affiliateGenerator.Setup(g => g.GenerateAsync(MercadoLivreUrl)).ReturnsAsync(MercadoLivreUrl);

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m));

        _offers.Verify(r => r.CreateAsync(It.Is<StoreOffer>(o => o.AffiliateUrl == null)), Times.Once);
    }

    [Fact]
    public async Task Execute_ForAnExistingOfferWithoutAffiliateLink_GeneratesAndSavesIt()
    {
        var existingProduct = Product.Create("iPhone 15 128GB");
        var existingOffer = StoreOffer.Create(existingProduct.Id, ProductSource.MercadoLivre, MercadoLivreUrl);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, MercadoLivreUrl)).ReturnsAsync(existingOffer);
        _products.Setup(r => r.GetByIdAsync(existingProduct.Id)).ReturnsAsync(existingProduct);
        _affiliateGenerator.Setup(g => g.IsAvailable).Returns(true);
        _affiliateGenerator.Setup(g => g.GenerateAsync(MercadoLivreUrl)).ReturnsAsync("https://afiliado.com/?u=abc");

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest(MercadoLivreUrl, 100m));

        existingOffer.AffiliateUrl.Should().Be("https://afiliado.com/?u=abc");
        _offers.Verify(r => r.UpdateAsync(existingOffer), Times.AtLeastOnce);
    }
}
