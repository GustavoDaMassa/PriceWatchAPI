using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class OfferCandidateMappings
{
    public static OfferCandidateDocument ToDocument(OfferCandidate c) => new()
    {
        Id = c.Id, ProductId = c.ProductId, Source = c.Source, Url = c.Url, Name = c.Name, Price = c.Price,
        ImageUrl = c.ImageUrl, Gtin = c.Gtin, Brand = c.Brand, Model = c.Model, Score = c.Score,
        Reason = c.Reason, AiSaysSame = c.AiSaysSame, AiReason = c.AiReason, CreatedAt = c.CreatedAt, ExpiresAt = c.ExpiresAt
    };

    public static OfferCandidate ToDomain(OfferCandidateDocument d) => OfferCandidate.Restore(
        d.Id, d.ProductId, d.Source, d.Url, d.Name, d.Price, d.ImageUrl, d.Gtin, d.Brand, d.Model,
        d.Score, d.Reason, d.CreatedAt, d.ExpiresAt, d.AiSaysSame, d.AiReason);
}

public static class OfferMatchFeedbackMappings
{
    public static OfferMatchFeedbackDocument ToDocument(OfferMatchFeedback f) => new()
    {
        Id = f.Id, UserId = f.UserId, CandidateId = f.CandidateId, ProductId = f.ProductId, Url = f.Url,
        Verdict = f.Verdict, Track = f.Track, CreatedAt = f.CreatedAt
    };

    public static OfferMatchFeedback ToDomain(OfferMatchFeedbackDocument d) => OfferMatchFeedback.Restore(
        d.Id, d.UserId, d.CandidateId, d.ProductId, d.Url, d.Verdict, d.Track, d.CreatedAt);
}
