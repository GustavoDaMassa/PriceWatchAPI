using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Auth;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Auth;

public class VerifyEmailUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly VerifyEmailUseCase _useCase;

    public VerifyEmailUseCaseTests()
    {
        _useCase = new VerifyEmailUseCase(_userRepo.Object);
    }

    [Fact]
    public async Task Execute_WithValidToken_ShouldVerifyEmailAndSaveUser()
    {
        var user = User.Create("A", "a@test.com", "h", "valid-token");
        _userRepo.Setup(r => r.GetByEmailAsync("a@test.com")).ReturnsAsync(user);

        await _useCase.ExecuteAsync("a@test.com", "valid-token");

        user.IsEmailVerified.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Execute_WithUnknownEmail_ShouldThrowUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("x@test.com")).ReturnsAsync((User?)null);

        var act = async () => await _useCase.ExecuteAsync("x@test.com", "tok");

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Execute_WithWrongToken_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "h", "correct-token");
        _userRepo.Setup(r => r.GetByEmailAsync("a@test.com")).ReturnsAsync(user);

        var act = async () => await _useCase.ExecuteAsync("a@test.com", "wrong-token");

        await act.Should().ThrowAsync<BusinessException>();
    }
}
