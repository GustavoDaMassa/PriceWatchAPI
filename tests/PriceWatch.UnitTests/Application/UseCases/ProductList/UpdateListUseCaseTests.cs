using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;

namespace PriceWatch.UnitTests.Application.UseCases.ProductList;

public class UpdateListUseCaseTests
{
    private readonly Mock<IProductListRepository> _repo = new();
    private readonly UpdateListUseCase _useCase;

    public UpdateListUseCaseTests()
    {
        _useCase = new UpdateListUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_WithValidOwnership_ShouldUpdateList()
    {
        var list = DomainProductList.Create("user-1", "Old Name", null);
        _repo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        await _useCase.ExecuteAsync(list.Id, "user-1", new UpdateProductListRequest("New Name", "New desc"));

        _repo.Verify(r => r.UpdateAsync(It.Is<DomainProductList>(l => l.Name == "New Name")), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenListNotFound_ShouldThrowProductListNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync("non-existent")).ReturnsAsync((DomainProductList?)null);

        var act = async () => await _useCase.ExecuteAsync(
            "non-existent", "user-1", new UpdateProductListRequest("Name", null));

        await act.Should().ThrowAsync<ProductListNotFoundException>();
    }

    [Fact]
    public async Task Execute_WhenListBelongsToOtherUser_ShouldThrowProductListNotFoundException()
    {
        var list = DomainProductList.Create("other-user", "Name", null);
        _repo.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        var act = async () => await _useCase.ExecuteAsync(
            list.Id, "user-1", new UpdateProductListRequest("Name", null));

        await act.Should().ThrowAsync<ProductListNotFoundException>();
    }
}
