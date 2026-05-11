using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;

namespace PriceWatch.UnitTests.Application.UseCases.ProductList;

public class DeleteListUseCaseTests
{
    private readonly Mock<IProductListRepository> _listRepo = new();
    private readonly Mock<ITrackedProductRepository> _productRepo = new();
    private readonly DeleteListUseCase _useCase;

    public DeleteListUseCaseTests()
    {
        _useCase = new DeleteListUseCase(_listRepo.Object, _productRepo.Object);
    }

    [Fact]
    public async Task Execute_WithValidOwnership_ShouldUnlinkProductsAndDeleteList()
    {
        var list = DomainProductList.Create("user-1", "My List", null);
        _listRepo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        await _useCase.ExecuteAsync(list.Id, "user-1");

        _productRepo.Verify(r => r.UnlinkByListIdAsync(list.Id), Times.Once);
        _listRepo.Verify(r => r.DeleteAsync(list.Id), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenNotOwner_ShouldThrowProductListNotFoundException()
    {
        var list = DomainProductList.Create("other-user", "My List", null);
        _listRepo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        var act = async () => await _useCase.ExecuteAsync(list.Id, "user-1");

        await act.Should().ThrowAsync<ProductListNotFoundException>();
    }
}
