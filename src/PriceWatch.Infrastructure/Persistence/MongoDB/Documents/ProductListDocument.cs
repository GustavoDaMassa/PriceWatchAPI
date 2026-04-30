using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class ProductListDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("userId")]
    public string UserId { get; set; } = default!;

    [BsonElement("name")]
    public string Name { get; set; } = default!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
