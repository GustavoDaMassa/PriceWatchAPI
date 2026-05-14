using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class TrackedProductDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("listId")]
    public string? ListId { get; set; }

    [BsonElement("userId")]
    public string UserId { get; set; } = default!;

    [BsonElement("url")]
    public string Url { get; set; } = default!;

    [BsonElement("source")]
    public ProductSource Source { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = default!;

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("targetPrice")]
    public decimal TargetPrice { get; set; }

    [BsonElement("currentPrice")]
    public decimal CurrentPrice { get; set; }

    [BsonElement("lowestPrice")]
    public decimal LowestPrice { get; set; }

    [BsonElement("checkIntervalHours")]
    public int CheckIntervalHours { get; set; }

    [BsonElement("nextCheckAt")]
    public DateTime NextCheckAt { get; set; }

    [BsonElement("lastCheckedAt")]
    public DateTime? LastCheckedAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("targetAlertSent")]
    public bool TargetAlertSent { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
