using PriceWatch.Application.UseCases.Offers;
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
    private readonly IUserProductSubscriptionRepository _subscriptionRepository;
    private readonly SyncOfferActivityUseCase _syncOfferActivity;
    private readonly IClickLogRepository _clickLogRepository;
    private readonly IOfferMatchFeedbackRepository _feedbackRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public DeleteAccountUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IProductListRepository listRepository,
        ITrackedProductRepository productRepository,
        IPriceSnapshotRepository snapshotRepository,
        INotificationRepository notificationRepository,
        IUserProductSubscriptionRepository subscriptionRepository,
        SyncOfferActivityUseCase syncOfferActivity,
        IClickLogRepository clickLogRepository,
        IOfferMatchFeedbackRepository feedbackRepository,
        IDeviceTokenRepository deviceTokenRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _listRepository = listRepository;
        _productRepository = productRepository;
        _snapshotRepository = snapshotRepository;
        _notificationRepository = notificationRepository;
        _subscriptionRepository = subscriptionRepository;
        _syncOfferActivity = syncOfferActivity;
        _clickLogRepository = clickLogRepository;
        _feedbackRepository = feedbackRepository;
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task ExecuteAsync(string userId, string password)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException(userId);

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            throw new BusinessException("Current password is incorrect.");

        // delete snapshots and products for the user, then lists, notifications, user
        var products = await _productRepository.GetByUserIdAsync(userId);
        foreach (var product in products)
            await _snapshotRepository.DeleteByProductIdAsync(product.Id);

        var productIds = products.Select(p => p.Id).ToList();
        foreach (var id in productIds)
            await _productRepository.DeleteAsync(id);

        // catálogo unificado: apaga as assinaturas do usuário (o catálogo é compartilhado, então
        // produtos e histórico de preço ficam) e pausa as ofertas que ficaram sem assinante
        var productIdsWithSubscription = (await _subscriptionRepository.GetByUserIdAsync(userId))
            .Select(s => s.ProductId)
            .Distinct()
            .ToList();
        await _subscriptionRepository.DeleteByUserIdAsync(userId);
        foreach (var catalogProductId in productIdsWithSubscription)
            await _syncOfferActivity.ExecuteAsync(catalogProductId);

        await _listRepository.DeleteByUserIdAsync(userId);
        await _notificationRepository.DeleteByUserIdAsync(userId);
        await _clickLogRepository.DeleteByUserIdAsync(userId);
        await _feedbackRepository.DeleteByUserIdAsync(userId);
        await _deviceTokenRepository.DeleteByUserIdAsync(userId);
        await _userRepository.DeleteAsync(userId);
    }
}
