using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class ClickLogMappings
{
    public static ClickLogDocument ToDocument(ClickLog click) => new()
    {
        Id = click.Id,
        OfferId = click.OfferId,
        UserId = click.UserId,
        CreatedAt = click.CreatedAt
    };

    public static ClickLog ToDomain(ClickLogDocument doc) =>
        ClickLog.Restore(doc.Id, doc.OfferId, doc.UserId, doc.CreatedAt);
}
