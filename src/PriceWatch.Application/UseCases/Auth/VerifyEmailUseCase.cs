using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Auth;

public class VerifyEmailUseCase
{
    private readonly IUserRepository _userRepository;

    public VerifyEmailUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task ExecuteAsync(string email, string token)
    {
        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new UserNotFoundException(email);

        user.VerifyEmail(token);
        await _userRepository.UpdateAsync(user);
    }
}
