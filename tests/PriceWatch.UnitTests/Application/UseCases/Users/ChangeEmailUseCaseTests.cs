using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Users;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Users;

public class ChangeEmailUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly ChangeEmailUseCase _useCase;

    public ChangeEmailUseCaseTests()
    {
        _useCase = new ChangeEmailUseCase(_userRepo.Object, _emailSender.Object);
    }

    [Fact]
    public async Task Execute_ShouldChangeEmailAndSendVerification()
    {
        var user = User.Create("A", "old@test.com", "hash", "tok");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepo.Setup(r => r.GetByEmailAsync("new@test.com")).ReturnsAsync((User?)null);

        await _useCase.ExecuteAsync(user.Id, new ChangeEmailRequest("new@test.com"));

        user.Email.Should().Be("new@test.com");
        user.IsEmailVerified.Should().BeFalse();
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        _emailSender.Verify(
            s => s.SendVerificationEmailAsync("new@test.com", "A", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithEmailAlreadyInUse_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "old@test.com", "hash", "tok");
        var other = User.Create("B", "taken@test.com", "hash", "tok");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepo.Setup(r => r.GetByEmailAsync("taken@test.com")).ReturnsAsync(other);

        var act = async () => await _useCase.ExecuteAsync(
            user.Id, new ChangeEmailRequest("taken@test.com"));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*already in use*");
    }

    [Fact]
    public async Task Execute_WithUnknownId_ShouldThrowUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync("unknown")).ReturnsAsync((User?)null);

        var act = async () => await _useCase.ExecuteAsync(
            "unknown", new ChangeEmailRequest("x@test.com"));

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
