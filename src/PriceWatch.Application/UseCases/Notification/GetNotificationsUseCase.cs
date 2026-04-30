using PriceWatch.Application.DTOs.Notification;
using PriceWatch.Domain.Interfaces.Repositories;

namespace PriceWatch.Application.UseCases.Notification;

public class GetNotificationsUseCase
{
    private readonly INotificationRepository _repository;

    public GetNotificationsUseCase(INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<NotificationResponse>> ExecuteAsync(string userId, bool? isRead)
    {
        var notifications = await _repository.GetByUserIdAsync(userId, isRead);
        return notifications.Select(n => new NotificationResponse(
            n.Id, n.ProductId, n.ProductName, n.Type, n.Message, n.IsRead, n.CreatedAt));
    }
}
