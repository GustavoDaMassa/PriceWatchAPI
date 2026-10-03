using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class StoreOfferDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("productId")]
    public string ProductId { get; set; } = default!;

    [BsonElement("source")]
    public ProductSource Source { get; set; }

    [BsonElement("url")]
    public string Url { get; set; } = default!;

    [BsonElement("gtin")]
    public string? Gtin { get; set; }

    [BsonElement("currentPrice")]
    public decimal CurrentPrice { get; set; }

    [BsonElement("lowestPrice")]
    public decimal LowestPrice { get; set; }

    [BsonElement("affiliateUrl")]
    public string? AffiliateUrl { get; set; }

    [BsonElement("checkIntervalHours")]
    public int CheckIntervalHours { get; set; }

    [BsonElement("nextCheckAt")]
    public DateTime NextCheckAt { get; set; }

    [BsonElement("lastCheckedAt")]
    public DateTime? LastCheckedAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
