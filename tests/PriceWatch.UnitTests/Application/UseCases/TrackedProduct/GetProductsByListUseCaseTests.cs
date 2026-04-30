using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;

namespace PriceWatch.UnitTests.Application.UseCases.TrackedProduct;

public class GetProductsByListUseCaseTests
{
    private readonly Mock<ITrackedProductRepository> _repo = new();
    private readonly GetProductsByListUseCase _useCase;

    public GetProductsByListUseCaseTests()
    {
        _useCase = new GetProductsByListUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_ShouldReturnProductsForList()
    {
        var products = new List<DomainTrackedProduct>
        {
            DomainTrackedProduct.Create("list-1", "user-1", "http://a.com", ProductSource.Manual, "Prod A", 100m),
            DomainTrackedProduct.Create("list-1", "user-1", "http://b.com", ProductSource.Manual, "Prod B", 200m),
        };
        _repo.Setup(r => r.GetByListIdAsync("list-1")).ReturnsAsync(products);

        var result = await _useCase.ExecuteAsync("list-1");

        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().Contain(new[] { "Prod A", "Prod B" });
    }
}
