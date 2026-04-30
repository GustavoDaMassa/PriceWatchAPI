using FluentAssertions;
using PriceWatch.Domain.Exceptions;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Exceptions;

public class ExceptionHierarchyTests
{
    private class ConcreteNotFoundException : NotFoundException
    {
        public ConcreteNotFoundException() : base("not found") { }
    }

    [Fact]
    public void NotFoundException_IsAbstract()
    {
        typeof(NotFoundException).IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void NotFoundException_InheritsFromException()
    {
        typeof(NotFoundException).BaseType.Should().Be(typeof(Exception));
    }

    [Fact]
    public void ConcreteNotFoundException_CanBeInstantiated()
    {
        var ex = new ConcreteNotFoundException();
        ex.Should().BeAssignableTo<NotFoundException>();
        ex.Message.Should().Be("not found");
    }

    [Fact]
    public void BusinessException_InheritsFromException()
    {
        new BusinessException("error").Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void BusinessException_SetsMessage()
    {
        var ex = new BusinessException("test message");
        ex.Message.Should().Be("test message");
    }
}
