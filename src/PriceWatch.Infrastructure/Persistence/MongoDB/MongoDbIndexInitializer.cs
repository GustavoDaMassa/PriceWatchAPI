using MongoDB.Driver;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB;

public class MongoDbIndexInitializer
{
    private readonly IMongoDatabase _database;

    public MongoDbIndexInitializer(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        await CreateUserIndexesAsync();
        await CreateProductListIndexesAsync();
        await CreateTrackedProductIndexesAsync();
        await CreatePriceSnapshotIndexesAsync();
        await CreateNotificationIndexesAsync();
    }

    private async Task CreateUserIndexesAsync()
    {
        var col = _database.GetCollection<UserDocument>("users");

        await col.Indexes.CreateOneAsync(new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(u => u.Email),
            new CreateIndexOptions { Unique = true, Name = "idx_users_email_unique" }));
    }

    private async Task CreateProductListIndexesAsync()
    {
        var col = _database.GetCollection<ProductListDocument>("product_lists");

        await col.Indexes.CreateOneAsync(new CreateIndexModel<ProductListDocument>(
            Builders<ProductListDocument>.IndexKeys.Ascending(p => p.UserId),
            new CreateIndexOptions { Name = "idx_product_lists_userId" }));
    }

    private async Task CreateTrackedProductIndexesAsync()
    {
        var col = _database.GetCollection<TrackedProductDocument>("tracked_products");

        // índice composto usado pelo PriceCheckWorker: isActive + nextCheckAt
        await col.Indexes.CreateOneAsync(new CreateIndexModel<TrackedProductDocument>(
            Builders<TrackedProductDocument>.IndexKeys
                .Ascending(p => p.IsActive)
                .Ascending(p => p.NextCheckAt),
            new CreateIndexOptions { Name = "idx_tracked_products_check_queue" }));

        await col.Indexes.CreateOneAsync(new CreateIndexModel<TrackedProductDocument>(
            Builders<TrackedProductDocument>.IndexKeys.Ascending(p => p.ListId),
            new CreateIndexOptions { Name = "idx_tracked_products_listId" }));

        await col.Indexes.CreateOneAsync(new CreateIndexModel<TrackedProductDocument>(
            Builders<TrackedProductDocument>.IndexKeys.Ascending(p => p.UserId),
            new CreateIndexOptions { Name = "idx_tracked_products_userId" }));

        await col.Indexes.CreateOneAsync(new CreateIndexModel<TrackedProductDocument>(
            Builders<TrackedProductDocument>.IndexKeys
                .Ascending(p => p.UserId)
                .Ascending(p => p.Url),
            new CreateIndexOptions { Unique = true, Name = "idx_tracked_products_userId_url_unique" }));
    }

    private async Task CreatePriceSnapshotIndexesAsync()
    {
        var col = _database.GetCollection<PriceSnapshotDocument>("price_snapshots");

        // produtId + capturedAt desc: query padrão do GetPriceHistoryUseCase
        await col.Indexes.CreateOneAsync(new CreateIndexModel<PriceSnapshotDocument>(
            Builders<PriceSnapshotDocument>.IndexKeys
                .Ascending(s => s.ProductId)
                .Descending(s => s.CapturedAt),
            new CreateIndexOptions { Name = "idx_price_snapshots_productId_capturedAt" }));
    }

    private async Task CreateNotificationIndexesAsync()
    {
        var col = _database.GetCollection<NotificationDocument>("notifications");

        // query principal: userId + filtro isRead
        await col.Indexes.CreateOneAsync(new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys
                .Ascending(n => n.UserId)
                .Ascending(n => n.IsRead),
            new CreateIndexOptions { Name = "idx_notifications_userId_isRead" }));

        // ordenação por data para exibição
        await col.Indexes.CreateOneAsync(new CreateIndexModel<NotificationDocument>(
            Builders<NotificationDocument>.IndexKeys
                .Ascending(n => n.UserId)
                .Descending(n => n.CreatedAt),
            new CreateIndexOptions { Name = "idx_notifications_userId_createdAt" }));
    }
}
