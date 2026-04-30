using FluentAssertions;
using PriceWatch.Domain.Entities;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class ProductListTests
{
    [Fact]
    public void Create_ShouldSetUserIdAndCreatedAt()
    {
        var list = ProductList.Create("user-1", "My List", null);

        list.UserId.Should().Be("user-1");
        list.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        list.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_ShouldSetNameAndDescription()
    {
        var list = ProductList.Create("user-1", "My List", "A description");

        list.Name.Should().Be("My List");
        list.Description.Should().Be("A description");
    }

    [Fact]
    public void Update_ShouldChangeNameAndDescription()
    {
        var list = ProductList.Create("user-1", "Old Name", "Old desc");

        list.Update("New Name", "New desc");

        list.Name.Should().Be("New Name");
        list.Description.Should().Be("New desc");
    }
}
