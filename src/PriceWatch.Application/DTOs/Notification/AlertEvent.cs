using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.Notification;

public record AlertEvent(
    string ProductId,
    string UserId,
    string ProductName,
    NotificationType Type,
    decimal CurrentPrice,
    string UserEmail);
