using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;

namespace PriceWatch.UnitTests.Application.UseCases.ProductList;

public class GetUserListsUseCaseTests
{
    private readonly Mock<IProductListRepository> _repo = new();
    private readonly GetUserListsUseCase _useCase;

    public GetUserListsUseCaseTests()
    {
        _useCase = new GetUserListsUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_ShouldReturnAllListsForUser()
    {
        var lists = new List<DomainProductList>
        {
            DomainProductList.Create("user-1", "List A", null),
            DomainProductList.Create("user-1", "List B", "desc"),
        };
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(lists);

        var result = await _useCase.ExecuteAsync("user-1");

        result.Should().HaveCount(2);
        result.Select(l => l.Name).Should().Contain(new[] { "List A", "List B" });
    }

    [Fact]
    public async Task Execute_WhenNoLists_ShouldReturnEmpty()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<DomainProductList>());

        var result = await _useCase.ExecuteAsync("user-1");

        result.Should().BeEmpty();
    }
}
