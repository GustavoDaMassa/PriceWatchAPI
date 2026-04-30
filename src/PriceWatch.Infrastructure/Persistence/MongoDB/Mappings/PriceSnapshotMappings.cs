using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class PriceSnapshotMappings
{
    public static PriceSnapshotDocument ToDocument(PriceSnapshot snapshot) => new()
    {
        Id = snapshot.Id,
        ProductId = snapshot.ProductId,
        Price = snapshot.Price,
        CapturedAt = snapshot.CapturedAt
    };

    public static PriceSnapshot ToDomain(PriceSnapshotDocument doc) => PriceSnapshot.Restore(
        doc.Id,
        doc.ProductId,
        doc.Price,
        doc.CapturedAt);
}
