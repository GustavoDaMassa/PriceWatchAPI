using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class ClickLogRepository : IClickLogRepository
{
    private readonly IMongoCollection<ClickLogDocument> _collection;

    public ClickLogRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ClickLogDocument>("click_logs");
    }

    public async Task CreateAsync(ClickLog click)
    {
        await _collection.InsertOneAsync(ClickLogMappings.ToDocument(click));
    }

    public async Task<long> CountByOfferIdAsync(string offerId)
    {
        return await _collection.CountDocumentsAsync(d => d.OfferId == offerId);
    }

    public async Task<IEnumerable<ClickLog>> GetByUserIdAsync(string userId)
    {
        var docs = await _collection.Find(d => d.UserId == userId).ToListAsync();
        return docs.Select(ClickLogMappings.ToDomain);
    }

    public async Task DeleteByUserIdAsync(string userId)
    {
        await _collection.DeleteManyAsync(d => d.UserId == userId);
    }
}
