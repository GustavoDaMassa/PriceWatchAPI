namespace PriceWatch.Domain.ValueObjects;

/// <summary>Uma oferta encontrada por busca em uma loja (ainda não é uma StoreOffer do catálogo).</summary>
public record OfferSearchResult(
    string Url,
    string Name,
    decimal Price,
    string? ImageUrl = null,
    string? Gtin = null,
    string? Brand = null,
    string? Model = null);
