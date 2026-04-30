using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class NotificationMappings
{
    public static NotificationDocument ToDocument(Notification notification) => new()
    {
        Id = notification.Id,
        UserId = notification.UserId,
        ProductId = notification.ProductId,
        ProductName = notification.ProductName,
        Type = notification.Type,
        Message = notification.Message,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt
    };

    public static Notification ToDomain(NotificationDocument doc) => Notification.Restore(
        doc.Id,
        doc.UserId,
        doc.ProductId,
        doc.ProductName,
        doc.Type,
        doc.Message,
        doc.IsRead,
        doc.CreatedAt);
}
