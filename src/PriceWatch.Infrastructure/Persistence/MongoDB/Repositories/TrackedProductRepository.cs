using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class TrackedProductRepository : ITrackedProductRepository
{
    private readonly IMongoCollection<TrackedProductDocument> _collection;

    public TrackedProductRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TrackedProductDocument>("tracked_products");
    }

    public async Task<IEnumerable<TrackedProduct>> GetByUserIdAsync(string userId, string? listId = null)
    {
        var filter = listId is not null
            ? Builders<TrackedProductDocument>.Filter.And(
                Builders<TrackedProductDocument>.Filter.Eq(d => d.UserId, userId),
                Builders<TrackedProductDocument>.Filter.Eq(d => d.ListId, listId))
            : Builders<TrackedProductDocument>.Filter.Eq(d => d.UserId, userId);

        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(TrackedProductMappings.ToDomain);
    }

    public async Task<TrackedProduct?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : TrackedProductMappings.ToDomain(doc);
    }

    public async Task<TrackedProduct?> GetByUserIdAndUrlAsync(string userId, string url)
    {
        var doc = await _collection
            .Find(d => d.UserId == userId && d.Url == url)
            .FirstOrDefaultAsync();
        return doc is null ? null : TrackedProductMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<TrackedProduct>> GetDueForCheckAsync()
    {
        var now = DateTime.UtcNow;
        var docs = await _collection
            .Find(d => d.IsActive && d.NextCheckAt <= now)
            .ToListAsync();
        return docs.Select(TrackedProductMappings.ToDomain);
    }

    public async Task CreateAsync(TrackedProduct product)
    {
        await _collection.InsertOneAsync(TrackedProductMappings.ToDocument(product));
    }

    public async Task UpdateAsync(TrackedProduct product)
    {
        await _collection.ReplaceOneAsync(d => d.Id == product.Id, TrackedProductMappings.ToDocument(product));
    }

    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(d => d.Id == id);
    }

    public async Task UnlinkByListIdAsync(string listId)
    {
        var update = Builders<TrackedProductDocument>.Update.Set(d => d.ListId, (string?)null);
        await _collection.UpdateManyAsync(d => d.ListId == listId, update);
    }
}
