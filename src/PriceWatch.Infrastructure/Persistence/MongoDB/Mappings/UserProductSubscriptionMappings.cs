using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class UserProductSubscriptionMappings
{
    public static UserProductSubscriptionDocument ToDocument(UserProductSubscription subscription) => new()
    {
        Id = subscription.Id,
        UserId = subscription.UserId,
        ProductId = subscription.ProductId,
        ListId = subscription.ListId,
        TargetPrice = subscription.TargetPrice,
        IsActive = subscription.IsActive,
        TargetAlertSent = subscription.TargetAlertSent,
        CreatedAt = subscription.CreatedAt
    };

    public static UserProductSubscription ToDomain(UserProductSubscriptionDocument doc) => UserProductSubscription.Restore(
        doc.Id,
        doc.UserId,
        doc.ProductId,
        doc.ListId,
        doc.TargetPrice,
        doc.IsActive,
        doc.TargetAlertSent,
        doc.CreatedAt);
}
