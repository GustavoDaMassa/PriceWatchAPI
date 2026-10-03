using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class DeviceTokenMappings
{
    public static DeviceTokenDocument ToDocument(DeviceToken d) => new()
    {
        Id = d.Id, UserId = d.UserId, Token = d.Token, Platform = d.Platform,
        CreatedAt = d.CreatedAt, LastSeenAt = d.LastSeenAt
    };

    public static DeviceToken ToDomain(DeviceTokenDocument d) =>
        DeviceToken.Restore(d.Id, d.UserId, d.Token, d.Platform, d.CreatedAt, d.LastSeenAt);
}
