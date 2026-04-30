using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.Notification;

public record NotificationResponse(
    string Id,
    string ProductId,
    string ProductName,
    NotificationType Type,
    string Message,
    bool IsRead,
    DateTime CreatedAt);
