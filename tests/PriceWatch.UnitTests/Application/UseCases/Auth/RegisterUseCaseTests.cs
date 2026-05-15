using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Auth;
using PriceWatch.Application.UseCases.Auth;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Auth;

public class RegisterUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly RegisterUseCase _useCase;

    public RegisterUseCaseTests()
    {
        _useCase = new RegisterUseCase(_userRepo.Object, _hasher.Object, _emailSender.Object);
    }

    [Fact]
    public async Task Execute_WithNewEmail_ShouldCreateUserAndSendVerificationEmail()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("new@test.com")).ReturnsAsync((User?)null);
        _hasher.Setup(h => h.Hash("pass123")).Returns("hashed");

        await _useCase.ExecuteAsync(new RegisterRequest("Test", "new@test.com", "pass123"));

        _userRepo.Verify(r => r.CreateAsync(It.Is<User>(u => u.Email == "new@test.com")), Times.Once);
        _emailSender.Verify(
            s => s.SendVerificationEmailAsync("new@test.com", "Test", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_WithExistingEmail_ShouldThrowBusinessException()
    {
        var existing = User.Create("Existing", "dup@test.com", "h", "t");
        _userRepo.Setup(r => r.GetByEmailAsync("dup@test.com")).ReturnsAsync(existing);

        var act = async () => await _useCase.ExecuteAsync(
            new RegisterRequest("New", "dup@test.com", "pass"));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*already in use*");
    }
}
