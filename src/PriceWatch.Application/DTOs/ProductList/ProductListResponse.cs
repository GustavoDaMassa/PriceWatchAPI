namespace PriceWatch.Application.DTOs.ProductList;

public record ProductListResponse(string Id, string Name, string? Description, DateTime CreatedAt);
