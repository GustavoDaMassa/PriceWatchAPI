using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class UserProductSubscriptionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("userId")]
    public string UserId { get; set; } = default!;

    [BsonElement("productId")]
    public string ProductId { get; set; } = default!;

    [BsonElement("listId")]
    public string? ListId { get; set; }

    [BsonElement("targetPrice")]
    public decimal TargetPrice { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("targetAlertSent")]
    public bool TargetAlertSent { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
