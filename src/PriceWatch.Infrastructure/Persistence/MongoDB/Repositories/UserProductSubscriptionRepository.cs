using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class UserProductSubscriptionRepository : IUserProductSubscriptionRepository
{
    private readonly IMongoCollection<UserProductSubscriptionDocument> _collection;

    public UserProductSubscriptionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<UserProductSubscriptionDocument>("user_product_subscriptions");
    }

    public async Task<UserProductSubscription?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : UserProductSubscriptionMappings.ToDomain(doc);
    }

    public async Task<UserProductSubscription?> GetByUserIdAndProductIdAsync(string userId, string productId)
    {
        var doc = await _collection
            .Find(d => d.UserId == userId && d.ProductId == productId)
            .FirstOrDefaultAsync();
        return doc is null ? null : UserProductSubscriptionMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<UserProductSubscription>> GetByProductIdAsync(string productId)
    {
        var docs = await _collection.Find(d => d.ProductId == productId).ToListAsync();
        return docs.Select(UserProductSubscriptionMappings.ToDomain);
    }

    public async Task<IEnumerable<UserProductSubscription>> GetByUserIdAsync(string userId, string? listId = null)
    {
        var filter = listId is not null
            ? Builders<UserProductSubscriptionDocument>.Filter.And(
                Builders<UserProductSubscriptionDocument>.Filter.Eq(d => d.UserId, userId),
                Builders<UserProductSubscriptionDocument>.Filter.Eq(d => d.ListId, listId))
            : Builders<UserProductSubscriptionDocument>.Filter.Eq(d => d.UserId, userId);

        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(UserProductSubscriptionMappings.ToDomain);
    }

    public async Task CreateAsync(UserProductSubscription subscription)
    {
        await _collection.InsertOneAsync(UserProductSubscriptionMappings.ToDocument(subscription));
    }

    public async Task UpdateAsync(UserProductSubscription subscription)
    {
        await _collection.ReplaceOneAsync(d => d.Id == subscription.Id, UserProductSubscriptionMappings.ToDocument(subscription));
    }

    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(d => d.Id == id);
    }

    public async Task UnlinkByListIdAsync(string listId)
    {
        var update = Builders<UserProductSubscriptionDocument>.Update.Set(d => d.ListId, (string?)null);
        await _collection.UpdateManyAsync(d => d.ListId == listId, update);
    }
}
