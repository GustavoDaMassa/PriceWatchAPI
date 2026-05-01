using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Users;

public class DeleteAccountUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IProductListRepository _listRepository;
    private readonly ITrackedProductRepository _productRepository;
    private readonly IPriceSnapshotRepository _snapshotRepository;
    private readonly INotificationRepository _notificationRepository;

    public DeleteAccountUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IProductListRepository listRepository,
        ITrackedProductRepository productRepository,
        IPriceSnapshotRepository snapshotRepository,
        INotificationRepository notificationRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _listRepository = listRepository;
        _productRepository = productRepository;
        _snapshotRepository = snapshotRepository;
        _notificationRepository = notificationRepository;
    }

    public async Task ExecuteAsync(string userId, string password)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException(userId);

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new BusinessException("Current password is incorrect.");

        // delete snapshots → products → lists → notifications → user
        var lists = await _listRepository.GetByUserIdAsync(userId);
        foreach (var list in lists)
        {
            var products = await _productRepository.GetByListIdAsync(list.Id);
            foreach (var product in products)
                await _snapshotRepository.DeleteByProductIdAsync(product.Id);

            await _productRepository.DeleteByListIdAsync(list.Id);
        }

        await _listRepository.DeleteByUserIdAsync(userId);
        await _notificationRepository.DeleteByUserIdAsync(userId);
        await _userRepository.DeleteAsync(userId);
    }
}
