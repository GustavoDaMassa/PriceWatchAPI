using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class ClickLogDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("offerId")]
    public string OfferId { get; set; } = default!;

    [BsonElement("userId")]
    public string UserId { get; set; } = default!;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
