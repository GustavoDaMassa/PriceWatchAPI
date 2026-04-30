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
    private readonly Mock<IProductListRepository> _repo = new();
    private readonly DeleteListUseCase _useCase;

    public DeleteListUseCaseTests()
    {
        _useCase = new DeleteListUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_WithValidOwnership_ShouldDeleteList()
    {
        var list = DomainProductList.Create("user-1", "My List", null);
        _repo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        await _useCase.ExecuteAsync(list.Id, "user-1");

        _repo.Verify(r => r.DeleteAsync(list.Id), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenNotOwner_ShouldThrowProductListNotFoundException()
    {
        var list = DomainProductList.Create("other-user", "My List", null);
        _repo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        var act = async () => await _useCase.ExecuteAsync(list.Id, "user-1");

        await act.Should().ThrowAsync<ProductListNotFoundException>();
    }
}
