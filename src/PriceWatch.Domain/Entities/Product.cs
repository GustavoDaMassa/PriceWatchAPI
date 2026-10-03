using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Entities;

public class Product
{
    public string Id { get; private set; } = default!;
    public string? Gtin { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public string? ImageUrl { get; private set; }
    public MatchStatus MatchStatus { get; private set; }
    public string? SuggestedMatchProductId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Product() { }

    public static Product Create(
        string name,
        string? gtin = null,
        string? brand = null,
        string? model = null,
        string? imageUrl = null)
    {
        return new Product
        {
            Id = Guid.NewGuid().ToString(),
            Gtin = gtin,
            Name = name,
            Brand = brand,
            Model = model,
            ImageUrl = imageUrl,
            MatchStatus = MatchStatus.Unmatched,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Product Restore(
        string id,
        string? gtin,
        string name,
        string? brand,
        string? model,
        string? imageUrl,
        MatchStatus matchStatus,
        string? suggestedMatchProductId,
        DateTime createdAt)
    {
        return new Product
        {
            Id = id,
            Gtin = gtin,
            Name = name,
            Brand = brand,
            Model = model,
            ImageUrl = imageUrl,
            MatchStatus = matchStatus,
            SuggestedMatchProductId = suggestedMatchProductId,
            CreatedAt = createdAt
        };
    }

    public void MarkAutoMatched() => MatchStatus = MatchStatus.AutoMatched;

    // fuzzy match (sem GTIN): nunca funde automaticamente — só sugere. O merge de fato
    // (mover StoreOffer/UserProductSubscription) só acontece quando um humano confirmar
    // (ver RF15/RI07 em docs/requisitos.md; confirmação em si é trabalho futuro, fora da Fase 5)
    public void SuggestMatch(string candidateProductId)
    {
        MatchStatus = MatchStatus.PendingReview;
        SuggestedMatchProductId = candidateProductId;
    }

    public void Confirm() => MatchStatus = MatchStatus.Confirmed;
    public void Reject() => MatchStatus = MatchStatus.Rejected;

    public bool IsVisibleInComparison() =>
        MatchStatus is MatchStatus.Unmatched or MatchStatus.AutoMatched or MatchStatus.Confirmed;
}
