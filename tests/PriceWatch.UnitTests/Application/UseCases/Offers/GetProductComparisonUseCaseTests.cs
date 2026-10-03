using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class GetProductComparisonUseCaseTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly GetProductComparisonUseCase _useCase;

    public GetProductComparisonUseCaseTests()
    {
        _useCase = new GetProductComparisonUseCase(_products.Object, _offers.Object);
    }

    [Fact]
    public async Task Execute_WhenProductDoesNotExist_ShouldThrowProductNotFoundException()
    {
        _products.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((Product?)null);

        var act = () => _useCase.ExecuteAsync("missing");

        await act.Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task Execute_ShouldOrderOffersByPriceAscending()
    {
        var product = Product.Create("iPhone 15 128GB");
        var cheaper = StoreOffer.Create(product.Id, ProductSource.Kabum, "https://kabum.com/item");
        cheaper.RecordPrice(100m);
        var pricier = StoreOffer.Create(product.Id, ProductSource.MercadoLivre, "https://ml.com/item");
        pricier.RecordPrice(150m);

        _products.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _offers.Setup(r => r.GetByProductIdAsync(product.Id)).ReturnsAsync([pricier, cheaper]);

        var result = await _useCase.ExecuteAsync(product.Id);

        result.Offers.Should().HaveCount(2);
        result.Offers[0].OfferId.Should().Be(cheaper.Id);
        result.Offers[1].OfferId.Should().Be(pricier.Id);
    }

    [Fact]
    public async Task Execute_ShouldMarkOnlyCheapestOfferAsCheapest()
    {
        var product = Product.Create("iPhone 15 128GB");
        var cheaper = StoreOffer.Create(product.Id, ProductSource.Kabum, "https://kabum.com/item");
        cheaper.RecordPrice(100m);
        var pricier = StoreOffer.Create(product.Id, ProductSource.MercadoLivre, "https://ml.com/item");
        pricier.RecordPrice(150m);

        _products.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _offers.Setup(r => r.GetByProductIdAsync(product.Id)).ReturnsAsync([cheaper, pricier]);

        var result = await _useCase.ExecuteAsync(product.Id);

        result.Offers.Single(o => o.OfferId == cheaper.Id).IsCheapest.Should().BeTrue();
        result.Offers.Single(o => o.OfferId == pricier.Id).IsCheapest.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ShouldExcludeInactiveOffers()
    {
        var product = Product.Create("iPhone 15 128GB");
        var active = StoreOffer.Create(product.Id, ProductSource.Kabum, "https://kabum.com/item");
        active.RecordPrice(100m);
        var inactive = StoreOffer.Create(product.Id, ProductSource.MercadoLivre, "https://ml.com/item");
        inactive.RecordPrice(90m);
        inactive.Deactivate();

        _products.Setup(r => r.GetByIdAsync(product.Id)).ReturnsAsync(product);
        _offers.Setup(r => r.GetByProductIdAsync(product.Id)).ReturnsAsync([active, inactive]);

        var result = await _useCase.ExecuteAsync(product.Id);

        result.Offers.Should().ContainSingle(o => o.OfferId == active.Id);
    }
}
