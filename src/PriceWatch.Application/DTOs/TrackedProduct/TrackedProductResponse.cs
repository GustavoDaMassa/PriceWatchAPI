using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.TrackedProduct;

public record TrackedProductResponse(
    string Id,
    string? ListId,
    string Name,
    string Url,
    string? ImageUrl,
    ProductSource Source,
    decimal TargetPrice,
    decimal CurrentPrice,
    decimal LowestPrice,
    bool IsActive,
    DateTime NextCheckAt);
