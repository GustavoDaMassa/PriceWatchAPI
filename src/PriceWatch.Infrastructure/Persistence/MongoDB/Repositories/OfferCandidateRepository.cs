using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class OfferCandidateRepository : IOfferCandidateRepository
{
    private readonly IMongoCollection<OfferCandidateDocument> _collection;

    public OfferCandidateRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<OfferCandidateDocument>("offer_candidates");
    }

    public async Task<OfferCandidate?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : OfferCandidateMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<OfferCandidate>> GetByProductIdAsync(string productId)
    {
        var now = DateTime.UtcNow;
        // o TTL do Mongo roda a cada ~1 min: filtra também aqui para nunca devolver candidata vencida
        var docs = await _collection
            .Find(d => d.ProductId == productId && d.ExpiresAt > now)
            .SortByDescending(d => d.Score)
            .ToListAsync();
        return docs.Select(OfferCandidateMappings.ToDomain);
    }

    public async Task<OfferCandidate?> GetByKeyAsync(string productId, ProductSource source, string url)
    {
        var doc = await _collection
            .Find(d => d.ProductId == productId && d.Source == source && d.Url == url)
            .FirstOrDefaultAsync();
        return doc is null ? null : OfferCandidateMappings.ToDomain(doc);
    }

    public async Task SaveAsync(OfferCandidate candidate)
    {
        await _collection.ReplaceOneAsync(
            d => d.Id == candidate.Id,
            OfferCandidateMappings.ToDocument(candidate),
            new ReplaceOptions { IsUpsert = true });
    }
}
