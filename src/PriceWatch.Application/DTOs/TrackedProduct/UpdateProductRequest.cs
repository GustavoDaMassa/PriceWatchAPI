namespace PriceWatch.Application.DTOs.TrackedProduct;

public record UpdateProductRequest(decimal TargetPrice, bool IsActive);
