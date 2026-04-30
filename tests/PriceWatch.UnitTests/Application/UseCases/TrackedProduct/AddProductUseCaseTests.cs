using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.UnitTests.Application.UseCases.TrackedProduct;

public class AddProductUseCaseTests
{
    private readonly Mock<ITrackedProductRepository> _productRepo = new();
    private readonly Mock<IPriceFetcherResolver> _resolver = new();
    private readonly Mock<IPriceFetcher> _fetcher = new();
    private readonly AddProductUseCase _useCase;

    public AddProductUseCaseTests()
    {
        _resolver.Setup(r => r.Resolve(It.IsAny<ProductSource>())).Returns(_fetcher.Object);
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>())).ReturnsAsync(150m);
        _useCase = new AddProductUseCase(_productRepo.Object, _resolver.Object);
    }

    [Fact]
    public async Task Execute_ShouldFetchInitialPriceAndCreateProduct()
    {
        var request = new AddProductRequest("list-1", "http://example.com", ProductSource.Manual, "My Product", 100m);

        var result = await _useCase.ExecuteAsync("user-1", request);

        result.Should().NotBeNull();
        result.Name.Should().Be("My Product");
        result.CurrentPrice.Should().Be(150m);
    }

    [Fact]
    public async Task Execute_ShouldCallRecordPrice()
    {
        var request = new AddProductRequest("list-1", "http://example.com", ProductSource.Manual, "My Product", 100m);

        await _useCase.ExecuteAsync("user-1", request);

        _productRepo.Verify(r => r.CreateAsync(
            It.Is<DomainTrackedProduct>(p => p.CurrentPrice == 150m && p.LowestPrice == 150m)),
            Times.Once);
    }
}
