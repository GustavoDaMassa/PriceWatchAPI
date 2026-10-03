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
        await CreateProductIndexesAsync();
        await CreateStoreOfferIndexesAsync();
        await CreateUserProductSubscriptionIndexesAsync();
        await CreateClickLogIndexesAsync();
        await CreateDiscoveryIndexesAsync();
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
            new CreateIndexOptions { Name = "idx_tracked_products_userId_url" }));
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

    private async Task CreateDiscoveryIndexesAsync()
    {
        var candidates = _database.GetCollection<OfferCandidateDocument>("offer_candidates");

        await candidates.Indexes.CreateOneAsync(new CreateIndexModel<OfferCandidateDocument>(
            Builders<OfferCandidateDocument>.IndexKeys.Ascending(c => c.ProductId),
            new CreateIndexOptions { Name = "idx_offer_candidates_productId" }));

        // a mesma oferta (loja + URL) aparece uma vez por produto
        await candidates.Indexes.CreateOneAsync(new CreateIndexModel<OfferCandidateDocument>(
            Builders<OfferCandidateDocument>.IndexKeys
                .Ascending(c => c.ProductId)
                .Ascending(c => c.Source)
                .Ascending(c => c.Url),
            new CreateIndexOptions { Unique = true, Name = "idx_offer_candidates_product_source_url_unique" }));

        // TTL: candidatas expiram sozinhas (ExpireAfter zero = na data de expiresAt)
        await candidates.Indexes.CreateOneAsync(new CreateIndexModel<OfferCandidateDocument>(
            Builders<OfferCandidateDocument>.IndexKeys.Ascending(c => c.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "idx_offer_candidates_expiresAt_ttl" }));

        var feedback = _database.GetCollection<OfferMatchFeedbackDocument>("offer_match_feedback");

        // um veredito por usuário e candidata
        await feedback.Indexes.CreateOneAsync(new CreateIndexModel<OfferMatchFeedbackDocument>(
            Builders<OfferMatchFeedbackDocument>.IndexKeys
                .Ascending(f => f.UserId)
                .Ascending(f => f.CandidateId),
            new CreateIndexOptions { Unique = true, Name = "idx_offer_match_feedback_user_candidate_unique" }));

        await feedback.Indexes.CreateOneAsync(new CreateIndexModel<OfferMatchFeedbackDocument>(
            Builders<OfferMatchFeedbackDocument>.IndexKeys
                .Ascending(f => f.UserId)
                .Ascending(f => f.ProductId),
            new CreateIndexOptions { Name = "idx_offer_match_feedback_user_product" }));
    }

    private async Task CreateClickLogIndexesAsync()
    {
        var col = _database.GetCollection<ClickLogDocument>("click_logs");

        // relatório de cliques por oferta, mais recentes primeiro
        await col.Indexes.CreateOneAsync(new CreateIndexModel<ClickLogDocument>(
            Builders<ClickLogDocument>.IndexKeys
                .Ascending(c => c.OfferId)
                .Descending(c => c.CreatedAt),
            new CreateIndexOptions { Name = "idx_click_logs_offerId_createdAt" }));

        // exclusão de conta (LGPD)
        await col.Indexes.CreateOneAsync(new CreateIndexModel<ClickLogDocument>(
            Builders<ClickLogDocument>.IndexKeys.Ascending(c => c.UserId),
            new CreateIndexOptions { Name = "idx_click_logs_userId" }));
    }

    private async Task CreateProductIndexesAsync()
    {
        var col = _database.GetCollection<ProductDocument>("products");

        // não-único: Product sem GTIN (PendingReview/Unmatched) tem gtin = null
        await col.Indexes.CreateOneAsync(new CreateIndexModel<ProductDocument>(
            Builders<ProductDocument>.IndexKeys.Ascending(p => p.Gtin),
            new CreateIndexOptions { Name = "idx_products_gtin" }));
    }

    private async Task CreateStoreOfferIndexesAsync()
    {
        var col = _database.GetCollection<StoreOfferDocument>("store_offers");

        await col.Indexes.CreateOneAsync(new CreateIndexModel<StoreOfferDocument>(
            Builders<StoreOfferDocument>.IndexKeys.Ascending(o => o.ProductId),
            new CreateIndexOptions { Name = "idx_store_offers_productId" }));

        // query crítica do worker de verificação de preço (substitui o índice equivalente
        // de tracked_products conforme os offers vão assumindo esse papel)
        await col.Indexes.CreateOneAsync(new CreateIndexModel<StoreOfferDocument>(
            Builders<StoreOfferDocument>.IndexKeys
                .Ascending(o => o.IsActive)
                .Ascending(o => o.NextCheckAt),
            new CreateIndexOptions { Name = "idx_store_offers_check_queue" }));

        // dedup: evita criar duas ofertas pra mesma URL na mesma loja
        await col.Indexes.CreateOneAsync(new CreateIndexModel<StoreOfferDocument>(
            Builders<StoreOfferDocument>.IndexKeys
                .Ascending(o => o.Source)
                .Ascending(o => o.Url),
            new CreateIndexOptions { Unique = true, Name = "idx_store_offers_source_url_unique" }));
    }

    private async Task CreateUserProductSubscriptionIndexesAsync()
    {
        var col = _database.GetCollection<UserProductSubscriptionDocument>("user_product_subscriptions");

        await col.Indexes.CreateOneAsync(new CreateIndexModel<UserProductSubscriptionDocument>(
            Builders<UserProductSubscriptionDocument>.IndexKeys
                .Ascending(s => s.UserId)
                .Ascending(s => s.ProductId),
            new CreateIndexOptions { Unique = true, Name = "idx_subscriptions_userId_productId_unique" }));

        await col.Indexes.CreateOneAsync(new CreateIndexModel<UserProductSubscriptionDocument>(
            Builders<UserProductSubscriptionDocument>.IndexKeys.Ascending(s => s.ListId),
            new CreateIndexOptions { Name = "idx_subscriptions_listId" }));

        // o worker de ofertas carrega os assinantes de um produto a cada checagem
        await col.Indexes.CreateOneAsync(new CreateIndexModel<UserProductSubscriptionDocument>(
            Builders<UserProductSubscriptionDocument>.IndexKeys.Ascending(s => s.ProductId),
            new CreateIndexOptions { Name = "idx_subscriptions_productId" }));
    }
}
