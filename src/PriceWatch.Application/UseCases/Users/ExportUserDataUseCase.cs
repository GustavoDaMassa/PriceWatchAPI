using PriceWatch.Application.DTOs.Users;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Users;

public class ExportUserDataUseCase
{
    private readonly IUserRepository _userRepository;
    private readonly IProductListRepository _listRepository;
    private readonly ITrackedProductRepository _productRepository;
    private readonly IUserProductSubscriptionRepository _subscriptionRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IClickLogRepository _clickLogRepository;
    private readonly IOfferMatchFeedbackRepository _feedbackRepository;
    private readonly IDeviceTokenRepository _deviceTokenRepository;

    public ExportUserDataUseCase(
        IUserRepository userRepository,
        IProductListRepository listRepository,
        ITrackedProductRepository productRepository,
        IUserProductSubscriptionRepository subscriptionRepository,
        INotificationRepository notificationRepository,
        IClickLogRepository clickLogRepository,
        IOfferMatchFeedbackRepository feedbackRepository,
        IDeviceTokenRepository deviceTokenRepository)
    {
        _userRepository = userRepository;
        _listRepository = listRepository;
        _productRepository = productRepository;
        _subscriptionRepository = subscriptionRepository;
        _notificationRepository = notificationRepository;
        _clickLogRepository = clickLogRepository;
        _feedbackRepository = feedbackRepository;
        _deviceTokenRepository = deviceTokenRepository;
    }

    public async Task<UserDataExport> ExecuteAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException(userId);

        var lists = await _listRepository.GetByUserIdAsync(userId);
        var products = await _productRepository.GetByUserIdAsync(userId);
        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId);
        var notifications = await _notificationRepository.GetByUserIdAsync(userId);
        var clicks = await _clickLogRepository.GetByUserIdAsync(userId);
        var feedback = await _feedbackRepository.GetByUserIdAsync(userId);
        var devices = await _deviceTokenRepository.GetByUserIdAsync(userId);

        return new UserDataExport(
            DateTime.UtcNow,
            new UserProfileResponse(user.Id, user.Name, user.Email, user.IsEmailVerified, user.CreatedAt),
            (lists ?? []).Select(l => new ExportedList(l.Id, l.Name, l.Description, l.CreatedAt)).ToList(),
            (products ?? []).Select(p => new ExportedTrackedProduct(
                p.Id, p.Name, p.Url, p.TargetPrice, p.CurrentPrice, p.LowestPrice, p.IsActive, p.CreatedAt)).ToList(),
            (subscriptions ?? []).Select(s => new ExportedSubscription(
                s.Id, s.ProductId, s.ListId, s.TargetPrice, s.IsActive, s.CreatedAt)).ToList(),
            (notifications ?? []).Select(n => new ExportedNotification(
                n.Id, n.ProductId, n.ProductName, n.Message, n.IsRead, n.CreatedAt)).ToList(),
            (clicks ?? []).Select(c => new ExportedClick(c.OfferId, c.CreatedAt)).ToList(),
            (feedback ?? []).Select(f => new ExportedMatchFeedback(
                f.ProductId, f.Url, f.Verdict == MatchVerdict.Same, f.Track, f.CreatedAt)).ToList(),
            (devices ?? []).Select(d => new ExportedDevice(d.Platform, d.CreatedAt, d.LastSeenAt)).ToList());
    }
}
