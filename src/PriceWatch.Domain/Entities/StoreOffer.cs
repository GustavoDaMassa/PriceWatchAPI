using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Entities;

public class StoreOffer
{
    public string Id { get; private set; } = default!;
    public string ProductId { get; private set; } = default!;
    public ProductSource Source { get; private set; }
    public string Url { get; private set; } = default!;
    public string? Gtin { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal LowestPrice { get; private set; }
    public string? AffiliateUrl { get; private set; }
    public int CheckIntervalHours { get; private set; }
    public DateTime NextCheckAt { get; private set; }
    public DateTime? LastCheckedAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private StoreOffer() { }

    public static StoreOffer Create(
        string productId,
        ProductSource source,
        string url,
        string? gtin = null,
        string? id = null)
    {
        return new StoreOffer
        {
            Id = id ?? Guid.NewGuid().ToString(),
            ProductId = productId,
            Source = source,
            Url = url,
            Gtin = gtin,
            CurrentPrice = 0m,
            LowestPrice = 0m,
            CheckIntervalHours = 1,
            NextCheckAt = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static StoreOffer Restore(
        string id,
        string productId,
        ProductSource source,
        string url,
        string? gtin,
        decimal currentPrice,
        decimal lowestPrice,
        string? affiliateUrl,
        int checkIntervalHours,
        DateTime nextCheckAt,
        DateTime? lastCheckedAt,
        bool isActive,
        DateTime createdAt)
    {
        return new StoreOffer
        {
            Id = id,
            ProductId = productId,
            Source = source,
            Url = url,
            Gtin = gtin,
            CurrentPrice = currentPrice,
            LowestPrice = lowestPrice,
            AffiliateUrl = affiliateUrl,
            CheckIntervalHours = checkIntervalHours,
            NextCheckAt = nextCheckAt,
            LastCheckedAt = lastCheckedAt,
            IsActive = isActive,
            CreatedAt = createdAt
        };
    }

    // reaproveita o mesmo agregado de série temporal do TrackedProduct legado (price_snapshots),
    // usando StoreOffer.Id no lugar do antigo TrackedProduct.Id — ver docs/modelagem.md
    public PriceSnapshot RecordPrice(decimal price)
    {
        CurrentPrice = price;

        if (LowestPrice == 0m || price < LowestPrice)
            LowestPrice = price;

        LastCheckedAt = DateTime.UtcNow;
        NextCheckAt = DateTime.UtcNow.AddHours(CheckIntervalHours);

        return PriceSnapshot.Create(Id, price);
    }

    // adia a próxima checagem sem alterar preços — usado quando a loja falha, para não
    // bater na API externa a cada ciclo do worker
    public void PostponeCheck(TimeSpan delay) => NextCheckAt = DateTime.UtcNow.Add(delay);

    // usado pelo backfill: preserva o menor preço histórico do TrackedProduct legado
    public void RestoreLowestPrice(decimal historicLowest)
    {
        if (historicLowest > 0 && (LowestPrice == 0m || historicLowest < LowestPrice))
            LowestPrice = historicLowest;
    }

    public void SetAffiliateUrl(string affiliateUrl) => AffiliateUrl = affiliateUrl;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    // usado pelo motor de matching (Fase 5) ao unificar duas ofertas do mesmo produto
    // físico sob um único Product — ver MatchStoreOfferUseCase
    public void ReassignToProduct(string productId) => ProductId = productId;
}
