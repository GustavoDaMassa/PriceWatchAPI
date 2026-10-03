using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class OfferCandidateDocument
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

    [BsonElement("name")]
    public string Name { get; set; } = default!;

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("gtin")]
    public string? Gtin { get; set; }

    [BsonElement("brand")]
    public string? Brand { get; set; }

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("score")]
    public double Score { get; set; }

    [BsonElement("reason")]
    public CandidateReason Reason { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    // índice TTL: o Mongo apaga a candidata sozinho depois desta data
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}
