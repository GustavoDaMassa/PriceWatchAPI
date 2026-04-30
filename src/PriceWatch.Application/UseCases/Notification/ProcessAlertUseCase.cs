using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Application.UseCases.Notification;

public class ProcessAlertUseCase
{
    private readonly INotificationRepository _repository;
    private readonly IEmailSender _emailSender;

    public ProcessAlertUseCase(INotificationRepository repository, IEmailSender emailSender)
    {
        _repository = repository;
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(
        string userId,
        string productId,
        string productName,
        NotificationType type,
        decimal currentPrice,
        string userEmail)
    {
        var notification = Domain.Entities.Notification.Create(userId, productId, productName, type, currentPrice);
        await _repository.CreateAsync(notification);

        var subject = type == NotificationType.TargetPriceReached
            ? $"PriceWatch: '{productName}' reached your target price!"
            : $"PriceWatch: New lowest price for '{productName}'!";

        await _emailSender.SendAlertEmailAsync(userEmail, subject, notification.Message);
    }
}
