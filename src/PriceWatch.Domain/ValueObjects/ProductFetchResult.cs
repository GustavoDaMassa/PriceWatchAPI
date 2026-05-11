namespace PriceWatch.Domain.ValueObjects;

public record ProductFetchResult(decimal Price, string Name, string? ImageUrl = null);
