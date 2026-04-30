using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Entities;

public class Notification
{
    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string ProductId { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public NotificationType Type { get; private set; }
    public string Message { get; private set; } = default!;
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(
        string userId,
        string productId,
        string productName,
        NotificationType type,
        decimal currentPrice)
    {
        var message = type switch
        {
            NotificationType.TargetPriceReached =>
                $"'{productName}' reached your target price! Current price: {currentPrice:C}",
            NotificationType.NewLowestPrice =>
                $"'{productName}' hit a new lowest price: {currentPrice:C}",
            _ => $"Price update for '{productName}': {currentPrice:C}"
        };

        return new Notification
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            ProductId = productId,
            ProductName = productName,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Notification Restore(
        string id,
        string userId,
        string productId,
        string productName,
        NotificationType type,
        string message,
        bool isRead,
        DateTime createdAt)
    {
        return new Notification
        {
            Id = id,
            UserId = userId,
            ProductId = productId,
            ProductName = productName,
            Type = type,
            Message = message,
            IsRead = isRead,
            CreatedAt = createdAt
        };
    }

    public void MarkAsRead() => IsRead = true;
}
