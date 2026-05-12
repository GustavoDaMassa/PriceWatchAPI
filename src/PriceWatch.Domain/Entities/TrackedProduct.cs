using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Entities;

public class TrackedProduct
{
    public string Id { get; private set; } = default!;
    public string? ListId { get; private set; }
    public string UserId { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public ProductSource Source { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ImageUrl { get; private set; }
    public decimal TargetPrice { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal LowestPrice { get; private set; }
    public int CheckIntervalHours { get; private set; } = 1;
    public DateTime NextCheckAt { get; private set; }
    public DateTime? LastCheckedAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public DateTime CreatedAt { get; private set; }

    private TrackedProduct() { }

    public void AssignToList(string listId) => ListId = listId;
    public void RemoveFromList() => ListId = null;

    public static TrackedProduct Create(
        string userId,
        string url,
        ProductSource source,
        string name,
        decimal targetPrice,
        string? listId = null,
        string? imageUrl = null)
    {
        return new TrackedProduct
        {
            Id = Guid.NewGuid().ToString(),
            ListId = listId,
            UserId = userId,
            Url = url,
            Source = source,
            Name = name,
            ImageUrl = imageUrl,
            TargetPrice = targetPrice,
            CurrentPrice = 0m,
            LowestPrice = 0m,
            CheckIntervalHours = 1,
            NextCheckAt = DateTime.UtcNow,
            IsActive = true,
            Metadata = new Dictionary<string, string>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public static TrackedProduct Restore(
        string id,
        string? listId,
        string userId,
        string url,
        ProductSource source,
        string name,
        string? imageUrl,
        decimal targetPrice,
        decimal currentPrice,
        decimal lowestPrice,
        int checkIntervalHours,
        DateTime nextCheckAt,
        DateTime? lastCheckedAt,
        bool isActive,
        Dictionary<string, string> metadata,
        DateTime createdAt)
    {
        return new TrackedProduct
        {
            Id = id,
            ListId = listId,
            UserId = userId,
            Url = url,
            Source = source,
            Name = name,
            ImageUrl = imageUrl,
            TargetPrice = targetPrice,
            CurrentPrice = currentPrice,
            LowestPrice = lowestPrice,
            CheckIntervalHours = checkIntervalHours,
            NextCheckAt = nextCheckAt,
            LastCheckedAt = lastCheckedAt,
            IsActive = isActive,
            Metadata = metadata,
            CreatedAt = createdAt
        };
    }

    public PriceSnapshot RecordPrice(decimal price)
    {
        CurrentPrice = price;

        if (LowestPrice == 0m || price < LowestPrice)
            LowestPrice = price;

        LastCheckedAt = DateTime.UtcNow;
        NextCheckAt = DateTime.UtcNow.AddHours(CheckIntervalHours);

        return PriceSnapshot.Create(Id, price);
    }

    public bool ShouldTriggerTargetAlert() => CurrentPrice <= TargetPrice;

    public bool ShouldTriggerLowestAlert(decimal previousLowest) => CurrentPrice < previousLowest;

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
