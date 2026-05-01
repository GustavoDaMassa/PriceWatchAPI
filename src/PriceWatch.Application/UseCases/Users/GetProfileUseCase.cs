using PriceWatch.Application.DTOs.Users;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Users;

public class GetProfileUseCase
{
    private readonly IUserRepository _userRepository;

    public GetProfileUseCase(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileResponse> ExecuteAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException(userId);

        return new UserProfileResponse(user.Id, user.Name, user.Email, user.IsEmailVerified, user.CreatedAt);
    }
}
