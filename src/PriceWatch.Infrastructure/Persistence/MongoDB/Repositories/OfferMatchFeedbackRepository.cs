using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class OfferMatchFeedbackRepository : IOfferMatchFeedbackRepository
{
    private readonly IMongoCollection<OfferMatchFeedbackDocument> _collection;

    public OfferMatchFeedbackRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<OfferMatchFeedbackDocument>("offer_match_feedback");
    }

    public async Task<OfferMatchFeedback?> GetByUserAndCandidateAsync(string userId, string candidateId)
    {
        var doc = await _collection
            .Find(d => d.UserId == userId && d.CandidateId == candidateId)
            .FirstOrDefaultAsync();
        return doc is null ? null : OfferMatchFeedbackMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<OfferMatchFeedback>> GetByUserAndProductAsync(string userId, string productId)
    {
        var docs = await _collection.Find(d => d.UserId == userId && d.ProductId == productId).ToListAsync();
        return docs.Select(OfferMatchFeedbackMappings.ToDomain);
    }

    public async Task SaveAsync(OfferMatchFeedback feedback)
    {
        await _collection.ReplaceOneAsync(
            d => d.Id == feedback.Id,
            OfferMatchFeedbackMappings.ToDocument(feedback),
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteByUserIdAsync(string userId)
    {
        await _collection.DeleteManyAsync(d => d.UserId == userId);
    }
}
