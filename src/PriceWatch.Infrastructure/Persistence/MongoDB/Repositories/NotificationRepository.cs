using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<NotificationDocument> _collection;

    public NotificationRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<NotificationDocument>("notifications");
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(string userId, bool? isRead = null)
    {
        var filter = Builders<NotificationDocument>.Filter.Eq(d => d.UserId, userId);
        if (isRead.HasValue)
            filter &= Builders<NotificationDocument>.Filter.Eq(d => d.IsRead, isRead.Value);

        var docs = await _collection.Find(filter).ToListAsync();
        return docs.Select(NotificationMappings.ToDomain);
    }

    public async Task<Notification?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : NotificationMappings.ToDomain(doc);
    }

    public async Task CreateAsync(Notification notification)
    {
        await _collection.InsertOneAsync(NotificationMappings.ToDocument(notification));
    }

    public async Task UpdateAsync(Notification notification)
    {
        await _collection.ReplaceOneAsync(d => d.Id == notification.Id, NotificationMappings.ToDocument(notification));
    }

    public async Task DeleteByUserIdAsync(string userId)
    {
        await _collection.DeleteManyAsync(d => d.UserId == userId);
    }
}
