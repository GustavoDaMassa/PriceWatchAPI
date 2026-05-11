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

public class LoginUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly LoginUseCase _useCase;

    public LoginUseCaseTests()
    {
        _useCase = new LoginUseCase(_userRepo.Object, _hasher.Object, _tokenService.Object);
    }

    [Fact]
    public async Task Execute_WithValidCredentials_ShouldReturnAuthResponse()
    {
        var user = User.Create("Ana", "ana@test.com", "hashed_pw", "tok");
        user.VerifyEmail("tok");

        _userRepo.Setup(r => r.GetByEmailAsync("ana@test.com")).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("pass", "hashed_pw")).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(user)).Returns("jwt-token");

        var result = await _useCase.ExecuteAsync(new LoginRequest("ana@test.com", "pass"));

        result.Token.Should().Be("jwt-token");
        result.Email.Should().Be("ana@test.com");
        result.Name.Should().Be("Ana");
    }

    [Fact]
    public async Task Execute_WithUnknownEmail_ShouldThrowUserNotFoundException()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("unknown@test.com")).ReturnsAsync((User?)null);

        var act = async () => await _useCase.ExecuteAsync(
            new LoginRequest("unknown@test.com", "pass"));

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Execute_WithWrongPassword_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "hashed_pw", "tok");
        user.VerifyEmail("tok");

        _userRepo.Setup(r => r.GetByEmailAsync("a@test.com")).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "hashed_pw")).Returns(false);

        var act = async () => await _useCase.ExecuteAsync(new LoginRequest("a@test.com", "wrong"));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*Invalid*");
    }

    [Fact]
    public async Task Execute_WithUnverifiedEmail_ShouldSucceed()
    {
        var user = User.Create("A", "a@test.com", "hashed_pw", "tok");

        _userRepo.Setup(r => r.GetByEmailAsync("a@test.com")).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("pass", "hashed_pw")).Returns(true);
        _tokenService.Setup(t => t.GenerateToken(user)).Returns("jwt-token");

        var result = await _useCase.ExecuteAsync(new LoginRequest("a@test.com", "pass"));

        result.Token.Should().Be("jwt-token");
    }
}
