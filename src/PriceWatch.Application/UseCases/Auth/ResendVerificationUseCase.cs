using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Auth;

public class ResendVerificationUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;

    public ResendVerificationUseCase(IUserRepository userRepository, IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new UserNotFoundException(email);

        var newToken = Guid.NewGuid().ToString();
        user.RegenerateVerificationToken(newToken);

        await _userRepository.UpdateAsync(user);
        await _emailSender.SendVerificationEmailAsync(user.Email, user.Name, newToken);
    }
}
