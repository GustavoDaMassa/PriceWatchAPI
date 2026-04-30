namespace PriceWatch.Application.DTOs.TrackedProduct;

public record PriceSnapshotResponse(string Id, decimal Price, DateTime CapturedAt);
