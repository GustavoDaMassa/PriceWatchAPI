namespace PriceWatch.Application.DTOs.ProductList;

public record AnalysisItemDto(
    string ProductId,
    string ProductName,
    decimal CurrentPrice,
    decimal TargetPrice,
    decimal DistancePercent);
