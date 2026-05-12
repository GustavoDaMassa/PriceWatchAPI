using PriceWatch.Application.DTOs.Users;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Users;

public class ChangeEmailUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;

    public ChangeEmailUseCase(IUserRepository userRepository, IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(string userId, ChangeEmailRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException(userId);

        var existing = await _userRepository.GetByEmailAsync(request.NewEmail);
        if (existing is not null)
            throw new BusinessException("Email already in use.");

        var verificationToken = Guid.NewGuid().ToString();
        user.ChangeEmail(request.NewEmail, verificationToken);

        await _userRepository.UpdateAsync(user);
        await _emailSender.SendVerificationEmailAsync(user.Email, user.Name, verificationToken, user.Locale);
    }
}
