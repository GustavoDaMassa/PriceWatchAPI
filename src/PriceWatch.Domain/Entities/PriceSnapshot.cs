namespace PriceWatch.Domain.Entities;

public class PriceSnapshot
{
    public string Id { get; private set; } = default!;
    public string ProductId { get; private set; } = default!;
    public decimal Price { get; private set; }
    public DateTime CapturedAt { get; private set; }

    private PriceSnapshot() { }

    public static PriceSnapshot Create(string productId, decimal price)
    {
        return new PriceSnapshot
        {
            Id = Guid.NewGuid().ToString(),
            ProductId = productId,
            Price = price,
            CapturedAt = DateTime.UtcNow
        };
    }

    public static PriceSnapshot Restore(string id, string productId, decimal price, DateTime capturedAt)
    {
        return new PriceSnapshot
        {
            Id = id,
            ProductId = productId,
            Price = price,
            CapturedAt = capturedAt
        };
    }
}
