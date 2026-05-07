using PriceWatch.Application.DTOs.Auth;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Auth;

public class LoginUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> ExecuteAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new UserNotFoundException(request.Email);

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new BusinessException("Invalid credentials.");

        var token = _tokenService.GenerateToken(user);
        return new AuthResponse(token, user.Email, user.Name);
    }
}
