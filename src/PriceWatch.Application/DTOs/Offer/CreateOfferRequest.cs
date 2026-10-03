namespace PriceWatch.Application.DTOs.Offer;

public record CreateOfferRequest(string Url, decimal TargetPrice, string? ListId = null);
