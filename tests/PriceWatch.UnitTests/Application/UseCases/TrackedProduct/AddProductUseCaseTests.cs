using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using Xunit;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.UnitTests.Application.UseCases.TrackedProduct;

public class AddProductUseCaseTests
{
    private readonly Mock<ITrackedProductRepository> _productRepo = new();
    private readonly Mock<IPriceFetcherResolver> _resolver = new();
    private readonly Mock<IPriceFetcher> _fetcher = new();
    private readonly AddProductUseCase _useCase;

    private const string MercadoLivreUrl = "https://produto.mercadolivre.com.br/MLB-123-titulo-_JM";

    public AddProductUseCaseTests()
    {
        _resolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_fetcher.Object);
        _fetcher.Setup(f => f.FetchAsync(It.IsAny<string>()))
            .ReturnsAsync(new ProductFetchResult(150m, "Produto Teste"));
        _fetcher.Setup(f => f.ProductSource).Returns(PriceWatch.Domain.Enums.ProductSource.MercadoLivre);
        _useCase = new AddProductUseCase(_productRepo.Object, _resolver.Object);
    }

    [Fact]
    public async Task Execute_ShouldFetchPriceAndNameFromApi()
    {
        var request = new AddProductRequest(MercadoLivreUrl, 100m, "list-1");

        var result = await _useCase.ExecuteAsync("user-1", request);

        result.Should().NotBeNull();
        result.Name.Should().Be("Produto Teste");
        result.CurrentPrice.Should().Be(150m);
    }

    [Fact]
    public async Task Execute_ShouldResolveByUrl()
    {
        var request = new AddProductRequest(MercadoLivreUrl, 100m);

        await _useCase.ExecuteAsync("user-1", request);

        _resolver.Verify(r => r.Resolve(MercadoLivreUrl), Times.Once);
    }

    [Fact]
    public async Task Execute_WithListId_ShouldPersistWithListId()
    {
        var request = new AddProductRequest(MercadoLivreUrl, 100m, "list-1");

        await _useCase.ExecuteAsync("user-1", request);

        _productRepo.Verify(r => r.CreateAsync(
            It.Is<DomainTrackedProduct>(p =>
                p.ListId == "list-1" &&
                p.UserId == "user-1" &&
                p.CurrentPrice == 150m &&
                p.LowestPrice == 150m)),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithoutListId_ShouldPersistWithNullListId()
    {
        var request = new AddProductRequest(MercadoLivreUrl, 100m);

        await _useCase.ExecuteAsync("user-1", request);

        _productRepo.Verify(r => r.CreateAsync(
            It.Is<DomainTrackedProduct>(p => p.ListId == null && p.UserId == "user-1")),
            Times.Once);
    }
}
