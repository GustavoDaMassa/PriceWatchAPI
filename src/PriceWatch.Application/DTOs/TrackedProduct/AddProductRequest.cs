using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.TrackedProduct;

public record AddProductRequest(string ListId, string Url, ProductSource Source, string Name, decimal TargetPrice);
