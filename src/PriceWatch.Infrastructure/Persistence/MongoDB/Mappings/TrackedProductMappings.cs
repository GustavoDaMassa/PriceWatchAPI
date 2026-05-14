using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class TrackedProductMappings
{
    public static TrackedProductDocument ToDocument(TrackedProduct product) => new()
    {
        Id = product.Id,
        ListId = product.ListId,
        UserId = product.UserId,
        Url = product.Url,
        Source = product.Source,
        Name = product.Name,
        ImageUrl = product.ImageUrl,
        TargetPrice = product.TargetPrice,
        CurrentPrice = product.CurrentPrice,
        LowestPrice = product.LowestPrice,
        CheckIntervalHours = product.CheckIntervalHours,
        NextCheckAt = product.NextCheckAt,
        LastCheckedAt = product.LastCheckedAt,
        IsActive = product.IsActive,
        TargetAlertSent = product.TargetAlertSent,
        Metadata = product.Metadata,
        CreatedAt = product.CreatedAt
    };

    public static TrackedProduct ToDomain(TrackedProductDocument doc) => TrackedProduct.Restore(
        doc.Id,
        doc.ListId,
        doc.UserId,
        doc.Url,
        doc.Source,
        doc.Name,
        doc.ImageUrl,
        doc.TargetPrice,
        doc.CurrentPrice,
        doc.LowestPrice,
        doc.CheckIntervalHours,
        doc.NextCheckAt,
        doc.LastCheckedAt,
        doc.IsActive,
        doc.TargetAlertSent,
        doc.Metadata,
        doc.CreatedAt);
}
