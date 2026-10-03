using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

public class OfferMatchFeedbackDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = default!;

    [BsonElement("userId")]
    public string UserId { get; set; } = default!;

    [BsonElement("candidateId")]
    public string CandidateId { get; set; } = default!;

    [BsonElement("productId")]
    public string ProductId { get; set; } = default!;

    [BsonElement("url")]
    public string Url { get; set; } = default!;

    [BsonElement("verdict")]
    public MatchVerdict Verdict { get; set; }

    [BsonElement("track")]
    public bool Track { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
