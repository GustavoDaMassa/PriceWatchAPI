using FluentAssertions;
using PriceWatch.Domain.Services;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Services;

public class ProductSpecsTests
{
    [Fact]
    public void Extract_FindsCapacitiesAndScreenSizes()
    {
        var specs = ProductSpecs.Extract("Apple iPhone 15 128GB Tela 6.1\" Azul");

        specs["gb"].Should().BeEquivalentTo(new[] { "128" });
        specs["in"].Should().BeEquivalentTo(new[] { "6.1" });
    }

    [Theory]
    [InlineData("Notebook 15,6 polegadas", "in", "15.6")]
    [InlineData("Notebook 15.6pol", "in", "15.6")]
    [InlineData("Monitor 27 pol 144Hz", "in", "27")]
    [InlineData("Monitor 27 pol 144Hz", "hz", "144")]
    [InlineData("SSD 1TB NVMe", "tb", "1")]
    [InlineData("Fonte 650W 80 Plus", "w", "650")]
    [InlineData("Bateria 5000mAh", "mah", "5000")]
    [InlineData("Memoria 16 GB DDR5", "gb", "16")]
    [InlineData("Garrafa 500ml", "ml", "500")]
    public void Extract_NormalizesNumbersAndUnits(string title, string unit, string value)
    {
        ProductSpecs.Extract(title)[unit].Should().Contain(value);
    }

    [Fact]
    public void Extract_ReturnsAnEmptyMapWhenThereAreNoMeasurements()
    {
        ProductSpecs.Extract("Mouse Gamer Sem Fio").Should().BeEmpty();
    }

    [Fact]
    public void Extract_KeepsEveryValueOfTheSameUnit()
    {
        var specs = ProductSpecs.Extract("Notebook 16GB RAM 512GB SSD");

        specs["gb"].Should().BeEquivalentTo(new[] { "16", "512" });
    }

    [Fact]
    public void Conflict_IsTrueWhenASharedUnitHasNoCommonValue()
    {
        var a = ProductSpecs.Extract("iPhone 15 128GB");
        var b = ProductSpecs.Extract("iPhone 15 256GB");

        ProductSpecs.Conflict(a, b).Should().BeTrue();
    }

    [Fact]
    public void Conflict_IsFalseWhenSharedUnitsOverlap()
    {
        var a = ProductSpecs.Extract("Notebook 16GB 512GB");
        var b = ProductSpecs.Extract("Notebook 512GB 16GB RAM");

        ProductSpecs.Conflict(a, b).Should().BeFalse();
    }

    [Fact]
    public void Conflict_IsFalseWhenOnlyOneSideHasAUnit()
    {
        var a = ProductSpecs.Extract("Mouse Gamer");
        var b = ProductSpecs.Extract("Mouse Gamer 16000 dpi 65g");

        ProductSpecs.Conflict(a, b).Should().BeFalse();
    }
}
