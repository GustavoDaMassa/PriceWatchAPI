using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.UnitTests.Application.UseCases.TrackedProduct;

public class GetListAnalysisUseCaseTests
{
    private readonly Mock<IProductListRepository> _listRepo = new();
    private readonly Mock<ITrackedProductRepository> _productRepo = new();
    private readonly GetListAnalysisUseCase _useCase;

    public GetListAnalysisUseCaseTests()
    {
        _useCase = new GetListAnalysisUseCase(_listRepo.Object, _productRepo.Object);
    }

    [Fact]
    public async Task Execute_ShouldReturnProductsOrderedByDistancePercent()
    {
        var list = DomainProductList.Create("user-1", "My List", null);
        _listRepo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        // Produto A: currentPrice=80, targetPrice=100 → distance = (80-100)/100*100 = -20%
        var prodA = DomainTrackedProduct.Create(list.Id, "user-1", "http://a.com", ProductSource.Manual, "Prod A", 100m);
        prodA.RecordPrice(80m);

        // Produto B: currentPrice=150, targetPrice=100 → distance = (150-100)/100*100 = 50%
        var prodB = DomainTrackedProduct.Create(list.Id, "user-1", "http://b.com", ProductSource.Manual, "Prod B", 100m);
        prodB.RecordPrice(150m);

        _productRepo.Setup(r => r.GetByListIdAsync(list.Id)).ReturnsAsync(new[] { prodB, prodA });

        var result = (await _useCase.ExecuteAsync(list.Id, "user-1")).ToList();

        result.Should().HaveCount(2);
        result[0].DistancePercent.Should().BeLessThan(result[1].DistancePercent);
        result[0].ProductName.Should().Be("Prod A");
    }
}
