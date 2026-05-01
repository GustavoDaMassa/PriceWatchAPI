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

public class ChangePasswordUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly ChangePasswordUseCase _useCase;

    public ChangePasswordUseCaseTests()
    {
        _useCase = new ChangePasswordUseCase(_userRepo.Object, _hasher.Object);
    }

    [Fact]
    public async Task Execute_WithValidCurrentPassword_ShouldUpdateHash()
    {
        var user = User.Create("A", "a@test.com", "old-hash", "tok");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("OldPass123", "old-hash")).Returns(true);
        _hasher.Setup(h => h.Hash("NewPass456")).Returns("new-hash");

        await _useCase.ExecuteAsync(user.Id, new ChangePasswordRequest("OldPass123", "NewPass456"));

        _userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => u.PasswordHash == "new-hash")), Times.Once);
    }

    [Fact]
    public async Task Execute_WithWrongCurrentPassword_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "old-hash", "tok");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("WrongPass", "old-hash")).Returns(false);

        var act = async () => await _useCase.ExecuteAsync(
            user.Id, new ChangePasswordRequest("WrongPass", "NewPass456"));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*incorrect*");
    }

    [Fact]
    public async Task Execute_WithUnknownId_ShouldThrowUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetByIdAsync("unknown")).ReturnsAsync((User?)null);

        var act = async () => await _useCase.ExecuteAsync(
            "unknown", new ChangePasswordRequest("any", "any"));

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
