using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.Offer;

public record OfferResponse(
    string SubscriptionId,
    string ProductId,
    string OfferId,
    string Name,
    string Url,
    string? ImageUrl,
    ProductSource Source,
    decimal TargetPrice,
    decimal CurrentPrice,
    decimal LowestPrice,
    bool IsActive,
    DateTime NextCheckAt);
