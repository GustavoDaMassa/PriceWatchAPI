using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;

namespace PriceWatch.UnitTests.Application.UseCases.ProductList;

public class CreateListUseCaseTests
{
    private readonly Mock<IProductListRepository> _repo = new();
    private readonly CreateListUseCase _useCase;

    public CreateListUseCaseTests()
    {
        _useCase = new CreateListUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_ShouldCreateListAndReturnResponse()
    {
        var request = new CreateProductListRequest("My List", "A description");

        var result = await _useCase.ExecuteAsync("user-1", request);

        result.Should().NotBeNull();
        result.Name.Should().Be("My List");
        result.Description.Should().Be("A description");
        result.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Execute_ShouldCallRepositoryCreate()
    {
        var request = new CreateProductListRequest("My List", null);

        await _useCase.ExecuteAsync("user-1", request);

        _repo.Verify(r => r.CreateAsync(
            It.Is<DomainProductList>(l => l.Name == "My List" && l.UserId == "user-1")),
            Times.Once);
    }
}
