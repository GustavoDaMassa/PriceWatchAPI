using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Notification;

public class MarkAllAsReadUseCase
{
    private readonly INotificationRepository _repository;

    public MarkAllAsReadUseCase(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(string userId)
    {
        var notifications = await _repository.GetByUserIdAsync(userId, isRead: false);

        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
            await _repository.UpdateAsync(notification);
        }
    }
}
