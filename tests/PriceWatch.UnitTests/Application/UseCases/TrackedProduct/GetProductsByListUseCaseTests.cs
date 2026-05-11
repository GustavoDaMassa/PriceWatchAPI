using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.UnitTests.Application.UseCases.TrackedProduct;

public class GetUserProductsUseCaseTests
{
    private readonly Mock<ITrackedProductRepository> _repo = new();
    private readonly GetUserProductsUseCase _useCase;

    public GetUserProductsUseCaseTests()
    {
        _useCase = new GetUserProductsUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_WithListId_ShouldReturnFilteredProducts()
    {
        var products = new List<DomainTrackedProduct>
        {
            DomainTrackedProduct.Create("user-1", "http://a.com", ProductSource.Manual, "Prod A", 100m, "list-1"),
            DomainTrackedProduct.Create("user-1", "http://b.com", ProductSource.Manual, "Prod B", 200m, "list-1"),
        };
        _repo.Setup(r => r.GetByUserIdAsync("user-1", "list-1")).ReturnsAsync(products);

        var result = await _useCase.ExecuteAsync("user-1", "list-1");

        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().Contain(new[] { "Prod A", "Prod B" });
    }

    [Fact]
    public async Task Execute_WithoutListId_ShouldReturnAllUserProducts()
    {
        var products = new List<DomainTrackedProduct>
        {
            DomainTrackedProduct.Create("user-1", "http://a.com", ProductSource.Manual, "Prod A", 100m),
            DomainTrackedProduct.Create("user-1", "http://b.com", ProductSource.Manual, "Prod B", 200m, "list-1"),
        };
        _repo.Setup(r => r.GetByUserIdAsync("user-1", null)).ReturnsAsync(products);

        var result = await _useCase.ExecuteAsync("user-1");

        result.Should().HaveCount(2);
    }
}
