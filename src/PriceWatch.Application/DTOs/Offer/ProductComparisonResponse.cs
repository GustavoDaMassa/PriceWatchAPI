using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.Offer;

public record ProductComparisonResponse(
    string ProductId,
    string Name,
    string? ImageUrl,
    IReadOnlyList<StoreOfferSummary> Offers);

public record StoreOfferSummary(
    string OfferId,
    ProductSource Source,
    decimal CurrentPrice,
    bool IsCheapest,
    string StoreName = "",
    decimal LowestPrice = 0m,
    DateTime? LastCheckedAt = null);
