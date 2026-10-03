using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class DeviceTokenRepository : IDeviceTokenRepository
{
    private readonly IMongoCollection<DeviceTokenDocument> _collection;

    public DeviceTokenRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DeviceTokenDocument>("device_tokens");
    }

    public async Task<DeviceToken?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : DeviceTokenMappings.ToDomain(doc);
    }

    public async Task<DeviceToken?> GetByTokenAsync(string token)
    {
        var doc = await _collection.Find(d => d.Token == token).FirstOrDefaultAsync();
        return doc is null ? null : DeviceTokenMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<DeviceToken>> GetByUserIdAsync(string userId)
    {
        var docs = await _collection.Find(d => d.UserId == userId).ToListAsync();
        return docs.Select(DeviceTokenMappings.ToDomain);
    }

    public async Task SaveAsync(DeviceToken device)
    {
        await _collection.ReplaceOneAsync(
            d => d.Id == device.Id,
            DeviceTokenMappings.ToDocument(device),
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(d => d.Id == id);
    }

    public async Task DeleteByUserIdAsync(string userId)
    {
        await _collection.DeleteManyAsync(d => d.UserId == userId);
    }
}
