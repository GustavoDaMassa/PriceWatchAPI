using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Users;

public class GetProfileUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly GetProfileUseCase _useCase;

    public GetProfileUseCaseTests()
    {
        _useCase = new GetProfileUseCase(_userRepo.Object);
    }

    [Fact]
    public async Task Execute_WithValidId_ShouldReturnProfile()
    {
        var user = User.Create("Gustavo", "gustavo@test.com", "hash", "tok");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var result = await _useCase.ExecuteAsync(user.Id);

        result.Name.Should().Be("Gustavo");
        result.Email.Should().Be("gustavo@test.com");
    }

    [Fact]
    public async Task Execute_WithUnknownId_ShouldThrowUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync("unknown")).ReturnsAsync((User?)null);

        var act = async () => await _useCase.ExecuteAsync("unknown");

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
