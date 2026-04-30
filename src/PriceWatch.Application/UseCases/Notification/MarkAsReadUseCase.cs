using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Notification;

public class MarkAsReadUseCase
{
    private readonly INotificationRepository _repository;

    public MarkAsReadUseCase(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(string notificationId, string userId)
    {
        var notification = await _repository.GetByIdAsync(notificationId);
        if (notification is null || notification.UserId != userId)
            throw new NotificationNotFoundException(notificationId);

        notification.MarkAsRead();
        await _repository.UpdateAsync(notification);
    }
}
