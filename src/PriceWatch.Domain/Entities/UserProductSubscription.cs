namespace PriceWatch.Domain.Entities;

public class UserProductSubscription
{
    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string ProductId { get; private set; } = default!;
    public string? ListId { get; private set; }
    public decimal TargetPrice { get; private set; }
    public bool IsActive { get; private set; }
    public bool TargetAlertSent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserProductSubscription() { }

    public static UserProductSubscription Create(
        string userId,
        string productId,
        decimal targetPrice,
        string? listId = null)
    {
        return new UserProductSubscription
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            ProductId = productId,
            ListId = listId,
            TargetPrice = targetPrice,
            IsActive = true,
            TargetAlertSent = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static UserProductSubscription Restore(
        string id,
        string userId,
        string productId,
        string? listId,
        decimal targetPrice,
        bool isActive,
        bool targetAlertSent,
        DateTime createdAt)
    {
        return new UserProductSubscription
        {
            Id = id,
            UserId = userId,
            ProductId = productId,
            ListId = listId,
            TargetPrice = targetPrice,
            IsActive = isActive,
            TargetAlertSent = targetAlertSent,
            CreatedAt = createdAt
        };
    }

    public void SetTargetPrice(decimal targetPrice)
    {
        TargetPrice = targetPrice;
        TargetAlertSent = false;
    }

    public void AssignToList(string listId) => ListId = listId;
    public void RemoveFromList() => ListId = null;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public bool ShouldTriggerTargetAlert(decimal currentPrice) =>
        TargetPrice > 0 && currentPrice <= TargetPrice && !TargetAlertSent;

    public void MarkTargetAlertSent() => TargetAlertSent = true;

    // usado pelo motor de matching (Fase 5) ao unificar produtos — ver MatchStoreOfferUseCase
    public void ReassignToProduct(string productId) => ProductId = productId;
}
