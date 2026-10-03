using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class ProductDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("gtin")]
    public string? Gtin { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = default!;

    [BsonElement("brand")]
    public string? Brand { get; set; }

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("matchStatus")]
    public MatchStatus MatchStatus { get; set; }

    [BsonElement("suggestedMatchProductId")]
    public string? SuggestedMatchProductId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
