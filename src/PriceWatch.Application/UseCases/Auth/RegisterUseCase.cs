using PriceWatch.Application.DTOs.Auth;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Auth;

public class RegisterUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;

    public RegisterUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(RegisterRequest request)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing is not null)
            throw new BusinessException("Email already in use.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var verificationToken = Guid.NewGuid().ToString();
        var user = User.Create(request.Name, request.Email, passwordHash, verificationToken);

        await _userRepository.CreateAsync(user);
        await _emailSender.SendVerificationEmailAsync(user.Email, user.Name, verificationToken);
    }
}
