using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class PriceSnapshotDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("productId")]
    public string ProductId { get; set; } = default!;

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("capturedAt")]
    public DateTime CapturedAt { get; set; }
}
