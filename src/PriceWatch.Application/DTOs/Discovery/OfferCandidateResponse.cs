using PriceWatch.Domain.Enums;

namespace PriceWatch.Application.DTOs.Discovery;

/// <summary>
/// Oferta encontrada em outra loja para um produto acompanhado. <c>Preselected</c> é true quando o
/// GTIN é idêntico (evidência objetiva); nas demais o usuário decide.
/// </summary>
public record OfferCandidateResponse(
    string Id,
    string ProductId,
    ProductSource Source,
    string StoreName,
    string Url,
    string Name,
    decimal Price,
    string? ImageUrl,
    string? Gtin,
    string? Brand,
    string? Model,
    double Score,
    CandidateReason Reason,
    bool Preselected,
    bool? AiSaysSame = null,
    string? AiReason = null);
