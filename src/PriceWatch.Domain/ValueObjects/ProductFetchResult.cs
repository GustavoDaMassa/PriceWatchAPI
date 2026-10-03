namespace PriceWatch.Domain.ValueObjects;

public record ProductFetchResult(
    decimal Price,
    string Name,
    string? ImageUrl = null,
    string? Gtin = null,
    string? Brand = null,
    string? Model = null);
