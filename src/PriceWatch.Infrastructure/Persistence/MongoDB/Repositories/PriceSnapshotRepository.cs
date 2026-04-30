using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class PriceSnapshotRepository : IPriceSnapshotRepository
{
    private readonly IMongoCollection<PriceSnapshotDocument> _collection;

    public PriceSnapshotRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PriceSnapshotDocument>("price_snapshots");
    }

    public async Task CreateAsync(PriceSnapshot snapshot)
    {
        await _collection.InsertOneAsync(PriceSnapshotMappings.ToDocument(snapshot));
    }

    public async Task<IEnumerable<PriceSnapshot>> GetByProductIdAsync(string productId, int limit = 100)
    {
        var docs = await _collection
            .Find(d => d.ProductId == productId)
            .SortByDescending(d => d.CapturedAt)
            .Limit(limit)
            .ToListAsync();
        return docs.Select(PriceSnapshotMappings.ToDomain);
    }
}
