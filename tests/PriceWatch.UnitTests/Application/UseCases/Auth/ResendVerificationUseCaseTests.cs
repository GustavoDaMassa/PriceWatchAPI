using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Auth;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Auth;

public class ResendVerificationUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly ResendVerificationUseCase _useCase;

    public ResendVerificationUseCaseTests()
    {
        _useCase = new ResendVerificationUseCase(_userRepo.Object, _emailSender.Object);
    }

    [Fact]
    public async Task Execute_WithUnverifiedUser_ShouldRegenerateTokenAndSendEmail()
    {
        var user = User.Create("A", "a@test.com", "h", "old-token");
        _userRepo.Setup(r => r.GetByEmailAsync("a@test.com")).ReturnsAsync(user);

        await _useCase.ExecuteAsync("a@test.com");

        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        _emailSender.Verify(
            s => s.SendVerificationEmailAsync("a@test.com", "A", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithAlreadyVerifiedUser_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "h", "tok");
        user.VerifyEmail("tok");
        _userRepo.Setup(r => r.GetByEmailAsync("a@test.com")).ReturnsAsync(user);

        var act = async () => await _useCase.ExecuteAsync("a@test.com");

        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Execute_WithUnknownEmail_ShouldThrowUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("x@test.com")).ReturnsAsync((User?)null);

        var act = async () => await _useCase.ExecuteAsync("x@test.com");

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
