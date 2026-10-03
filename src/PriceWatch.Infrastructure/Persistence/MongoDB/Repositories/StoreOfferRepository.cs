using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class StoreOfferRepository : IStoreOfferRepository
{
    private readonly IMongoCollection<StoreOfferDocument> _collection;

    public StoreOfferRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<StoreOfferDocument>("store_offers");
    }

    public async Task<StoreOffer?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : StoreOfferMappings.ToDomain(doc);
    }

    public async Task<StoreOffer?> GetBySourceAndUrlAsync(ProductSource source, string url)
    {
        var doc = await _collection
            .Find(d => d.Source == source && d.Url == url)
            .FirstOrDefaultAsync();
        return doc is null ? null : StoreOfferMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<StoreOffer>> GetByProductIdAsync(string productId)
    {
        var docs = await _collection.Find(d => d.ProductId == productId).ToListAsync();
        return docs.Select(StoreOfferMappings.ToDomain);
    }

    public async Task<IEnumerable<StoreOffer>> GetDueForCheckAsync()
    {
        var now = DateTime.UtcNow;
        var docs = await _collection
            .Find(d => d.IsActive && d.NextCheckAt <= now)
            .ToListAsync();
        return docs.Select(StoreOfferMappings.ToDomain);
    }

    public async Task CreateAsync(StoreOffer offer)
    {
        await _collection.InsertOneAsync(StoreOfferMappings.ToDocument(offer));
    }

    public async Task UpdateAsync(StoreOffer offer)
    {
        await _collection.ReplaceOneAsync(d => d.Id == offer.Id, StoreOfferMappings.ToDocument(offer));
    }
}
