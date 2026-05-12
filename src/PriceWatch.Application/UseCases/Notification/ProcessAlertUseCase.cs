using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Notification;

public class ProcessAlertUseCase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;

    public ProcessAlertUseCase(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(
        string userId,
        string productId,
        string productName,
        NotificationType type,
        decimal currentPrice)
    {
        var notification = Domain.Entities.Notification.Create(userId, productId, productName, type, currentPrice);
        await _notificationRepository.CreateAsync(notification);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null || !user.IsEmailVerified)
            return;

        var subject = type == NotificationType.TargetPriceReached
            ? $"PriceWatch: '{productName}' reached your target price!"
            : $"PriceWatch: New lowest price for '{productName}'!";

        await _emailSender.SendAlertEmailAsync(user.Email, subject, notification.Message, user.Locale);
    }
}
